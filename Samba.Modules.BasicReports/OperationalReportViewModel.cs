using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Documents;
using Samba.Domain.Models.Tickets;
using Samba.Presentation.Services;
using Samba.Services;
using Samba.Services.Common;
namespace Samba.Modules.BasicReports
{
 public class OperationalReportViewModel : ReportViewModelBase
 {
  private readonly string _mode;
  public static readonly string[] Modes={"Daily sales","Hourly sales","Payment methods","Ticket register","Ticket status","Department sales","Tax summary","Adjustments","Last modified by user"};
  public OperationalReportViewModel(string mode,IUserService users,IApplicationState state,ILogService log,ISettingService settings):base(users,state,log,settings){_mode=mode;}
  public override string ReportKey {get{return "EboPOS.Operational."+_mode;}}
  protected override string GetHeader(){return _mode;}
  protected override void CreateFilterGroups(){FilterGroups.Clear();FilterGroups.Add(CreateWorkPeriodFilterGroup());}
  protected override FlowDocument GetReport(){
   var report=new SimpleReport("18cm");report.Document.PageWidth=720;AddDefaultReportHeader(report,ReportContext.CurrentWorkPeriod,_mode);
   report.AddHeader("Scope: tickets selected by last-payment date. Daily/hourly grouping uses ticket creation time. Open tickets included; gross payments before change/refunds.");
   var data=BuildRows(ReportContext.Tickets,_mode);
   if(data.Count>0){report.AddColumnLength("data",Enumerable.Repeat("*",data[0].Length).ToArray());report.AddTable("data",data[0]);foreach(var row in data.Skip(1))report.AddRow("data",row);}
   if(data.Count==1)report.AddHeader("No matching records.");
   return report.Document;
  }
  private static string Money(decimal amount){return amount.ToString("0.00",CultureInfo.InvariantCulture);}
  public static List<string[]> BuildRows(IEnumerable<Ticket> source,string mode){
   var tickets=source.ToList();var rows=new List<string[]>();
   if(mode=="Payment methods"){
    rows.Add(new[]{"Method","Payments","Gross received"});foreach(var g in tickets.SelectMany(t=>t.Payments).GroupBy(p=>p.Name??"Unnamed").OrderBy(g=>g.Key))rows.Add(new[]{g.Key,g.Count().ToString(),Money(g.Sum(p=>p.Amount))});
   }else if(mode=="Ticket register"){
    rows.Add(new[]{"Ticket","Date","Status","Stored total","Remaining"});foreach(var t in tickets.OrderBy(t=>t.Date))rows.Add(new[]{t.TicketNumber??t.Id.ToString(),t.Date.ToString("yyyy-MM-dd HH:mm"),t.IsClosed?"Closed":"Open",Money(t.TotalAmount),Money(t.RemainingAmount)});
   }else if(mode=="Tax summary"){
    rows.Add(new[]{"Ticket type ID","Tickets","Calculated tax"});foreach(var g in tickets.GroupBy(t=>t.TicketTypeId).OrderBy(g=>g.Key))rows.Add(new[]{g.Key.ToString(),g.Count().ToString(),Money(g.Sum(t=>t.CalculateTax(t.GetPlainSum(),t.GetPreTaxServicesTotal())))});
   }else if(mode=="Adjustments"){
    rows.Add(new[]{"Ticket","Pre-tax adjustments (signed)","Post-tax adjustments (signed)"});foreach(var t in tickets.OrderBy(t=>t.Date))rows.Add(new[]{t.TicketNumber??t.Id.ToString(),Money(t.GetPreTaxServicesTotal()),Money(t.GetPostTaxServicesTotal())});
   }else{
    rows.Add(new[]{mode,"Tickets","Stored ticket totals","Outstanding"});
    Func<Ticket,string> key=t=>t.Date.ToString("yyyy-MM-dd");
    if(mode=="Hourly sales")key=t=>t.Date.Hour.ToString("00")+":00";
    if(mode=="Ticket status")key=t=>t.IsClosed?"Closed":"Open";
    if(mode=="Department sales")key=t=>"Department ID "+t.DepartmentId;
    if(mode=="Last modified by user")key=t=>t.LastModifiedUserName??"Unknown";
    foreach(var g in tickets.GroupBy(key).OrderBy(g=>g.Key))rows.Add(new[]{g.Key,g.Count().ToString(),Money(g.Sum(t=>t.TotalAmount)),Money(g.Sum(t=>t.RemainingAmount))});
   }
   return rows;
  }
 }

 public static class ReportCsv
 {
  public static string Escape(string value){
   value=value??"";
   // Prevent spreadsheet programs from treating user-provided labels as formulas.
   if(value.Length>0 && "=+@-\t\r".IndexOf(value[0])>=0)value="'"+value;
   return "\""+value.Replace("\"","\"\"")+"\"";
  }
  public static IEnumerable<string> Rows(BlockCollection blocks){
   foreach(Block block in blocks){
    var table=block as Table;
    if(table!=null){foreach(var group in table.RowGroups)foreach(var row in group.Rows)yield return string.Join(",",row.Cells.Select(c=>Escape(new TextRange(c.ContentStart,c.ContentEnd).Text.TrimEnd('\r','\n'))));}
    else if(block is Section){foreach(var row in Rows(((Section)block).Blocks))yield return row;}
   }
  }
 }
}
