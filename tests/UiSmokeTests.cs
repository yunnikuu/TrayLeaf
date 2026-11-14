using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TrayLeaf;
class UiSmokeTests {
 static int passed,failed;
 static void Check(string label,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+label);if(ok)passed++;else failed++;}
 static T Get<T>(Window w,string name) where T:class{return (T)w.FindName(name);}
 static void Click(Window w,string name){Get<Button>(w,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
 static void Render(Window w,string path){w.UpdateLayout();w.Dispatcher.Invoke(new Action(delegate{}),DispatcherPriority.ApplicationIdle);w.UpdateLayout();var content=(FrameworkElement)w.Content;var bmp=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(content);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(path))enc.Save(f);Console.WriteLine("RENDER "+Path.GetFullPath(path)+" "+bmp.PixelWidth+"x"+bmp.PixelHeight);Check("png-nonempty-"+Path.GetFileName(path),new FileInfo(path).Length>10000);}
 [STAThread]static int Main(string[] args){string dir=Path.GetFullPath(args[0]);Directory.CreateDirectory(dir);try{var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};using(var db=new Database(Path.Combine(dir,"ui-tests.db")))using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayLeaf.MainWindow.xaml")){var w=(Window)XamlReader.Load(stream);app.MainWindow=w;var c=new Controller(app,w,db,true);c.Start(Path.Combine(dir,"automatic-preview.png"));var t=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};t.Tick+=delegate{t.Stop();try{
 Check("main-window-visible",w.IsVisible&&Get<Grid>(w,"CurrentPanel").Visibility==Visibility.Visible);
 Check("default-shortcut-close",Get<TextBox>(w,"CloseHotkeyInput").Text=="Ctrl + Alt + 1");
 Check("default-shortcut-restore",Get<TextBox>(w,"RestoreHotkeyInput").Text=="Ctrl + Alt + 2");
 Check("default-risk-warnings-enabled",Get<CheckBox>(w,"WarnRisk").IsChecked==true);
 Check("default-scan-visible-mode",Get<ComboBox>(w,"ScanMode").SelectedIndex==0);
 Render(w,Path.Combine(dir,"main.png"));
 Get<TextBox>(w,"Search").Text="CloudSync";
 var view=CollectionViewSource.GetDefaultView(Get<DataGrid>(w,"ProcessGrid").ItemsSource);Check("search-filters-process-name",view.Cast<object>().Count()==1);
 Get<TextBox>(w,"Search").Text="";Check("clear-search-restores-list",view.Cast<object>().Count()==5);
 Click(w,"NavRestore");Check("restore-navigation",Get<Grid>(w,"RestorePanel").Visibility==Visibility.Visible&&Get<Grid>(w,"CurrentPanel").Visibility==Visibility.Collapsed);
 Get<DataGrid>(w,"RestoreGrid").ItemsSource=new List<AppRow>{new AppRow{Name="音乐播放器",Path=@"C:\Program Files\Example\MusicPlayer.exe",Args="--minimized",Selected=true,ItemStatus="closed",State="待恢复"},new AppRow{Name="云盘同步",Path=@"C:\Program Files\Example\CloudSync.exe",Admin=true,ItemStatus="closed",State="待恢复"}};
 Render(w,Path.Combine(dir,"restore.png"));var rg=Get<DataGrid>(w,"RestoreGrid");Console.WriteLine("OBSERVED restore-grid-width="+rg.ActualWidth+" columns="+String.Join(",",rg.Columns.Select(x=>x.ActualWidth.ToString("F1"))));Check("restore-columns-fit",rg.Columns.Sum(x=>x.ActualWidth)<=rg.ActualWidth+1);
 Click(w,"NavSettings");Check("settings-navigation",Get<ScrollViewer>(w,"SettingsPanel").Visibility==Visibility.Visible);Render(w,Path.Combine(dir,"settings.png"));
 w.Width=960;w.Height=640;Click(w,"NavCurrent");Render(w,Path.Combine(dir,"main-minimum.png"));Check("minimum-size-layout",w.ActualWidth>=960&&w.ActualHeight>=640);
 Click(w,"NavRestore");Render(w,Path.Combine(dir,"restore-minimum.png"));
 Console.WriteLine("OBSERVED demo-data-only; no-process-scan; no-process-termination; no-hotkey-registration");
 }catch(Exception ex){failed++;Console.Error.WriteLine(ex);}finally{app.Shutdown();}};t.Start();app.Run();}}catch(Exception ex){failed++;Console.Error.WriteLine(ex);}Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}
