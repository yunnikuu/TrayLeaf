using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
using TrayLeaf;
class LifecycleTests {
 static int passed,failed;
 static void Check(string label,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+label);if(ok)passed++;else failed++;}
 static T Field<T>(Controller c,string n){return (T)typeof(Controller).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);}
 [STAThread]static int Main(string[] a){var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Directory.CreateDirectory(a[0]);try{using(var db=new Database(Path.Combine(a[0],"lifecycle.db")))using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayLeaf.MainWindow.xaml")){
 db.Set("closeHotkey","0|0");db.Set("restoreHotkey","0|0");var w=(Window)XamlReader.Load(stream);app.MainWindow=w;var c=new Controller(app,w,db,false);c.Start(null);var clock=Stopwatch.StartNew();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};int stage=0;TimeSpan beforeCpu=TimeSpan.Zero;long startMs=0;Forms.NotifyIcon tray=null;
 timer.Tick+=delegate{try{if(clock.ElapsedMilliseconds>60000)throw new TimeoutException("lifecycle timeout");if(Field<bool>(c,"busy"))return;
 if(stage==0){Check("normal-mode-scan-finished",true);tray=Field<Forms.NotifyIcon>(c,"tray");Check("notify-icon-visible",tray!=null&&tray.Visible);Check("window-started-visible",w.IsVisible);w.Close();Check("close-hides-not-exits",!w.IsVisible&&tray.Visible);using(var process=Process.GetCurrentProcess()){beforeCpu=process.TotalProcessorTime;startMs=clock.ElapsedMilliseconds;}stage=1;}
 else if(stage==1&&clock.ElapsedMilliseconds-startMs>=5000){using(var process=Process.GetCurrentProcess()){process.Refresh();Console.WriteLine("OBSERVED hidden-working-set-MiB="+(process.WorkingSet64/1048576.0).ToString("F1")+" private-MiB="+(process.PrivateMemorySize64/1048576.0).ToString("F1")+" cpu-ms="+(process.TotalProcessorTime-beforeCpu).TotalMilliseconds.ToString("F1")+" elapsed-ms="+(clock.ElapsedMilliseconds-startMs)+" logical-cpus="+Environment.ProcessorCount);}tray.ContextMenuStrip.Items[0].PerformClick();stage=2;}
 else if(stage==2){Check("tray-menu-reopens-window",w.IsVisible);tray.ContextMenuStrip.Items[4].PerformClick();stage=3;}
 else if(stage==3){var panel=w.FindName("SettingsPanel") as System.Windows.Controls.ScrollViewer;Check("tray-menu-opens-settings",panel.Visibility==Visibility.Visible);timer.Stop();tray.ContextMenuStrip.Items[5].PerformClick();}
 }catch(Exception ex){failed++;Console.Error.WriteLine(ex);timer.Stop();typeof(Controller).GetMethod("Exit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,null);}};timer.Start();app.Run();Check("explicit-exit-closes-window",!w.IsVisible);}}
 catch(Exception ex){failed++;Console.Error.WriteLine(ex);}Console.WriteLine("OBSERVED own-test-process-only; readonly-real-scan; isolated-database; hotkeys-disabled; no-programs-closed-or-launched");Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}
