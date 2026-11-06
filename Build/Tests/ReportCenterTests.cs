using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Serialization;
using System.Windows.Documents;
using System.Xml.Serialization;
using Samba.Domain.Models.Accounts;
using Samba.Domain.Models.Settings;
using Samba.Domain.Models.Tickets;
using Samba.Modules.AccountModule;
using Samba.Modules.BasicReports;
using Samba.Services;
using Samba.Services.Common;
using Samba.Presentation.Services;

class TestProxy:RealProxy
{
 public Func<IMethodCallMessage,object> Handler;
 public TestProxy(Type type,Func<IMethodCallMessage,object> handler):base(type){Handler=handler;}
 public override IMessage Invoke(IMessage msg){var call=(IMethodCallMessage)msg;return new ReturnMessage(Handler(call),null,0,call.LogicalCallContext,call);}
}
class ReportCenterTests
{
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS: "+name);}
 static void Field(object obj,string name,object value){obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(obj,value);}
 [STAThread] static int Main(){try{Run();return 0;}catch(Exception ex){Console.WriteLine(ex.ToString());return 1;}}
 static void Run(){
  var a=new Ticket {Id=1,TicketNumber="T1",Date=new DateTime(2024,2,29,10,0,0),TotalAmount=100,RemainingAmount=25,DepartmentId=1,TicketTypeId=1,LastModifiedUserName="A"};
  var b=new Ticket {Id=2,TicketNumber="T2",Date=new DateTime(2024,2,29,11,0,0),TotalAmount=50,RemainingAmount=0,IsClosed=true,DepartmentId=2,TicketTypeId=1,LastModifiedUserName="B"};
  a.Payments.Add(new Payment{Name="JazzCash",Amount=75});b.Payments.Add(new Payment{Name="Easypaisa",Amount=50});
  var tickets=new[]{a,b};
  foreach(var mode in OperationalReportViewModel.Modes){var rows=OperationalReportViewModel.BuildRows(tickets,mode);Check(rows.Count>1 && rows.All(r=>r.Length==rows[0].Length),mode+" creates consistent columns");Check(OperationalReportViewModel.BuildRows(new Ticket[0],mode).Count==1,mode+" handles empty data");}
  var settingsValues=(IProgramSettings)new TestProxy(typeof(IProgramSettings),call=>call.MethodName=="get_UserInfo"?(object)"Demonstration business":null).GetTransparentProxy();
  var settings=(ISettingService)new TestProxy(typeof(ISettingService),call=>call.MethodName=="get_ProgramSettings"?settingsValues:null).GetTransparentProxy();
  ReportContext.CurrentWorkPeriod=new WorkPeriod{StartDate=new DateTime(2024,2,1),EndDate=new DateTime(2024,3,1)};
  typeof(ReportContext).GetField("_tickets",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,tickets);
  foreach(var mode in OperationalReportViewModel.Modes){var document=new OperationalReportViewModel(mode,null,null,null,settings).GetReportDocument();Check(document.Blocks.OfType<Table>().Single().Columns.Count==OperationalReportViewModel.BuildRows(tickets,mode)[0].Length,mode+" creates complete printable document");}
  var daily=OperationalReportViewModel.BuildRows(tickets,"Daily sales");Check(daily[1][1]=="2"&&daily[1][2]=="150.00"&&daily[1][3]=="25.00","daily ticket totals and outstanding reconcile");
  var payments=OperationalReportViewModel.BuildRows(tickets,"Payment methods");Check(payments.Any(r=>r[0]=="JazzCash"&&r[2]=="75.00")&&payments.Any(r=>r[0]=="Easypaisa"&&r[2]=="50.00"),"wallet summaries reconcile with original amounts");
  Check(ReportCsv.Escape("a,\"b\"")=="\"a,\"\"b\"\"\"","CSV escapes commas and quotes");Check(ReportCsv.Escape("=1+1").StartsWith("\"'="),"CSV neutralizes spreadsheet formula labels");
  var doc=new FlowDocument();var table=new Table();var group=new TableRowGroup();var row=new TableRow();row.Cells.Add(new TableCell(new Paragraph(new Run("A,B"))));group.Rows.Add(row);table.RowGroups.Add(group);doc.Blocks.Add(table);Check(ReportCsv.Rows(doc.Blocks).Single()=="\"A,B\"","CSV reads rendered table cells");
  var parse=typeof(ReportContext).GetMethod("StrToDate",BindingFlags.Static|BindingFlags.NonPublic);Check((DateTime)parse.Invoke(null,new object[]{"30 02 2024"})==new DateTime(2024,2,29),"leap-year date clamps correctly");Check((DateTime)parse.Invoke(null,new object[]{"31 02 2025"})==new DateTime(2025,2,28),"non-leap date clamps correctly");
  var profile=new ReportUserPreferences{Zoom=130,FavoritesOnly=true};profile.Favorites.Add("report.one");var writer=new StringWriter();var serializer=new XmlSerializer(typeof(ReportUserPreferences));serializer.Serialize(writer,profile);var restored=(ReportUserPreferences)serializer.Deserialize(new StringReader(writer.ToString()));Check(restored.Zoom==130&&restored.FavoritesOnly&&restored.Favorites.Single()=="report.one","user preferences serialize independently of database");
  var sampleReport=new OperationalReportViewModel("Daily sales",null,null,null,null);
  Check(!sampleReport.PreviewCommand.CanExecute(null)&&!sampleReport.ExportCsvCommand.CanExecute(null),"preview and CSV disabled before a document exists");
  sampleReport.Document=doc;Check(sampleReport.PreviewCommand.CanExecute(null)&&sampleReport.ExportCsvCommand.CanExecute(null),"preview and CSV enabled for a generated report");
  var cloned=(FlowDocument)System.Windows.Markup.XamlReader.Parse(System.Windows.Markup.XamlWriter.Save(doc));Check(!object.ReferenceEquals(cloned,doc)&&ReportCsv.Rows(cloned.Blocks).SequenceEqual(ReportCsv.Rows(doc.Blocks)),"page preview clones the report without changing its data");
  var center=new BasicReportViewModel();typeof(ReportContext).GetField("_reports",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,new List<ReportViewModelBase>{sampleReport,new OperationalReportViewModel("Payment methods",null,null,null,null)});
  center.SearchText="payment";Check(center.Reports.Count()==1&&center.Reports.Single().Header=="Payment methods","report search filters available reports");
  var pathMethod=typeof(BasicReportViewModel).GetMethod("ProfilePath",BindingFlags.NonPublic|BindingFlags.Instance);Field(center,"_profileUserId",1);var path1=(string)pathMethod.Invoke(center,null);Field(center,"_profileUserId",2);var path2=(string)pathMethod.Invoke(center,null);Check(path1!=path2,"different POS users have separate preference files");Field(center,"_profileUserId",-1);
  var zero=new AccountScreenRow("Cash",0,0,1,"",1,null);Check(zero.DisplayBalance==0m.ToString("N2"),"zero balance is displayed as a numeric value");
  AccountScreen requested=null;var original=new AccountScreen{Filter=2};
  var service=(IAccountService)new TestProxy(typeof(IAccountService),call=>{if(call.MethodName=="GetAccountScreenRows"){requested=(AccountScreen)call.Args[0];return new[]{new AccountScreenRow("Cash",12,12,5,"",7,null)};}throw new Exception("Unexpected account operation: "+call.MethodName);}).GetTransparentProxy();
  var state=(IApplicationState)new TestProxy(typeof(IApplicationState),call=>call.MethodName=="get_CurrentWorkPeriod"?new WorkPeriod():null).GetTransparentProxy();
  var vm=(AccountSelectorViewModel)FormatterServices.GetUninitializedObject(typeof(AccountSelectorViewModel));Field(vm,"_accountService",service);Field(vm,"_applicationState",state);Field(vm,"_selectedAccountScreen",original);Field(vm,"_accounts",new ObservableCollection<AccountScreenRow>());
  vm.SelectedAccount=new AccountScreenRow("Cash total",12,12,0,"",7,null);
  typeof(AccountSelectorViewModel).GetMethod("OnShowAccountDetails",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(vm,new object[]{null});
  Check(requested!=null&&requested!=original&&requested.Filter==2&&requested.AccountScreenValues.Single().DisplayDetails&&requested.AccountScreenValues.Single().AccountTypeId==7,"account-type drill-down retains date scope and selects correct account type");
  Check(original.AccountScreenValues.Count==0&&vm.Accounts.Single().AccountId==5,"account drill-down leaves persistent screen settings unchanged");
 }
}
