using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TrayLeaf;
class Version082Tests {
 static int passed,failed;static string dir;
 static void Check(string name,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(ok)passed++;else failed++;}
 static T Get<T>(Window w,string name)where T:class{return w.FindName(name)as T;}
 static object Call(Controller c,string name,params object[] args){return typeof(Controller).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);}
 static void Idle(Window w){w.UpdateLayout();w.Dispatcher.Invoke(new Action(delegate{}),DispatcherPriority.ApplicationIdle);w.UpdateLayout();}
 static IEnumerable<T> Find<T>(DependencyObject root)where T:DependencyObject{if(root is T)yield return (T)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var n in Find<T>(VisualTreeHelper.GetChild(root,i)))yield return n;}
 static bool Within(DependencyObject child,DependencyObject parent){while(child!=null){if(child==parent)return true;child=VisualTreeHelper.GetParent(child);}return false;}
 static void Wait(Task t){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};timer.Tick+=delegate{if(t.IsCompleted){timer.Stop();frame.Continue=false;}};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();t.GetAwaiter().GetResult();}
 static void Render(Window w,string name){Idle(w);var root=(FrameworkElement)w.Content;var b=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);b.Render(root);var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(Path.Combine(dir,name+".png")))e.Save(f);}
 static void Refresh(OperationService ops,IEnumerable<AppRow> rows,params string[] running){typeof(OperationService).GetMethod("RefreshStatuses").Invoke(ops,new object[]{rows,new HashSet<string>(running,StringComparer.OrdinalIgnoreCase)});}
 static Task Monitor(OperationService ops,IEnumerable<AppRow> rows,Func<HashSet<string>> read,Func<int,Task> delay){var t=typeof(OperationService).Assembly.GetType("TrayLeaf.LaunchMonitor");object m=Activator.CreateInstance(t,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{read,delay},null);return (Task)t.GetMethod("VerifyAsync").Invoke(m,new object[]{rows,ops});}
 static void States(Database db){var ops=new OperationService(db);bool supported=typeof(OperationService).GetMethod("RefreshStatuses")!=null;Check("runtime-refresh-service",supported);if(!supported)return;
  var rows=new List<AppRow>{new AppRow{Name="先前关闭失败",Path=@"C:\Apps\a.exe",Args="--saved 世界",Admin=true,Selected=true},new AppRow{Name="旧请求",Path=@"C:\Apps\b.exe",Selected=false},new AppRow{Name="仍运行",Path=@"C:\Apps\c.exe",Selected=true}};
  long batch=db.Batch(rows);rows=ops.History(batch);ops.State(rows[0],"closefailed","拒绝访问");ops.State(rows[1],"launched","");ops.State(rows[2],"closed","");
  int notify=0;rows[0].PropertyChanged+=(s,e)=>{if(e.PropertyName=="CanRestore")notify++;};
  Refresh(ops,rows,@"c:\apps\C.exe");Check("failed-close-absent-can-retry",rows[0].CanRestore);Check("old-launch-request-absent-can-retry",rows[1].CanRestore);Check("running-path-case-insensitive-disabled",!rows[2].CanRestore&&!rows[2].Selected);Check("refresh-retains-user-args-and-admin",rows[0].Args=="--saved 世界"&&rows[0].Admin);Check("refresh-retains-stopped-selection",rows[0].Selected);Check("refresh-does-not-auto-select",!rows[1].Selected);Check("refresh-notifies-checkbox",notify>0);
  Refresh(ops,rows);Check("running-exits-can-select",rows[2].CanRestore);rows[2].Selected=true;Check("retry-selection-can-be-saved",rows[2].Selected);
  var configured=new AppRow{Name="配置",Path=@"C:\Apps\config.exe",ItemStatus="configured",Selected=true};Refresh(ops,new[]{configured});Check("refresh-keeps-config-unpersisted-and-selected",configured.ItemId==0&&configured.Selected&&configured.CanRestore);
  ops.State(rows[0],"launched","");rows[0].State="已发送启动请求";ops.State(rows[1],"launched","");rows[1].State="已发送启动请求";
  int polls=0;var waits=new List<int>();Wait(Monitor(ops,rows.Take(2),delegate{polls++;return new HashSet<string>(polls>=2?new[]{rows[0].Path.ToUpperInvariant()}:new string[0],StringComparer.OrdinalIgnoreCase);},ms=>{waits.Add(ms);return Task.FromResult(0);}));
  Check("five-shared-checks",polls==5&&waits.Count==5);Check("production-delay-every-3000ms",waits.All(ms=>ms==3000));Check("success-detected-on-second-check",rows[0].ItemStatus=="started"&&rows[0].State=="启动成功"&&!rows[0].CanRestore);Check("failure-after-fifth-check",rows[1].ItemStatus=="launchfailed"&&rows[1].State=="启动失败"&&rows[1].CanRestore);Check("failure-has-explanation",!String.IsNullOrWhiteSpace(rows[1].Error));Check("verification-persists-state",ops.History(batch)[0].ItemStatus=="started"&&ops.History(batch)[1].ItemStatus=="launchfailed");
  ops.State(rows[0],"launched","");polls=0;Wait(Monitor(ops,new[]{rows[0]},()=>{polls++;return new HashSet<string>(new[]{rows[0].Path},StringComparer.OrdinalIgnoreCase);},ms=>Task.FromResult(0)));Check("stop-polling-after-success",polls==1);
  ops.State(rows[0],"launched","");polls=0;Wait(Monitor(ops,new[]{rows[0]},delegate{polls++;throw new IOException("snapshot temporarily failed");},ms=>Task.FromResult(0)));Check("snapshot-failure-bounded-and-retryable",polls==5&&rows[0].ItemStatus=="launchfailed"&&rows[0].CanRestore);
  Refresh(ops,new[]{rows[0]},rows[0].Path);Check("late-start-recognized-on-refresh",!rows[0].CanRestore&&rows[0].State=="已在运行");
 }
 static void Live(Controller c,Window w,Database db){
  System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(w.Dispatcher));
  string fixture=Path.Combine(dir,"TrayLeaf.ControlledFixture.exe"),log=Path.Combine(dir,"launched-fixture.txt");if(File.Exists(log))File.Delete(log);
  var ops=new OperationService(db);var row=new AppRow{Name="恢复验证专用程序",Path=fixture,Args="--tray-only --log \""+log+"\" --marker \"saved 世界\"",Admin=false,Selected=true,ItemStatus="configured"};
  typeof(Controller).GetField("history",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,new List<AppRow>{row});Get<DataGrid>(w,"RestoreGrid").ItemsSource=new[]{row};
  int pid=0;try{
   var watch=Stopwatch.StartNew();Wait((Task)Call(c,"RestoreCore"));watch.Stop();
   if(File.Exists(log))pid=Int32.Parse(File.ReadAllLines(log)[0]);
   Check("controller-real-launch-and-verification",row.ItemStatus=="started"&&row.State=="启动成功"&&pid>0);Check("controller-waits-first-three-seconds",watch.ElapsedMilliseconds>=2900&&watch.ElapsedMilliseconds<10000);Console.WriteLine("OBSERVED live-start elapsedMs="+watch.ElapsedMilliseconds+" state="+row.State);
   Check("full-path-query-detects-live-process",ProcessService.RunningPaths().Contains(fixture));Check("live-launch-args-preserved",File.Exists(log)&&File.ReadAllText(log).Contains("saved 世界"));Check("live-success-footer",Get<TextBlock>(w,"StatusText").Text.Contains("启动成功 1 个"));
   Wait((Task)Call(c,"RefreshRestoreCore"));Check("live-refresh-running-not-selectable",!row.CanRestore&&!row.Selected);
   Render(w,"restore-running-dark");
   if(pid>0)using(var child=Process.GetProcessById(pid)){if(!child.HasExited&&String.Equals(Native.ProcessPath(pid),fixture,StringComparison.OrdinalIgnoreCase)){child.Kill();child.WaitForExit(4000);pid=0;}}
   Wait((Task)Call(c,"RefreshRestoreCore"));Check("live-refresh-exited-selectable",row.CanRestore&&row.State.Contains("未运行"));
   // Render the former disabled/selected failure case with the actual checkbox binding.
   row.Selected=true;Idle(w);var checks=Find<CheckBox>(Get<DataGrid>(w,"RestoreGrid")).Where(x=>Object.ReferenceEquals(x.DataContext,row)).ToList();Check("refreshed-checkbox-enabled-and-ticked",checks.Any(x=>x.IsEnabled&&x.IsChecked==true));Render(w,"restore-stopped-dark");
  }finally{if(pid>0)try{using(var child=Process.GetProcessById(pid)){if(!child.HasExited&&String.Equals(Native.ProcessPath(pid),fixture,StringComparison.OrdinalIgnoreCase)){child.Kill();child.WaitForExit(4000);}}}catch(ArgumentException){}}
  var absent=new AppRow{Name="未出现的测试程序",Path=Path.Combine(dir,"never-launched.exe"),Selected=false};ops.PrepareLaunch(new List<AppRow>{absent});ops.State(absent,"launched","");absent.State="已发送启动请求";Get<DataGrid>(w,"RestoreGrid").ItemsSource=new[]{absent};
  var elapsed=Stopwatch.StartNew();int polls=0,heartbeats=0;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};timer.Tick+=delegate{heartbeats++;};timer.Start();
  Wait(Monitor(ops,new[]{absent},()=>{polls++;return new HashSet<string>(StringComparer.OrdinalIgnoreCase);},ms=>Task.Delay(ms)));timer.Stop();elapsed.Stop();
  Console.WriteLine("OBSERVED real-timeout polls="+polls+" elapsedMs="+elapsed.ElapsedMilliseconds+" uiHeartbeats="+heartbeats+" state="+absent.State);
  Check("real-timeout-five-checks-fifteen-seconds",polls==5&&elapsed.ElapsedMilliseconds>=14800&&elapsed.ElapsedMilliseconds<23000);Check("real-timeout-failure-retryable",absent.ItemStatus=="launchfailed"&&absent.State=="启动失败"&&absent.CanRestore);Check("polling-keeps-ui-responsive",heartbeats>=40);Render(w,"restore-failed-dark");
 }
 [STAThread]static int Main(string[] args){dir=args[0];Directory.CreateDirectory(dir);try{var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Window w;using(var stream=typeof(Entry).Assembly.GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))w=(Window)XamlReader.Load(stream);using(var db=new Database(Path.Combine(dir,Guid.NewGuid()+".db"))){var c=new Controller(app,w,db,true);c.Start(null);Idle(w);
 var buttons=new[]{Get<Button>(w,"MinimizeWindow"),Get<Button>(w,"MaximizeWindow"),Get<Button>(w,"CloseWindow")};Check("caption-equal-widths",buttons.Select(b=>Math.Round(b.ActualWidth,2)).Distinct().Count()==1);Check("caption-equal-heights",buttons.Select(b=>Math.Round(b.ActualHeight,2)).Distinct().Count()==1);Check("caption-equal-margins",buttons.Select(b=>b.Margin).Distinct().Count()==1);Check("caption-all-vector-icons",buttons.All(b=>Find<System.Windows.Shapes.Path>(b).Any()));
 var glyph=Get<System.Windows.Shapes.Path>(w,"MaximizeGlyph");if(glyph!=null){string normal=glyph.Data.ToString();w.WindowState=WindowState.Maximized;Idle(w);Check("maximize-indicates-restore",glyph.Data.ToString()!=normal);w.WindowState=WindowState.Normal;Idle(w);Check("restore-indicates-maximize",glyph.Data.ToString()==normal);}
 Check("about-outside-titlebar",!Within(Get<Button>(w,"NavAbout"),Get<Grid>(w,"TitleBar")));var parent=VisualTreeHelper.GetParent(Get<Button>(w,"NavSettings")) as Panel;Check("about-below-settings",parent!=null&&parent.Children.Contains(Get<Button>(w,"NavAbout"))&&parent.Children.IndexOf(Get<Button>(w,"NavAbout"))>parent.Children.IndexOf(Get<Button>(w,"NavSettings")));
 Check("refresh-state-button",Get<Button>(w,"RefreshRestoreButton")!=null);if(Get<Button>(w,"RefreshRestoreButton")!=null)Check("refresh-records-and-states-separate",Get<Button>(w,"ReloadHistoryButton")!=null);
 var grid=Get<DataGrid>(w,"ProcessGrid");grid.ItemsSource=new[]{new AppRow{Name="受保护应用",ProcessName="Protected.exe",State="关闭失败：拒绝访问；进程受到安全软件自保护，当前权限不能结束该程序",MemoryMB=12345}};Idle(w);var cell=grid.Columns.Last().GetCellContent(grid.Items[0]) as TextBlock;Check("failure-text-wraps-and-ellipsizes",cell!=null&&cell.TextWrapping==TextWrapping.Wrap&&cell.TextTrimming==TextTrimming.CharacterEllipsis);Check("failure-full-tooltip",cell!=null&&Convert.ToString(cell.ToolTip).Contains("拒绝访问"));Render(w,"current-failure-light");
 var r=new List<AppRow>{new AppRow{Name="完成项",Path=@"C:\test\done.exe"},new AppRow{Name="未关闭甲",Path=@"C:\test\failed.exe"},new AppRow{Name="未关闭乙",Path=@"C:\test\other.exe"}};long batchId=db.Batch(r);var ops=new OperationService(db);r=ops.History(batchId);ops.State(r[0],"closed","");ops.State(r[1],"closefailed","拒绝访问");ops.State(r[2],"cancelled","用户放弃");Call(c,"CompleteClose",batchId,1,3);string status=Get<TextBlock>(w,"StatusText").Text;Check("close-footer-names-failed-apps",status.Contains("未关闭甲")&&status.Contains("未关闭乙")&&!status.Contains("完成项"));Check("close-footer-keeps-current-page",Get<Grid>(w,"CurrentPanel").Visibility==Visibility.Visible);
 Get<System.Windows.Controls.ComboBox>(w,"ThemeInput").SelectedIndex=1;Idle(w);Render(w,"current-failure-dark");
 Get<Button>(w,"NavRestore").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Idle(w);Render(w,"restore-dark");States(db);Live(c,w,db);Call(c,"Exit");}
 }catch(Exception ex){failed++;Console.Error.WriteLine(ex);}Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}
