using System;using System.IO;using System.Linq;using System.Reflection;using System.Diagnostics;using System.Threading;using System.Windows;using System.Windows.Interop;using System.Windows.Threading;using TrayLeaf;
class StartupBehaviorTests {
 static int passed,failed;static readonly BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic,Instance=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string name){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(ok)passed++;else failed++;}
 static void Pump(int ms=80){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 static int Relaunch(string work,bool startup){var info=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--relaunch "+(startup?"--startup ":"")+StartupManager.Quote(Path.Combine(work,"duplicate-must-not-open.db"))){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};using(var p=Process.Start(info)){var watch=Stopwatch.StartNew();while(!p.HasExited&&watch.ElapsedMilliseconds<6000)Pump(20);if(!p.HasExited){p.Kill();p.WaitForExit();return -1;}string stdout=p.StandardOutput.ReadToEnd(),stderr=p.StandardError.ReadToEnd();Console.WriteLine("CHILD "+stdout.Trim()+" STDERR "+stderr.Trim());Pump();return p.ExitCode;}}
 [STAThread]static int Main(string[] args){try{
  if(args.Length>0&&args[0]=="--relaunch"){bool startup=args.Contains("--startup");int result=Entry.Main((startup?new[]{"--startup","--no-elevate","--db",args.Last()}:new[]{"--no-elevate","--db",args.Last()}));Console.WriteLine("RELAUNCH_EXIT="+result);return result;}
  string work=args[0];Directory.CreateDirectory(work);
  var policy=typeof(StartupManager).GetMethod("ShouldLaunchHidden",Static);
  foreach(bool auto in new[]{false,true})foreach(bool silent in new[]{false,true})foreach(bool startup in new[]{false,true}){
   bool want=auto&&silent&&startup;bool actual=policy!=null&&(bool)policy.Invoke(null,new object[]{startup?new[]{"--startup"}:new string[0],auto,silent});
   Check(policy!=null&&actual==want,"visibility-auto-"+auto+"-silent-"+silent+"-login-"+startup);
  }
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Window w;using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))w=(Window)System.Windows.Markup.XamlReader.Load(s);
  using(var db=new Database(Path.Combine(work,"settings.db"))){db.Set("launchHidden","1");db.Set("autoCheckUpdates","0");db.Set("closeHotkey","0|0");db.Set("restoreHotkey","0|0");var controller=new Controller(app,w,db,true);controller.Start(null);Pump();
   var visibility=typeof(Controller).GetMethod("ApplyStartupVisibility",Instance);
   if(visibility!=null){
    visibility.Invoke(controller,new object[]{new string[0],true});Pump();Check(w.IsVisible,"manual-open-shows-with-silent-preference");
    visibility.Invoke(controller,new object[]{new[]{"--startup"},true});Pump();Check(!w.IsVisible,"enabled-silent-login-hides");
    visibility.Invoke(controller,new object[]{new[]{"--startup"},false});Pump();Check(w.IsVisible,"disabled-autostart-does-not-hide");
    db.Set("launchHidden","0");visibility.Invoke(controller,new object[]{new[]{"--startup"},true});Pump();Check(w.IsVisible,"login-with-silent-off-shows");
   }else foreach(string name in new[]{"manual-open-shows-with-silent-preference","enabled-silent-login-hides","disabled-autostart-does-not-hide","login-with-silent-off-shows"})Check(false,name);
   var activation=typeof(Controller).Assembly.GetType("TrayLeaf.SingleInstanceActivation");
   if(activation!=null){
    w.Hide();Pump();Check(Relaunch(work,false)==0&&w.IsVisible,"desktop-relaunch-shows-hidden-existing-window");
    w.WindowState=WindowState.Minimized;w.Hide();Pump();Check(Relaunch(work,false)==0&&w.IsVisible&&w.WindowState==WindowState.Normal,"desktop-relaunch-restores-minimized-window");
    w.Hide();Pump();Check(Relaunch(work,true)==0&&!w.IsVisible,"duplicate-login-does-not-reveal-existing-window");
    Check(!File.Exists(Path.Combine(work,"duplicate-must-not-open.db")),"duplicate-exits-before-opening-another-database");
   }else foreach(string name in new[]{"desktop-relaunch-shows-hidden-existing-window","desktop-relaunch-restores-minimized-window","duplicate-login-does-not-reveal-existing-window","duplicate-exits-before-opening-another-database"})Check(false,name);
   typeof(Controller).GetField("exiting",Instance).SetValue(controller,true);w.Close();Pump();
  }
  Console.WriteLine("RESULT "+passed+" passed "+failed+" failed");return failed==0?0:1;
 }catch(Exception ex){Console.WriteLine("ERROR "+ex);return 2;}}
}
