using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TrayLeaf;
class Version021Tests {
 static int passed,failed; static string dir; static string exe;
 static void Check(string s,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+s);if(ok)passed++;else failed++;}
 static T Get<T>(Window w,string n) where T:class{return (T)w.FindName(n);}
 static IEnumerable<T> Find<T>(DependencyObject o) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++){var c=VisualTreeHelper.GetChild(o,i);if(c is T)yield return (T)c;foreach(var v in Find<T>(c))yield return v;}}
 static Process Start(string args){var p=Process.Start(new ProcessStartInfo(exe,args){UseShellExecute=false});p.WaitForInputIdle(5000);Thread.Sleep(400);return p;}
 static AppRow Row(Process p){p.Refresh();var r=new AppRow{Name="Controlled fixture",Path=exe,Args="",ArgsKnown=true};r.Processes.Add(new Identity{Pid=p.Id,Started=p.StartTime.ToUniversalTime().Ticks,Path=exe});return r;}
 static void Stop(Process p){try{if(!p.HasExited){p.Kill();p.WaitForExit(4000);}}finally{p.Dispose();}}
 static void CloseTests(){using(var db=new Database(Path.Combine(dir,"close.db"))){var ops=new OperationService(db);
 foreach(string mode in new[]{"--tray-only","--ignore-close",""}){var p=Start(mode);try{var r=Row(p);ops.Prepare(new List<AppRow>{r});int prompts=0;var sw=Stopwatch.StartNew();string result=ops.Close(r,x=>{prompts++;return false;});sw.Stop();Console.WriteLine("OBSERVED mode="+mode+" result="+result+" forcePrompts="+prompts+" elapsedMs="+sw.ElapsedMilliseconds+" exited="+p.HasExited);Check("ordinary-close-no-repeated-prompt-"+mode,result=="已关闭"&&p.HasExited&&prompts==0&&r.CanRestore);Check("close-bounded-"+mode,sw.ElapsedMilliseconds<4500);}finally{Stop(p);}}
 var risk=Start("--ignore-close");try{var r=Row(risk);r.Risk=true;ops.Prepare(new List<AppRow>{r});int prompts=0;Check("risk-force-decline-respected",ops.Close(r,x=>{prompts++;return false;}).StartsWith("已取消")&&!risk.HasExited&&prompts==1);}finally{Stop(risk);}
 var owned=Start("--ignore-close");try{var r=Row(owned);r.Processes[0].Started++;bool blocked=false;try{ProcessService.Force(r);}catch(InvalidOperationException){blocked=true;}Check("identity-mismatch-still-blocked",blocked&&!owned.HasExited);}finally{Stop(owned);}
 }}
 static void Sort(DataGrid g,DataGridColumn c){typeof(DataGrid).GetMethod("PerformSort",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,new object[]{c});g.UpdateLayout();}
 static void Render(Window w,string name,double scale){w.UpdateLayout();var e=(FrameworkElement)w.Content;var b=new RenderTargetBitmap((int)(e.ActualWidth*scale),(int)(e.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);b.Render(e);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(Path.Combine(dir,name)))enc.Save(f);Console.WriteLine("RENDER "+name+" "+b.PixelWidth+"x"+b.PixelHeight);}
 static DataGridCell Cell(DependencyObject o){while(o!=null&&!(o is DataGridCell))o=VisualTreeHelper.GetParent(o);return o as DataGridCell;}
 static bool Centered(CheckBox c){var cell=Cell(c);if(cell==null)return false;var p=c.TransformToAncestor(cell).Transform(new Point(c.ActualWidth/2,c.ActualHeight/2));Console.WriteLine("OBSERVED checkbox centerDelta="+Math.Abs(p.X-cell.ActualWidth/2).ToString("F2"));return Math.Abs(p.X-cell.ActualWidth/2)<1.5;}
 static void Layout(Window w,DataGrid g,string tag){g.UpdateLayout();var mem=g.Columns.First(c=>Convert.ToString(c.Header)=="内存占用");var elev=g.Columns.First(c=>Convert.ToString(c.Header)=="来源");Console.WriteLine("OBSERVED "+tag+" memoryWidth="+mem.ActualWidth+" elevationWidth="+elev.ActualWidth);
 foreach(string value in new[]{"1,536 MB","16,384 MB","123,456 MB"}){var ft=new FormattedText(value,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(w.FontFamily,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),w.FontSize,Brushes.Black,VisualTreeHelper.GetDpi(w).PixelsPerDip);Check(tag+"-memory-fits-"+value,mem.ActualWidth>=ft.WidthIncludingTrailingWhitespace+20);}
 var head=Find<DataGridColumnHeader>(g).FirstOrDefault(h=>h.Column==elev);var txt=Find<TextBlock>(head).FirstOrDefault(t=>t.Text=="来源");Check(tag+"-elevation-header-not-clipped",txt!=null&&txt.ActualWidth>=txt.DesiredSize.Width-1);
 var checks=Find<CheckBox>(g).Where(c=>c.DataContext is AppRow).ToList();Check(tag+"-checkboxes-centered",checks.Count>=2&&checks.All(c=>c.HorizontalAlignment==HorizontalAlignment.Center&&c.VerticalAlignment==VerticalAlignment.Center&&Centered(c)));
 }
 static void UiTests(){var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};using(var db=new Database(Path.Combine(dir,"ui.db")))using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayLeaf.MainWindow.xaml")){var w=(Window)XamlReader.Load(stream);app.MainWindow=w;var controller=new Controller(app,w,db,true);controller.Start(null);w.Width=1380;w.Height=900;
 var g=Get<DataGrid>(w,"ProcessGrid");g.ItemsSource=new List<AppRow>{new AppRow{Name="设计工具",ProcessName="Design.exe",MemoryMB=1536,Source="任务栏窗口",State="运行中",Selected=true,Admin=true},new AppRow{Name="开发环境",ProcessName="Dev.exe",MemoryMB=16384,Source="通知区域",State="运行中"},new AppRow{Name="大型项目",ProcessName="Project.exe",MemoryMB=123456,Source="任务栏窗口",State="运行中"}};w.UpdateLayout();
 Layout(w,g,"initial13");
 foreach(var c in g.Columns){double before=c.ActualWidth;c.Width=new DataGridLength(Math.Max(c.MinWidth,before)+24);w.UpdateLayout();Check("resizable-column-"+c.DisplayIndex,g.CanUserResizeColumns&&c.CanUserResize&&c.ActualWidth>=before+20);}
 var dragColumn=g.Columns[0];var dragHeader=Find<DataGridColumnHeader>(g).First(h=>h.Column==dragColumn);var thumb=(Thumb)dragHeader.Template.FindName("PART_RightHeaderGripper",dragHeader);double dragBefore=dragColumn.ActualWidth;thumb.RaiseEvent(new DragStartedEventArgs(0,0){RoutedEvent=Thumb.DragStartedEvent});thumb.RaiseEvent(new DragDeltaEventArgs(24,0){RoutedEvent=Thumb.DragDeltaEvent});thumb.RaiseEvent(new DragCompletedEventArgs(24,0,false){RoutedEvent=Thumb.DragCompletedEvent});w.UpdateLayout();Check("header-gripper-drag-changes-width",dragColumn.ActualWidth>=dragBefore+20);
 Layout(w,g,"font13");var memory=g.Columns.First(c=>Convert.ToString(c.Header)=="内存占用");var source=g.Columns.First(c=>Convert.ToString(c.Header)=="来源");Check("numeric-memory-sort-member",memory.SortMemberPath=="MemoryMB");Sort(g,memory);Check("memory-ascending-numeric",((AppRow)g.Items[0]).MemoryMB==1536&&memory.SortDirection==ListSortDirection.Ascending);w.UpdateLayout();var mh=Find<DataGridColumnHeader>(g).First(h=>h.Column==memory);var glyph=mh.Template.FindName("SortGlyph",mh) as System.Windows.Shapes.Path;Check("visible-ascending-sort-glyph",glyph!=null&&glyph.Visibility==Visibility.Visible&&glyph.Opacity>0.9);Sort(g,memory);Check("memory-descending-numeric",((AppRow)g.Items[0]).MemoryMB==123456&&memory.SortDirection==ListSortDirection.Descending);Sort(g,source);Check("source-sort-glyph-moves",source.SortDirection!=null&&memory.SortDirection==null);Render(w,"current-150pct.png",1.5);
 w.FontSize=22;w.UpdateLayout();Layout(w,g,"font22");Render(w,"current-large-font-200pct.png",2);w.FontSize=13;
 Get<Button>(w,"NavRestore").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var rg=Get<DataGrid>(w,"RestoreGrid");rg.ItemsSource=new List<AppRow>{new AppRow{Name="设计工具",Path=@"C:\Program Files\Design\Design.exe",ItemStatus="closed",State="待恢复",Selected=true,Admin=true}};w.UpdateLayout();Check("restore-checkboxes-centered",Find<CheckBox>(rg).Where(c=>c.DataContext is AppRow).All(c=>c.HorizontalAlignment==HorizontalAlignment.Center&&Centered(c)));Render(w,"restore-150pct.png",1.5);
 w.Width=960;w.Height=640;w.UpdateLayout();Check("restore-columns-fit-minimum",rg.Columns.Sum(c=>c.ActualWidth)<=rg.ActualWidth+1);Render(w,"restore-minimum.png",1);Get<Button>(w,"NavCurrent").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));w.UpdateLayout();Check("minimum-layout-scrollable",g.HorizontalScrollBarVisibility==ScrollBarVisibility.Auto);Render(w,"current-minimum.png",1);app.Shutdown();}}
 [STAThread]static int Main(string[] args){dir=Path.GetFullPath(args[0]);Directory.CreateDirectory(dir);exe=Path.Combine(dir,"TrayLeaf.ControlledFixture.exe");try{CloseTests();UiTests();}catch(Exception ex){failed++;Console.Error.WriteLine(ex);}Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}
