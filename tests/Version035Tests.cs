using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TrayLeaf;
class Version035Tests {
 static int passed,failed;static string dir;
 static void Check(string n,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+n);if(ok)passed++;else failed++;}
 static T Get<T>(Window w,string n)where T:class{return (T)w.FindName(n);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,a);}
 static AppRow Row(string p,string name,bool selected){return new AppRow{Path=p,Name=name,ProcessName=Path.GetFileName(p),Source="任务栏窗口",Args="--marker original",Admin=true,Selected=selected,ArgsKnown=true};}
 static void Idle(Window w){w.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));w.UpdateLayout();}
 static List<AppRow> Visible(DataGrid g){return CollectionViewSource.GetDefaultView(g.ItemsSource).Cast<AppRow>().ToList();}
 static IEnumerable<TextBlock> Texts(DependencyObject p){for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);if(c is TextBlock)yield return (TextBlock)c;foreach(var t in Texts(c))yield return t;}}
 static string FirstPath(DataGrid g){var row=Visible(g).FirstOrDefault();return row==null?"":row.Path;}
 static void Config(){var t=typeof(Entry).Assembly.GetType("TrayLeaf.RuntimeConfiguration");Check("named-config-loader",t!=null);if(t==null)return;var method=t.GetMethod("Load",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);Check("config-load-api",method!=null);if(method==null)return;
 string p=Path.Combine(dir,"TrayLeaf.config");File.WriteAllText(p,"<configuration><runtime><AppContextSwitchOverrides value=\"Switch.System.Windows.DoNotScaleForDpiChanges=true\"/></runtime></configuration>");method.Invoke(null,new object[]{p});bool value;Check("named-config-applies-runtime-switch",AppContext.TryGetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges",out value)&&value);
 File.WriteAllText(p,"<broken>");method.Invoke(null,new object[]{p});Check("invalid-config-safe-dpi-default",AppContext.TryGetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges",out value)&&!value);File.Delete(p);method.Invoke(null,new object[]{p});Check("missing-config-safe-default",AppContext.TryGetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges",out value)&&!value);
 }
 static void Ui(){var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};string path=Path.Combine(dir,"v035-ui.db");if(File.Exists(path))File.Delete(path);using(var db=new Database(path)){
 Window w;using(var s=typeof(Entry).Assembly.GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))w=(Window)XamlReader.Load(s);
 Check("larger-default-window",w.Width>=1280&&w.Height>=960);var c=new Controller(app,w,db,true);c.Start(null);Idle(w);
 var list=new List<AppRow>{Row(Path.Combine(dir,"alpha.exe"),"Alpha",false),Row(Path.Combine(dir,"beta.exe"),"Beta",true),Row(Path.Combine(dir,"gamma.exe"),"Gamma",false)};list[0].MemoryMB=900;list[1].MemoryMB=20;list[2].MemoryMB=100;
 var field=typeof(Controller).GetField("current",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(c,list);var grid=Get<DataGrid>(w,"ProcessGrid");grid.ItemsSource=list;
 var historical=Row(list[1].Path,"Beta",false);historical.Args="--from-history 世界";long batch=db.Batch(new List<AppRow>{historical});Call(c,"ReloadCollections");Get<TextBox>(w,"Search").Text="non-matching-query";Get<ComboBox>(w,"CloseHistoryCombo").SelectedIndex=0;Idle(w);
 var current=(List<AppRow>)field.GetValue(c);var picked=current.Single(x=>x.Path==historical.Path);
 Check("history-source-label",picked.Source=="历史记录");Check("history-default-selected",picked.Selected);Check("history-preserves-args-and-admin",picked.Args==historical.Args&&picked.Admin);Check("history-clears-stale-search",Get<TextBox>(w,"Search").Text=="");Check("history-visible-and-pinned",Visible(grid).Count==3&&FirstPath(grid)==historical.Path);Check("history-unselected-others",current.Where(x=>x.Path!=historical.Path).All(x=>!x.Selected));Check("history-feedback-text-removed",String.IsNullOrEmpty(Get<TextBlock>(w,"StatusText").Text));
 var memory=grid.Columns.Single(x=>x.SortMemberPath=="MemoryMB");var sort=typeof(DataGrid).GetMethod("OnSorting",BindingFlags.Instance|BindingFlags.NonPublic);sort.Invoke(grid,new object[]{new DataGridSortingEventArgs(memory)});Idle(w);Check("history-pinned-under-memory-ascending",FirstPath(grid)==historical.Path&&memory.SortDirection==ListSortDirection.Ascending);sort.Invoke(grid,new object[]{new DataGridSortingEventArgs(memory)});Check("history-pinned-under-memory-descending",FirstPath(grid)==historical.Path&&memory.SortDirection==ListSortDirection.Descending);Check("unpinned-order-still-sorts",Visible(grid).Skip(1).Select(x=>x.MemoryMB).SequenceEqual(new[]{900.0,100.0}));
 var source=grid.Columns.Single(x=>x.SortMemberPath=="Source");sort.Invoke(grid,new object[]{new DataGridSortingEventArgs(source)});Check("history-pinned-under-source-sort",FirstPath(grid)==historical.Path&&source.SortDirection.HasValue&&!memory.SortDirection.HasValue);
 var second=Row(list[2].Path,"Gamma",true);db.Batch(new List<AppRow>{second});Call(c,"ReloadCollections");Get<ComboBox>(w,"CloseHistoryCombo").SelectedIndex=0;Idle(w);Check("new-history-replaces-prior-pins",FirstPath(grid)==second.Path&&picked.Source=="任务栏窗口"&&!picked.Selected);
 Get<TextBox>(w,"Search").Text="Beta";Call(c,"ApplyFilter");Check("search-still-works",Visible(grid).Count==1&&Visible(grid)[0].Name=="Beta");Get<TextBox>(w,"Search").Text="";
 Call(c,"CompleteClose",batch,1,1);Check("completion-message-no-full-stop",Get<TextBlock>(w,"StatusText").Text=="共计 1 个程序，已全部关闭");Check("close-still-stays-on-current-page",Get<Grid>(w,"CurrentPanel").Visibility==Visibility.Visible);
 bool clean=true;foreach(int page in new[]{0,1,2}){Call(c,"Navigate",page);Idle(w);clean&=Texts(w).Where(t=>t.IsVisible).All(t=>!t.Text.Contains("。"));}Check("all-page-copy-no-chinese-full-stops",clean);Call(c,"Navigate",0);
 var size=typeof(Entry).Assembly.GetType("TrayLeaf.WindowSizing");Check("work-area-sizing",size!=null);if(size!=null){var fit=size.GetMethod("ApplyDefault",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);fit.Invoke(null,new object[]{w,new Rect(0,0,1280,720)});Check("small-display-window-fits",w.Width<=1248&&w.Height<=688);fit.Invoke(null,new object[]{w,new Rect(0,0,1920,1080)});Check("large-display-prefers-expanded-size",w.Width==1280&&w.Height==960);}
 for(int i=1;i<=12;i++){var extra=Row(Path.Combine(dir,"background"+i+".exe"),"后台程序 "+i,false);extra.MemoryMB=128+i;Call(c,"LoadIcon",extra);current.Add(extra);}grid.ItemsSource=null;grid.ItemsSource=current;Call(c,"ApplyFilter");Idle(w);Check("expanded-window-shows-seven-or-more-programs",grid.ActualHeight>=7*grid.RowHeight+44);var content=(FrameworkElement)w.Content;var image=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var f=File.Create(Path.Combine(dir,"v035-current.png")))encoder.Save(f);Console.WriteLine("RENDER v035-current.png");Call(c,"Exit");}app.Shutdown();}
 [STAThread]static int Main(string[] args){dir=Path.GetFullPath(args[0]);try{Config();Ui();}catch(Exception ex){failed++;Console.Error.WriteLine(ex);}Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}
