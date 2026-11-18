using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace TrayLeaf {
 public sealed partial class Controller {
  CancellationTokenSource automaticUpdate;bool updateDisposed;
  void Setup097(){
   Get<ComboBox>("WindowTintInput").SelectedIndex=(int)Number("windowTint",0,0,ThemeManager.Accents.Length-1);Get<ComboBox>("WindowTintInput").SelectionChanged+=delegate{PreviewAppearance();};
   Get<CheckBox>("AutoCheckUpdateInput").IsChecked=Setting("autoCheckUpdates",true);Get<CheckBox>("AutoCheckUpdateInput").Unchecked+=delegate{if(automaticUpdate!=null)automaticUpdate.Cancel();};Get<CheckBox>("AutoCheckUpdateInput").Checked+=delegate{if(!initializing)CheckAutoUpdates();};
   Click("ChangeDataButton",ChangeDataDirectory);Click("ManageCustomButton",ManageCustom);Click("NewVersionBadge",delegate{Navigate(3);if(releaseUrl!=null){Text("UpdateStatus",UiLanguage.Format("发现新版本 {0}，可点击下载更新",Convert.ToString(Get<Button>("NewVersionBadge").Tag)));Get<TextBlock>("UpdateStatus").Visibility=Visibility.Visible;Get<Button>("OpenReleaseButton").Visibility=Visibility.Visible;}});
   ScrollActivity.Attach(Get<ScrollViewer>("SettingsPanel"));ScrollActivity.Attach(Get<DataGrid>("ProcessGrid"));ScrollActivity.Attach(Get<DataGrid>("RestoreGrid"));
   w.Closed+=delegate{updateDisposed=true;if(automaticUpdate!=null)automaticUpdate.Cancel();};UpdateCustomCount();PreviewAppearance();
  }
  void UpdateCustomCount(){Text("CustomCountText","已添加 "+db.CustomPrograms().Count+" 个自定义程序；移除只清除手动管理记录，不会关闭或卸载程序");}
  void ManageCustom(){if(busy)return;var rows=db.CustomPrograms();if(rows.Count==0){Status("没有自定义添加的程序");return;}var dialog=new ProgramSelectionDialog(w,rows,false,true);if(dialog.ShowDialog()!=true)return;db.RemoveCustomPrograms(dialog.Chosen);UpdateCustomCount();Refresh();Status("已移除 "+dialog.Chosen.Count+" 个自定义程序；历史与配置仍保留");}
  void HideRows(IEnumerable<AppRow> selected){if(busy)return;var rows=selected.ToList();if(rows.Count==0){Status("请先勾选要隐藏的程序");return;}if(!AskOptional("将隐藏 "+rows.Count+" 个程序\n\n"+String.Join("\n",rows.Take(12).Select(x=>"• "+x.Name))+"\n\n隐藏只影响程序列表，不会关闭或卸载应用，可在“设置 → 程序管理”恢复显示","隐藏所选程序","confirmHide","ConfirmHideInput"))return;db.HidePrograms(rows);foreach(var row in rows)row.Selected=false;current=db.FilterVisible(current);Get<DataGrid>("ProcessGrid").ItemsSource=current;ApplyFilter();UpdateHiddenCount();Status("已隐藏 "+rows.Count+" 个程序；未关闭或卸载应用；可在“设置 → 程序管理”恢复显示");}
  async void ChangeDataDirectory(){if(busy)return;int visit=pageVisit;try{string directory=NativeFolderPicker.Choose(w,Path.GetDirectoryName(db.FilePath));if(String.IsNullOrEmpty(directory))return;if(String.Equals(Path.Combine(Path.GetFullPath(directory),"trayleaf.db"),db.FilePath,StringComparison.OrdinalIgnoreCase)){Status("当前已使用该数据目录");return;}if(!ModernDialog.Confirm(w,"将把现有历史、配置及设置迁移到：\n"+directory+"\n\n原数据库保留，所选目录不能已有数据库","更改数据目录",false))return;
   string marker=demo||Environment.GetCommandLineArgs().Contains("--db")?Path.Combine(Path.GetDirectoryName(db.FilePath),"data-location.preview.txt"):DataLocation.Marker;SetBusy(true);Get<Button>("ChangeDataButton").IsEnabled=false;Get<Button>("ManageCustomButton").IsEnabled=false;
   try{await Task.Run(()=>db.Relocate(directory,marker));Text("DbPathText",db.FilePath);VisitStatus(visit,"数据目录已更改，历史、配置和设置已迁移；原数据库保留");}finally{SetBusy(false);Get<Button>("ChangeDataButton").IsEnabled=true;Get<Button>("ManageCustomButton").IsEnabled=true;}
  }catch(Exception ex){Error(ex);}}
  void CheckAutoUpdates(){if(demo||!UpdatePolicy.ShouldCheck(db,DateTime.Now))return;
   // Start is called before Application.Run; dispatch after WPF has installed its UI context.
   w.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(async delegate{if(!updateDisposed&&Get<CheckBox>("AutoCheckUpdateInput").IsChecked==true)await CheckForUpdates(true);}));
  }
  void PresentUpdate(string tag){
   bool newer=UpdateService.IsNewer(tag,AppIdentity.ReleaseVersion);
   releaseUrl=newer?UpdateService.ProjectUrl+"/releases/tag/"+Uri.EscapeDataString(tag):null;
   Get<Button>("NewVersionBadge").Visibility=newer?Visibility.Visible:Visibility.Collapsed;
   var badge=Get<Button>("NewVersionBadge");string version="v"+(tag??"").TrimStart('v','V');badge.Tag=version;
   badge.Content=UiLanguage.Format("发现新版本 {0}",version);badge.ToolTip=badge.Content;
   if(updateDownload==null)DownloadCaption(newer?(String.IsNullOrEmpty(downloadedUpdateFile)?"下载更新":"安装更新"):"检查更新");
   Get<Button>("OpenReleaseButton").Visibility=newer?Visibility.Visible:Visibility.Collapsed;
  }
 }
}
