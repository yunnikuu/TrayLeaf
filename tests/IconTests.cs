using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TrayLeaf;
using Forms=System.Windows.Forms;
class IconTests {
 static int passed,failed;static string output;static Assembly subject=typeof(Controller).Assembly;
 static void Check(string name,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(ok)passed++;else failed++;}
 static string Hash(byte[] bytes){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(bytes)).Replace("-","");}
 static byte[] Bytes(Stream s){if(s==null)return new byte[0];using(s)using(var m=new MemoryStream()){s.CopyTo(m);return m.ToArray();}}
 static string Pixels(BitmapSource s){if(s==null)return "null";var c=new FormatConvertedBitmap(s,PixelFormats.Bgra32,null,0);byte[] p=new byte[c.PixelWidth*c.PixelHeight*4];c.CopyPixels(p,c.PixelWidth*4,0);return c.PixelWidth+"x"+c.PixelHeight+":"+Hash(p);}
 static BitmapSource Png(string path){using(var s=File.OpenRead(path)){var b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;b.DecodePixelWidth=256;b.StreamSource=s;b.EndInit();b.Freeze();return b;}}
 static BitmapSource Ico(string path){using(var s=File.OpenRead(path)){var d=new IconBitmapDecoder(s,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);return d.Frames.OrderByDescending(f=>f.PixelWidth).First();}}
 static object Call(Controller c,string method){return typeof(Controller).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,null);}
 static object Field(Controller c,string name){return typeof(Controller).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);}
 static void Idle(Window w){w.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));w.UpdateLayout();}
 static bool OldMark(DependencyObject n){var t=n as TextBlock;if(t!=null&&t.Text=="休")return true;for(int i=0;i<VisualTreeHelper.GetChildrenCount(n);i++)if(OldMark(VisualTreeHelper.GetChild(n,i)))return true;return false;}
 static byte[] IconPixels(System.Drawing.Icon icon){using(var b=icon.ToBitmap())using(var m=new MemoryStream()){b.Save(m,System.Drawing.Imaging.ImageFormat.Png);return m.ToArray();}}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr FindResource(IntPtr module,IntPtr name,IntPtr type);
 [DllImport("kernel32.dll")]static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
 [DllImport("kernel32.dll")]static extern IntPtr LockResource(IntPtr resource);
 [DllImport("kernel32.dll")]static extern uint SizeofResource(IntPtr module,IntPtr resource);
 [DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr module);
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr wParam,IntPtr lParam);
 static byte[] WinResource(IntPtr m,int id,int type){IntPtr r=FindResource(m,(IntPtr)id,(IntPtr)type);if(r==IntPtr.Zero)return new byte[0];uint size=SizeofResource(m,r);var b=new byte[size];Marshal.Copy(LockResource(LoadResource(m,r)),b,0,b.Length);return b;}
 static void CheckExe(byte[] expected){IntPtr m=LoadLibraryEx(subject.Location,IntPtr.Zero,2);try{var group=WinResource(m,32512,14);if(group.Length==0)group=WinResource(m,1,14);int count=group.Length>=6?BitConverter.ToUInt16(group,4):0;Check("exe-shell-icon-ten-sizes",count==10);bool all=count==10;for(int i=0;i<count&&all;i++){int g=6+i*14,e=6+i*16;int id=BitConverter.ToUInt16(group,g+12);int size=(int)BitConverter.ToUInt32(expected,e+8),offset=(int)BitConverter.ToUInt32(expected,e+12);byte[] actual=WinResource(m,id,3);all=actual.SequenceEqual(expected.Skip(offset).Take(size));}Check("exe-icon-payload-matches-approved-artwork",all);}finally{if(m!=IntPtr.Zero)FreeLibrary(m);}}
 [STAThread]static int Main(string[] args){output=Path.GetFullPath(args[2]);Directory.CreateDirectory(output);Environment.CurrentDirectory=output;Window w=null;Controller c=null;Database db=null;try{
 var expectedIco=File.ReadAllBytes(args[0]);var expectedPng=File.ReadAllBytes(args[1]);
 CheckExe(expectedIco);
 Check("embedded-ico-matches-approved-file",Bytes(subject.GetManifestResourceStream("TrayLeaf.Assets.TrayLeaf.ico")).SequenceEqual(expectedIco));
 Check("embedded-png-matches-approved-file",Bytes(subject.GetManifestResourceStream("TrayLeaf.Assets.TrayLeaf.png")).SequenceEqual(expectedPng));
 using(var stream=subject.GetManifestResourceStream("TrayLeaf.Assets.TrayLeaf.ico")){Check("embedded-ico-supports-16-through-256",stream!=null&&new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames.Select(f=>f.PixelWidth).OrderBy(x=>x).SequenceEqual(new[]{16,20,24,32,40,48,64,96,128,256}));}
 var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};db=new Database(Path.Combine(output,"icons.db"));
 using(var stream=subject.GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))w=(Window)XamlReader.Load(stream);app.MainWindow=w;c=new Controller(app,w,db,true);c.Start(null);Idle(w);
 Check("window-icon-is-assigned",w.Icon!=null);Check("window-icon-is-approved-artwork",Pixels(w.Icon as BitmapSource)==Pixels(Ico(args[0])));
 var logo=w.FindName("BrandIcon") as Image;Check("sidebar-icon-is-visible",logo!=null&&logo.Source!=null&&logo.ActualWidth>0);Check("sidebar-icon-is-approved-artwork",logo!=null&&Pixels(logo.Source as BitmapSource)==Pixels(Png(args[1])));
 Check("old-sidebar-character-is-replaced",!OldMark(w));Check("wpf-images-frozen-and-stream-independent",w.Icon!=null&&w.Icon.IsFrozen&&logo!=null&&logo.Source.IsFrozen);
 IntPtr hwnd=new WindowInteropHelper(w).Handle;Check("window-taskbar-big-icon-is-exposed",SendMessage(hwnd,0x7F,(IntPtr)1,IntPtr.Zero)!=IntPtr.Zero);Check("window-small-icon-is-exposed",SendMessage(hwnd,0x7F,IntPtr.Zero,IntPtr.Zero)!=IntPtr.Zero);
 Call(c,"CreateTray");var tray=Field(c,"tray") as Forms.NotifyIcon;var icon=Field(c,"trayIcon") as System.Drawing.Icon;
 Check("notify-icon-is-visible",tray!=null&&tray.Visible&&tray.Icon!=null);
 using(var expected=new System.Drawing.Icon(args[0],Forms.SystemInformation.SmallIconSize)){Check("tray-icon-matches-approved-artwork",icon!=null&&IconPixels(icon).SequenceEqual(IconPixels(expected)));}
 w.Close();Idle(w);Check("hide-to-tray-preserves-identity",!w.IsVisible&&tray.Visible&&Pixels(w.Icon as BitmapSource)==Pixels(Ico(args[0])));Call(c,"Show");Idle(w);Check("reopen-retains-approved-logo",w.IsVisible&&logo!=null&&logo.Source!=null);
 var content=(FrameworkElement)w.Content;var bitmap=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(output,"icon-ui-preview.png")))encoder.Save(f);
 Call(c,"Exit");Check("tray-icon-hidden-on-explicit-exit",!tray.Visible);c=null;w=null;
 }catch(Exception ex){Console.Error.WriteLine(ex);failed++;}finally{if(c!=null)try{Call(c,"Exit");}catch{}if(db!=null)db.Dispose();}
 Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed");return failed==0?0:1;}
}
