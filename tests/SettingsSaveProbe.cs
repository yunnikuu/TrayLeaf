using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
class Release083BehaviorProbe {
 [DllImport("advapi32.dll")]static extern int RegOverridePredefKey(IntPtr key,IntPtr replacement);
 static object Call(object o,string name,params object[] args){try{return o.GetType().GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);}catch(TargetInvocationException ex){throw ex.InnerException;}}
 static T Get<T>(Window w,string n)where T:class{return (T)w.FindName(n);}
 static bool Transparent(Brush b){var s=b as SolidColorBrush;return s!=null&&s.Color.A==0;}
 static void Render(Window w,string name){w.UpdateLayout();var bmp=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(w);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(Path.Combine(Environment.GetEnvironmentVariable("TRAYLEAF_PROBE_SCREENSHOTS")??Path.GetTempPath(),name)))png.Save(f);}
 [STAThread]static int Main(string[] a){Console.OutputEncoding=new System.Text.UTF8Encoding(false);if(a.Length!=3||a[1]!="--scenario"||a[2]!="settings-save")return 2;string temp=Path.Combine(Path.GetTempPath(),"TrayLeaf083Probe-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);string keyName=@"Software\TrayLeaf.TestIsolation\"+Guid.NewGuid().ToString("N");RegistryKey key=null;bool redirected=false;object c=null;IDisposable db=null;Window w=null;Application app=null;try{
  // Refuse to touch a configured task; all registry writes are redirected to this process's test hive.
  object svc=null,folder=null,task=null;try{svc=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));dynamic s=svc;s.Connect();folder=s.GetFolder("\\");dynamic f=folder;try{task=f.GetTask("TrayLeaf-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value);Console.WriteLine("REFUSED_EXISTING_USER_TASK");return 3;}catch(Exception ex){if((uint)ex.HResult!=0x80070002)throw;}}finally{foreach(var x in new[]{task,folder,svc})if(x!=null)Marshal.FinalReleaseComObject(x);}
  var asm=Assembly.LoadFile(Path.GetFullPath(a[0]));var controller=asm.GetType("TrayLeaf.Controller");app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};using(var stream=asm.GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))w=(Window)XamlReader.Load(stream);app.MainWindow=w;db=(IDisposable)Activator.CreateInstance(asm.GetType("TrayLeaf.Database"),new object[]{Path.Combine(temp,"settings.db")});Call(db,"Set","closeHotkey","0|0");Call(db,"Set","restoreHotkey","0|0");c=Activator.CreateInstance(controller,new object[]{app,w,db,true});Call(c,"Start",new object[]{null});
  key=Registry.CurrentUser.CreateSubKey(keyName);int result=RegOverridePredefKey(new IntPtr(unchecked((int)0x80000001)),key.Handle.DangerousGetHandle());if(result!=0)throw new System.ComponentModel.Win32Exception(result);redirected=true;
  controller.GetField("demo",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,false);
  Console.WriteLine("SCENARIO=settings-save");Console.WriteLine("VERSION="+System.Diagnostics.FileVersionInfo.GetVersionInfo(a[0]).ProductVersion);
  Get<CheckBox>(w,"AutoStartInput").IsChecked=false;Get<CheckBox>(w,"RunAsAdminInput").IsChecked=true;Get<ComboBox>(w,"ThemeInput").SelectedIndex=1;Get<ComboBox>(w,"AccentInput").SelectedIndex=2;Get<Slider>(w,"RadiusInput").Value=20;Get<CheckBox>(w,"ConfirmCloseInput").IsChecked=false;bool saved=false;
  try{Call(c,"SaveSettings");saved=true;Console.WriteLine("SETTINGS_SAVE=SAVED");}catch(Exception ex){Console.WriteLine("SETTINGS_SAVE=FAILED:"+ex.GetType().Name+":0x"+ex.HResult.ToString("X8"));}
  Console.WriteLine("DB_THEME="+Call(db,"Get","theme","unset"));Console.WriteLine("DB_RADIUS="+Call(db,"Get","cornerRadius","unset"));Console.WriteLine("DB_RISK="+Call(db,"Get","confirmClose","unset"));Console.WriteLine("PREFERENCE_ADMIN="+asm.GetType("TrayLeaf.StartupManager").GetProperty("PreferAdmin",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null,null));
  if(saved){Get<CheckBox>(w,"RunAsAdminInput").IsChecked=false;Call(c,"SaveSettings");Console.WriteLine("SECOND_SAVE=SAVED");Console.WriteLine("PREFERENCE_ADMIN_AFTER_SECOND="+asm.GetType("TrayLeaf.StartupManager").GetProperty("PreferAdmin",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null,null));}
  controller.GetField("demo",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,true);
  var d=new Window{Title="操作未完成",Width=510,Height=230,Resources=w.Resources,FontFamily=w.FontFamily,Content=new StackPanel{Margin=new Thickness(24),Children={new TextBlock{Text="操作未完成",FontSize=22},new TextBlock{Text="弹窗样式验证",Margin=new Thickness(0,16,0,0)}}}};asm.GetType("TrayLeaf.Chrome").GetMethod("Decorate",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{d,false});d.Show();d.UpdateLayout();var close=((Grid)((Grid)d.Content).Children[0]).Children.OfType<Button>().Single();close.ApplyTemplate();Console.WriteLine("DIALOG_CLOSE_TRANSPARENT="+Transparent(close.Background));Console.WriteLine("DIALOG_CLOSE_BORDERLESS="+(close.BorderThickness==new Thickness(0)));Console.WriteLine("DIALOG_CLOSE_VECTOR="+(close.Content is System.Windows.Shapes.Path));Render(d,"dialog-v083-dark.png");asm.GetType("TrayLeaf.ThemeManager").GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,"0",0});Render(d,"dialog-v083-light.png");close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Console.WriteLine("DIALOG_CLOSE_WORKS="+!d.IsVisible);
  if(saved){((IDisposable)db).Dispose();db=null;var read=(IDisposable)Activator.CreateInstance(asm.GetType("TrayLeaf.Database"),new object[]{Path.Combine(temp,"settings.db")});using(read)Console.WriteLine("REOPENED_DB_THEME="+Call(read,"Get","theme","unset"));}
  Console.WriteLine("EXIT=0");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{if(c!=null){c.GetType().GetField("demo",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,true);Call(c,"Exit");}if(db!=null)db.Dispose();if(redirected)RegOverridePredefKey(new IntPtr(unchecked((int)0x80000001)),IntPtr.Zero);if(key!=null)key.Dispose();Registry.CurrentUser.DeleteSubKeyTree(keyName,false);}}
}
