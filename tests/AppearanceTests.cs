using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TrayLeaf;
class AppearanceTests {
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool AreDpiAwarenessContextsEqual(IntPtr first,IntPtr second);
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr hwnd);
 static int passed,failed;
 static void Check(string label,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+label);if(ok)passed++;else failed++;}
 static T Get<T>(Window w,string n) where T:class{return w.FindName(n) as T;}
 static Window Load(){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))return (Window)XamlReader.Load(s);}
 static void Click(Window w,string n){Get<Button>(w,n).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
 static void Idle(Window w){w.UpdateLayout();w.Dispatcher.Invoke(new Action(delegate{}),DispatcherPriority.ApplicationIdle);w.UpdateLayout();}
 [STAThread]static int Main(string[] a){string dir=Path.GetFullPath(a[0]);Directory.CreateDirectory(dir);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};try{
  using(var db=new Database(Path.Combine(dir,"appearance.db"))){var w=Load();app.MainWindow=w;var c=new Controller(app,w,db,true);c.Start(null);
  var hwnd=new System.Windows.Interop.WindowInteropHelper(w).Handle;var context=GetWindowDpiAwarenessContext(hwnd);Check("native-window-permonitor-v2",AreDpiAwarenessContextsEqual(context,new IntPtr(-4)));Console.WriteLine("OBSERVED native-window-dpi="+GetDpiForWindow(hwnd)+" context="+context);
  Check("default-microsoft-yahei",w.FontFamily.Source=="Microsoft YaHei");
  Check("layout-rounding",w.UseLayoutRounding);Check("pixel-snapping",w.SnapsToDevicePixels);
  var combo=Get<ComboBox>(w,"FontFamilyInput");Check("font-picker-present",combo!=null);
  if(combo!=null){Check("font-list-lazy",combo.Items.Count==0);Click(w,"NavSettings");Check("installed-fonts-loaded",combo.Items.Count>1);Check("font-picker-selected-default",combo.SelectedValue as string=="Microsoft YaHei");
   var font=Fonts.SystemFontFamilies.First(f=>f.Source=="Consolas");combo.SelectedValue=font.Source;
   Check("font-preview-updates",Get<TextBlock>(w,"FontPreview").FontFamily.Source==font.Source);
   Click(w,"SaveSettingsButton");Check("font-applied-live",w.FontFamily.Source==font.Source);Check("font-stored-in-sqlite",db.Get("fontFamily","")==font.Source);
   using(var reopened=new Database(db.FilePath))Check("font-durable-reopen",reopened.Get("fontFamily","")==font.Source);
   var w2=Load();var c2=new Controller(app,w2,db,true);c2.Start(null);Check("font-restored-on-restart",w2.FontFamily.Source==font.Source);w2.Hide();
   var choices=combo.ItemsSource.Cast<FontChoice>().ToArray();
   Check("all-picker-fonts-resolve",choices.All(f=>Appearance.Resolve(f.Source).Source==f.Source));
   var composites=choices.Where(f=>f.Source.StartsWith("Global ",StringComparison.Ordinal)).ToArray();
   Check("composite-font-options-present",composites.Length==4);
   bool compositePreview=true,compositeLive=true,compositeSaved=true,compositeDurable=true,compositeRestart=true;
   foreach(var composite in composites){combo.SelectedValue=composite.Source;compositePreview&=Get<TextBlock>(w,"FontPreview").FontFamily.Source==composite.Source;
    Click(w,"SaveSettingsButton");compositeLive&=w.FontFamily.Source==composite.Source;compositeSaved&=db.Get("fontFamily","")==composite.Source;
    using(var reopened=new Database(db.FilePath))compositeDurable&=reopened.Get("fontFamily","")==composite.Source;
    var wc=Load();var cc=new Controller(app,wc,db,true);cc.Start(null);compositeRestart&=wc.FontFamily.Source==composite.Source;wc.Hide();
   }
   Check("composite-font-preview",compositePreview);Check("composite-font-live",compositeLive);Check("composite-font-saved",compositeSaved);Check("composite-font-durable-reopen",compositeDurable);Check("composite-font-restart",compositeRestart);
   Click(w,"ResetFontButton");Click(w,"SaveSettingsButton");Check("reset-default-font",w.FontFamily.Source=="Microsoft YaHei"&&db.Get("fontFamily","")=="Microsoft YaHei");
   db.Set("fontFamily","TrayLeaf Font Not Installed 7942");var w3=Load();var c3=new Controller(app,w3,db,true);c3.Start(null);Check("missing-font-falls-back",w3.FontFamily.Source=="Microsoft YaHei");w3.Hide();db.Set("fontFamily","Microsoft YaHei");
   Idle(w);var sc=Get<ScrollViewer>(w,"SettingsPanel");sc.ScrollToTop();Idle(w);
   foreach(int dpi in new[]{96,144,192,288}){var content=(FrameworkElement)w.Content;int width=(int)Math.Ceiling(content.ActualWidth*dpi/96.0),height=(int)Math.Ceiling(content.ActualHeight*dpi/96.0);var bmp=new RenderTargetBitmap(width,height,dpi,dpi,PixelFormats.Pbgra32);bmp.Render(content);string path=Path.Combine(dir,"settings-"+dpi+"dpi.png");var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(path))encoder.Save(file);Check("render-"+dpi+"dpi",new FileInfo(path).Length>10000&&bmp.PixelWidth==width);Console.WriteLine("OBSERVED synthetic-render dpi="+dpi+" pixels="+width+"x"+height+" logical="+content.ActualWidth+"x"+content.ActualHeight);}
   w.Width=960;w.Height=640;Idle(w);sc.ScrollToBottom();Idle(w);var save=Get<Button>(w,"SaveSettingsButton");var pt=save.TranslatePoint(new Point(0,0),sc);Check("save-reachable-minimum",pt.Y>=0&&pt.Y+save.ActualHeight<=sc.ActualHeight+1);
  }
  w.Hide();}
 }catch(Exception ex){failed++;Console.Error.WriteLine(ex);}finally{app.Shutdown();}
 Console.WriteLine("OBSERVED UI-demo-only; no-process-termination; no-global-hotkeys; synthetic-DPI-not-hardware-multimonitor");Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}
