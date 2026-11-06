using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Events;
using Samba.Domain.Models.Users;
using Samba.Presentation.Common;
using Samba.Presentation.Services.Common;
namespace Samba.Modules.BasicReports
{
 public class ReportUserPreferences {
  public List<string> Favorites {get;set;}
  public bool FavoritesOnly {get;set;}
  public double Zoom {get;set;}
  public ReportUserPreferences(){Favorites=new List<string>();Zoom=100;}
 }
 [Export] public class BasicReportViewModel:ObservableObject {
  private ReportUserPreferences _preferences=new ReportUserPreferences();
  private string _searchText=""; private int _profileUserId=-1;
  public string PreferenceStatus {get;private set;}
  public IEnumerable<ReportViewModelBase> Reports {get{return ReportContext.Reports.Where(r=>(string.IsNullOrWhiteSpace(SearchText)||r.Header.IndexOf(SearchText,StringComparison.CurrentCultureIgnoreCase)>=0)&&(!FavoritesOnly||_preferences.Favorites.Contains(r.ReportKey))).OrderByDescending(r=>_preferences.Favorites.Contains(r.ReportKey));}}
  public string SearchText {get{return _searchText;}set{_searchText=value??"";RaisePropertyChanged(()=>Reports);}}
  public bool FavoritesOnly {get{return _preferences.FavoritesOnly;}set{_preferences.FavoritesOnly=value;RaisePropertyChanged(()=>FavoritesOnly);RaisePropertyChanged(()=>Reports);SavePreferences();}}
  public double ReportZoom {get{return _preferences.Zoom;}set{_preferences.Zoom=Math.Max(60,Math.Min(180,value));RaisePropertyChanged(()=>ReportZoom);}}
  public DelegateCommand<ReportViewModelBase> ReportExecuteCommand {get;set;}
  public DelegateCommand<string> FavoriteCommand {get;set;}
  public DelegateCommand<string> SavePreferencesCommand {get;set;}
  public string FavoriteLabel {get{return ActiveReport!=null&&_preferences.Favorites.Contains(ActiveReport.ReportKey)?"Remove favorite":"Add favorite";}}
  private ReportViewModelBase _activeReport;
  public ReportViewModelBase ActiveReport {get{return _activeReport;}set{_activeReport=value;RaisePropertyChanged(()=>ActiveReport);RaisePropertyChanged(()=>IsReportVisible);RaisePropertyChanged(()=>FavoriteLabel);FavoriteCommand.RaiseCanExecuteChanged();}}
  public bool IsReportVisible {get{return ActiveReport!=null;}}
  public BasicReportViewModel(){
   ReportExecuteCommand=new DelegateCommand<ReportViewModelBase>(OnExecuteReport);
   FavoriteCommand=new DelegateCommand<string>(s=>{string key=ActiveReport.ReportKey;if(_preferences.Favorites.Contains(key))_preferences.Favorites.Remove(key);else _preferences.Favorites.Add(key);RaisePropertyChanged(()=>Reports);RaisePropertyChanged(()=>FavoriteLabel);SavePreferences();},s=>ActiveReport!=null);
   SavePreferencesCommand=new DelegateCommand<string>(s=>SavePreferences());
   EventServiceFactory.EventService.GetEvent<GenericEvent<User>>().Subscribe(x=>{if(x.Topic==EventTopicNames.UserLoggedOut){ClearReport();_preferences=new ReportUserPreferences();_searchText="";_profileUserId=-1;}});
   EventServiceFactory.EventService.GetEvent<GenericEvent<EventAggregator>>().Subscribe(x=>{if(x.Topic==EventTopicNames.ActivateNavigation)ClearReport();});
  }
  private void ClearReport(){if(ActiveReport!=null)ActiveReport.Document=null;ActiveReport=null;}
  public void LoadUserPreferences(){
   var user=ReportContext.ApplicationState.CurrentLoggedInUser;int id=user==null?0:user.Id;if(_profileUserId==id)return;
   ClearReport();_profileUserId=id;_preferences=new ReportUserPreferences();_searchText="";
   try{string path=ProfilePath();if(id>0&&File.Exists(path))using(var reader=File.OpenRead(path))_preferences=(ReportUserPreferences)new XmlSerializer(typeof(ReportUserPreferences)).Deserialize(reader);if(_preferences.Favorites==null)_preferences.Favorites=new List<string>();if(double.IsNaN(_preferences.Zoom)||double.IsInfinity(_preferences.Zoom))_preferences.Zoom=100;_preferences.Zoom=Math.Max(60,Math.Min(180,_preferences.Zoom));PreferenceStatus="Favorites and zoom are saved separately for each POS user.";}
   catch(Exception e){if(!(e is IOException)&&!(e is UnauthorizedAccessException)&&!(e is InvalidOperationException))throw;_preferences=new ReportUserPreferences();PreferenceStatus="Preferences unavailable; defaults are in use.";}
   RaisePropertyChanged(()=>SearchText);RaisePropertyChanged(()=>Reports);RaisePropertyChanged(()=>FavoritesOnly);RaisePropertyChanged(()=>ReportZoom);RaisePropertyChanged(()=>PreferenceStatus);
  }
  private string ProfilePath(){return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EboPOS","ReportPreferences","user-"+_profileUserId+".xml");}
  private void SavePreferences(){if(_profileUserId<=0)return;try{string path=ProfilePath();Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";using(var writer=File.Create(temp))new XmlSerializer(typeof(ReportUserPreferences)).Serialize(writer,_preferences);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);PreferenceStatus="Your report preferences are saved.";}catch(Exception e){if(!(e is IOException)&&!(e is UnauthorizedAccessException)&&!(e is InvalidOperationException))throw;PreferenceStatus="Could not save preferences; check access to your profile folder.";}RaisePropertyChanged(()=>PreferenceStatus);}
  private void OnExecuteReport(ReportViewModelBase report){if(report==null)return;foreach(var item in ReportContext.Reports)item.Selected=false;report.Selected=true;ActiveReport=report;report.RefreshReport();}
 }
}
