using System;using System.Diagnostics;using System.IO;using System.Threading;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;
namespace TrayLeaf {
 public sealed partial class Controller {
  Task updateCheck;string downloadedUpdateFile,downloadedUpdateDigest;
  internal Func<CancellationToken,Task<string>> updateFetcher=token=>UpdateService.Latest(token);
  internal Func<CancellationToken,Task<ReleaseInfo>> releaseFetcher=token=>UpdateService.GetRelease(token);
  internal Func<ReleaseInfo,string,Action<int>,CancellationToken,Task<string>> updateDownloader=(r,d,p,t)=>UpdateService.Download(r,d,p,t);
  internal Func<bool> confirmUpdate=null;internal Action<string> launchUpdate=null;internal Action<string> reportUpdateError=null;
  Task CheckForUpdates(bool automatic){if(updateDisposed)return Task.FromResult(0);if(updateCheck!=null&&!updateCheck.IsCompleted)return updateCheck;updateCheck=CheckForUpdatesCore(automatic);return updateCheck;}
  async Task CheckForUpdatesCore(bool automatic){
   if(automatic&&Get<CheckBox>("AutoCheckUpdateInput").IsChecked!=true)return;
   var button=Get<Button>("CheckUpdateButton");button.IsEnabled=false;
   var stop=new CancellationTokenSource();if(automatic)automaticUpdate=stop;
   try{UpdateText("正在检查更新…");string tag=await updateFetcher(stop.Token);if(updateDisposed||stop.IsCancellationRequested)return;
    PresentUpdate(tag);UpdateText(UpdateService.IsNewer(tag,AppIdentity.ReleaseVersion)?"发现新版本 "+tag+"，可点击下载最新版":"当前已是最新版本 v"+AppIdentity.ReleaseVersion);
   }catch(OperationCanceledException){if(!updateDisposed&&!automatic)UpdateText("检查已取消");}
   catch(Exception ex){if(!updateDisposed)UpdateText("检查失败："+ex.Message);}
   finally{if(automaticUpdate==stop)automaticUpdate=null;stop.Dispose();if(!updateDisposed)button.IsEnabled=true;}
  }
  void UpdateText(string message){Text("UpdateStatus",message);Get<TextBlock>("UpdateStatus").Visibility=Visibility.Visible;Get<Button>("DownloadLatestButton").ToolTip=UiLanguage.Text(message);}
  void DownloadCaption(string caption){if(updateDisposed)return;Get<Button>("DownloadLatestButton").Content=UiLanguage.Text(caption);if(downloadButton!=null)downloadButton.Content=UiLanguage.Text(caption);}
  async Task DownloadLatestUpdate(){
   if(updateDownload!=null){updateDownload.Cancel();return;}
   updateDownload=new CancellationTokenSource();var token=updateDownload.Token;DownloadCaption("取消下载");
   try{
    if(busy)throw new IOException("请先等待关闭或恢复操作完成");
    UpdateText("正在读取安装包信息…");var release=await releaseFetcher(token);token.ThrowIfCancellationRequested();
    if(!UpdateService.IsNewer(release.Tag,AppIdentity.ReleaseVersion)){PresentUpdate(release.Tag);throw new IOException("当前已是最新版本 v"+AppIdentity.ReleaseVersion);}
    availableRelease=release;PresentUpdate(release.Tag);
    string file=downloadedUpdateFile;
    if(String.IsNullOrEmpty(file)||!File.Exists(file)||downloadedUpdateDigest!=release.Digest){
     string directory=Path.Combine(Path.GetDirectoryName(db.FilePath),"updates");
     file=await updateDownloader(release,directory,percent=>w.Dispatcher.BeginInvoke(new Action(delegate{if(updateDisposed||updateDownload==null||token.IsCancellationRequested)return;UpdateText("正在下载安装包 "+percent+"%");DownloadCaption("取消下载 · "+percent+"%");})),token);
    }
    token.ThrowIfCancellationRequested();
    if(!await Task.Run(()=>UpdateService.VerifyDigest(file,release.Digest))){downloadedUpdateFile=null;downloadedUpdateDigest=null;throw new IOException("安装前复核 SHA-256 失败，已阻止安装");}
    downloadedUpdateFile=file;downloadedUpdateDigest=release.Digest;UpdateText("安装包已下载并通过 SHA-256 校验");
    if(updateDisposed)return;
    bool accepted;if(confirmUpdate!=null)accepted=confirmUpdate();else{Show();accepted=ModernDialog.Confirm(w,"是否更新到最新版本","",true);}
    if(!accepted){UpdateText("已取消安装，安装包已保留，可稍后点击安装最新版");return;}
    token.ThrowIfCancellationRequested();
    if(!await Task.Run(()=>UpdateService.VerifyDigest(file,release.Digest)))throw new IOException("安装包已发生变化，已阻止安装");
    if(busy)throw new IOException("请先等待关闭或恢复操作完成后再安装更新");
    if(liveTimer!=null&&liveTimer.IsEnabled)SaveLiveSettings();
    if(launchUpdate!=null)launchUpdate(file);else{
     var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe"),"/i "+StartupManager.Quote(file)){UseShellExecute=true};
     using(var process=Process.Start(start)){if(process==null)throw new IOException("Windows Installer 未能启动");}Exit();
    }
   }catch(OperationCanceledException){if(!updateDisposed)UpdateText("下载已取消");}
   catch(Exception ex){if(!updateDisposed){string message="更新失败："+ex.Message;UpdateText(message);if(reportUpdateError!=null)reportUpdateError(message);else{Show();ModernDialog.Confirm(w,message,"",false);}}}
   finally{updateDownload.Dispose();updateDownload=null;DownloadCaption(String.IsNullOrEmpty(downloadedUpdateFile)?"下载最新版":"安装最新版");}
  }
 }
}
