using System;using System.Diagnostics;using System.IO;using System.Threading;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
namespace TrayLeaf {
 public sealed partial class Controller {
  CancellationTokenSource updateDownload;ReleaseInfo availableRelease;Grid aboutHost;FlowDocumentScrollViewer changelogView;TextBlock changelogStatus;bool changelogLoading,changelogLoaded;TimeSpan? changelogLastAttempt;readonly Stopwatch changelogWatch=Stopwatch.StartNew();internal Func<TimeSpan> changelogClock=null;
  internal Func<string,CancellationToken,Task<string>> changelogFetcher=(url,token)=>UpdateService.GetChangelog(url,token);
  void SetupAbout100(){var about=Get<ScrollViewer>("AboutPanel");var parent=(Grid)about.Parent;int index=parent.Children.IndexOf(about);parent.Children.Remove(about);aboutHost=FeatureUi.Parse("<Grid.ColumnDefinitions><ColumnDefinition Width='154'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions><StackPanel x:Name='Side' Margin='0,0,20,0'/>");Grid.SetRow(aboutHost,1);aboutHost.Visibility=Visibility.Collapsed;parent.Children.Insert(index,aboutHost);Grid.SetColumn(about,1);aboutHost.Children.Add(about);
   var content=new Grid();content.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});content.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});var bar=new DockPanel{Margin=new Thickness(0,0,0,12)};changelogStatus=FeatureUi.Label("更新内容",true);var retry=FeatureUi.Button("刷新",async delegate{await LoadChangelog();},false);DockPanel.SetDock(retry,Dock.Right);bar.Children.Add(retry);bar.Children.Add(changelogStatus);content.Children.Add(bar);changelogView=new FlowDocumentScrollViewer{IsToolBarVisible=false,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Background=Brushes.Transparent};Grid.SetRow(changelogView,1);content.Children.Add(changelogView);Grid.SetColumn(content,1);content.Visibility=Visibility.Collapsed;aboutHost.Children.Add(content);ScrollActivity.Attach(changelogView);
   var side=(StackPanel)aboutHost.FindName("Side");Button aboutTab=null,logTab=null;Action<bool> select=async showLog=>{about.Visibility=showLog?Visibility.Collapsed:Visibility.Visible;content.Visibility=showLog?Visibility.Visible:Visibility.Collapsed;foreach(var item in new[]{aboutTab,logTab}){item.SetResourceReference(Control.ForegroundProperty,(item==logTab)==showLog?"Accent":"Muted");item.SetResourceReference(Control.BorderBrushProperty,(item==logTab)==showLog?"Accent":"TransparentNavigationBorder");}if(showLog&&!changelogLoaded&&!changelogLoading)await LoadChangelog();};aboutTab=FeatureUi.Button("关于",()=>select(false),false);logTab=FeatureUi.Button("更新内容",()=>select(true),false);foreach(var b in new[]{aboutTab,logTab}){b.SetResourceReference(FrameworkElement.StyleProperty,"Nav");b.Margin=new Thickness(0,0,0,4);side.Children.Add(b);}select(false);
   RefreshChangelogOnStart();
   downloadButton=FeatureUi.Button("下载最新版",DownloadUpdate,true);downloadButton.Visibility=Visibility.Collapsed;var actions=(WrapPanel)Get<Button>("CheckUpdateButton").Parent;actions.Children.Add(downloadButton);Click("DownloadLatestButton",DownloadUpdate);
  }
  void RefreshChangelogOnStart(){if(demo)return;w.Dispatcher.BeginInvoke(new Action(async delegate{await LoadChangelog();}));}
  async Task LoadChangelog(){
   TimeSpan now=changelogClock!=null?changelogClock():changelogWatch.Elapsed;
   if(changelogLoading)return;
   if(changelogLastAttempt.HasValue&&now-changelogLastAttempt.Value<TimeSpan.FromSeconds(10)){changelogStatus.Text=UiLanguage.Text("刷新间隔为 10 秒，请稍后重试");return;}
   changelogLastAttempt=now;changelogLoading=true;changelogStatus.Text=UiLanguage.Text("正在获取更新内容…");
   try{string text=await changelogFetcher("https://raw.githubusercontent.com/yunnikuu/TrayLeaf/main/CHANGELOG.md?session="+Guid.NewGuid().ToString("N"),CancellationToken.None);changelogView.Document=MarkdownView.Render(text,w);changelogLoaded=true;changelogStatus.Text=UiLanguage.Text("更新内容 · GitHub CHANGELOG.md");}
   catch(Exception ex){changelogStatus.Text=UiLanguage.Text("获取失败：")+ex.Message;}
   finally{changelogLoading=false;}
  }

  async void DownloadUpdate(){await DownloadLatestUpdate();}
 }
}
