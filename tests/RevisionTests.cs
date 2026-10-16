using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TrayLeaf;
class RevisionTests {
 static int pass,fail;static string dir;
 static void Check(string label,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+label);if(ok)pass++;else fail++;}
 static T Get<T>(Window w,string name) where T:class{return w.FindName(name) as T;}
 static void Click(Window w,string name){Get<Button>(w,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
 static void Idle(Window w){w.UpdateLayout();w.Dispatcher.Invoke(new Action(delegate{}),DispatcherPriority.ApplicationIdle);w.UpdateLayout();}
 static void Render(Window w,string file){Idle(w);var v=(FrameworkElement)w.Content;var b=new RenderTargetBitmap((int)v.ActualWidth,(int)v.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(v);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(Path.Combine(dir,file)))png.Save(f);Console.WriteLine("RENDER "+file+" "+b.PixelWidth+"x"+b.PixelHeight);}
 static void RenderElement(FrameworkElement v,string file){v.UpdateLayout();var b=new RenderTargetBitmap((int)Math.Ceiling(v.ActualWidth),(int)Math.Ceiling(v.ActualHeight),96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(new VisualBrush(v),null,new Rect(0,0,v.ActualWidth,v.ActualHeight));b.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(Path.Combine(dir,file)))png.Save(f);Console.WriteLine("RENDER "+file+" "+b.PixelWidth+"x"+b.PixelHeight);}
 static object Call(Database db,string name,params object[] args){return typeof(Database).GetMethod(name).Invoke(db,args);}
 static string Colour(Brush b){var s=b as SolidColorBrush;return s==null?"none":s.Color.ToString();}
 static AppRow Row(string path,string name){return new AppRow{Path=path,Name=name,ProcessName=System.IO.Path.GetFileName(path),Args="--fixture",ArgsKnown=true,Selected=true,Source="通知区域"};}
 [STAThread]static int Main(string[] args){dir=Path.GetFullPath(args[0]);Directory.CreateDirectory(dir);try{var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};using(var db=new Database(Path.Combine(dir,"revision.db")))using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayLeaf.MainWindow.xaml")){
 var w=(Window)XamlReader.Load(stream);app.MainWindow=w;var c=new Controller(app,w,db,true);c.Start(null);Idle(w);
 Check("all-page-titles-without-trailing-full-stop",new[]{"NavCurrent","NavRestore","NavSettings"}.All(n=>{Click(w,n);return !Get<TextBlock>(w,"PageTitle").Text.EndsWith("。");}));Click(w,"NavCurrent");
 var combo=Get<ComboBox>(w,"ScanMode");combo.ApplyTemplate();Check("unified-combobox-popup-template",combo.Template.FindName("PART_Popup",combo)!=null&&combo.Template.FindName("SelectorBorder",combo)!=null);
 var grid=Get<DataGrid>(w,"ProcessGrid");grid.SelectedIndex=1;grid.Focus();Idle(w);var row=(DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(1);var cells=Descendants<DataGridCell>(row).ToList();
 Check("selected-cells-no-system-blue-or-dotted-focus",cells.Count>0&&cells.All(x=>x.Background is SolidColorBrush&&((SolidColorBrush)x.Background).Color.A==0&&Colour(x.Foreground)=="#FF172B40"&&x.FocusVisualStyle==null));
 Check("remove-action-applies-to-all-programs",(string)Get<Button>(w,"ForgetButton").Content=="移除程序"&&Get<Button>(w,"ManageHiddenButton")!=null);
 Render(w,"selected-row.png");combo.IsDropDownOpen=true;Idle(w);var popup=combo.Template.FindName("PART_Popup",combo) as Popup;Check("dropdown-opens-and-binds-items",popup!=null&&popup.IsOpen&&combo.Items.Count==2);if(popup!=null&&popup.Child is FrameworkElement)RenderElement((FrameworkElement)popup.Child,"source-dropdown.png");combo.IsDropDownOpen=false;combo.SelectedIndex=1;Check("dropdown-mode-switch-retained",combo.SelectedIndex==1);combo.SelectedIndex=0;
 var hide=typeof(Database).GetMethod("HidePrograms");Check("persistent-program-removal-api",hide!=null);
 if(hide!=null){
 var a=Row(@"C:\Fixture\Music.exe","音乐播放器");var b=Row(@"C:\Fixture\Chat.exe","聊天应用");var missing=new AppRow{Path="",ProcessName="protected.exe",Name="不可读路径",Selected=true};
 db.SaveManaged(a.Path,a.Name,a.Args,true,true);db.Set("custom:"+a.Path,"1");db.Set("args:"+a.Path,"1");long batch=db.Batch(new List<AppRow>{a});db.Execute("UPDATE items SET status='closed' WHERE batch=?",batch.ToString());
 Call(db,"HidePrograms",new List<AppRow>{a,missing});var hidden=(List<AppRow>)Call(db,"HiddenPrograms");Check("hide-automatic-and-pathless-programs",hidden.Count==2);
 Check("hide-clears-close-choice-not-launch-metadata",db.Query("SELECT chosen,args,admin FROM managed WHERE path=?",a.Path).Single().SequenceEqual(new[]{"0","--fixture","1"}));
 Check("hide-preserves-restart-history",new OperationService(db).History(batch).Single().CanRestore);
 var same=Row(@"c:\fixture\MUSIC.exe","大小写路径");var visible=(List<AppRow>)Call(db,"FilterVisible",new List<AppRow>{same,b,missing});Check("scan-and-close-input-filtered-case-insensitively",visible.Count==1&&visible[0]==b);
 using(var reopened=new Database(db.FilePath)){Check("removal-survives-database-reopen",((List<AppRow>)Call(reopened,"HiddenPrograms")).Count==2);}
 Call(db,"RestorePrograms",hidden);Check("restore-display-clears-exclusions",((List<AppRow>)Call(db,"HiddenPrograms")).Count==0);Check("restore-display-does-not-reselect-for-close",db.Query("SELECT chosen FROM managed WHERE path=?",a.Path)[0][0]=="0");
 Check("restored-rows-pass-scan-filter",((List<AppRow>)Call(db,"FilterVisible",new List<AppRow>{same,b,missing})).Count==3);
 Call(db,"HidePrograms",new List<AppRow>{a,a});Check("remove-idempotent",((List<AppRow>)Call(db,"HiddenPrograms")).Count==1);Call(db,"RestorePrograms",new List<AppRow>{a});
 Call(db,"HidePrograms",new List<AppRow>{a});Call(db,"AllowProgram",same);Check("explicit-readd-clears-path-exclusion",((List<AppRow>)Call(db,"HiddenPrograms")).Count==0);
 Call(db,"HidePrograms",new List<AppRow>{missing});Call(db,"AllowProgram",new AppRow{Path=@"C:\Fixture\SystemHost.exe",ProcessName=missing.ProcessName,Name=missing.Name});Check("explicit-readd-clears-pathless-exclusion",((List<AppRow>)Call(db,"HiddenPrograms")).Count==0);
 var dialogType=typeof(Controller).Assembly.GetType("TrayLeaf.ProgramSelectionDialog");Check("independent-program-selection-dialog",dialogType!=null);
 if(dialogType!=null){var dialog=(Window)Activator.CreateInstance(dialogType,new object[]{w,new List<AppRow>{a,b},false});dialog.Show();Idle(dialog);var dg=Get<DataGrid>(dialog,"Programs");var dr=dg.ItemsSource.Cast<AppRow>().ToList();Check("removal-choices-independent-from-close-checkboxes",dr.All(x=>!x.Selected)&&a.Selected&&b.Selected);Get<TextBox>(dialog,"Filter").Text="Chat";Idle(dialog);Check("removal-dialog-search",dg.Items.Count==1);Click(dialog,"ChooseVisible");Check("choose-visible-only-and-enable-confirm",dr.Count(x=>x.Selected)==1&&dr.Single(x=>x.Selected).Path==b.Path&&Get<Button>(dialog,"Confirm").IsEnabled);Render(dialog,"remove-dialog.png");dialog.Width=620;dialog.Height=440;Idle(dialog);var confirm=Get<Button>(dialog,"Confirm");var point=confirm.TranslatePoint(new Point(),(UIElement)dialog.Content);Check("removal-dialog-minimum-size-buttons-visible",point.Y>=0&&point.Y+confirm.ActualHeight<=((FrameworkElement)dialog.Content).ActualHeight&&point.X>=0&&point.X+confirm.ActualWidth<=((FrameworkElement)dialog.Content).ActualWidth);Render(dialog,"remove-minimum.png");dialog.Close();
 Call(db,"HidePrograms",new List<AppRow>{a,b});var hd=(Window)Activator.CreateInstance(dialogType,new object[]{w,(List<AppRow>)Call(db,"HiddenPrograms"),true});hd.Show();Idle(hd);Render(hd,"hidden-dialog.png");hd.Close();Call(db,"RestorePrograms",new List<AppRow>{a,b});
 // Exercise the actual Controller.Forget handler using a safe modal dispatcher callback.
 var before=grid.ItemsSource.Cast<AppRow>().Count();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};timer.Tick+=delegate{var modal=app.Windows.Cast<Window>().FirstOrDefault(x=>x.GetType()==dialogType);if(modal==null)return;timer.Stop();var list=Get<DataGrid>(modal,"Programs").ItemsSource.Cast<AppRow>().ToList();list[1].Selected=true;Click(modal,"Confirm");};timer.Start();Click(w,"ForgetButton");timer.Stop();Idle(w);Check("controller-remove-immediately-updates-current-list",grid.ItemsSource.Cast<AppRow>().Count()==before-1);Check("controller-remove-recorded-without-closing-processes",((List<AppRow>)Call(db,"HiddenPrograms")).Count==1);
 var current=grid.ItemsSource.Cast<AppRow>().ToList();Check("removed-program-not-in-close-input-or-selection",!current.Any(x=>x.ProcessName=="Messenger.exe"));
 timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};timer.Tick+=delegate{var modal=app.Windows.Cast<Window>().FirstOrDefault(x=>x.GetType()==dialogType);if(modal==null)return;timer.Stop();Click(modal,"ChooseVisible");Click(modal,"Confirm");};timer.Start();Click(w,"NavSettings");Click(w,"ManageHiddenButton");timer.Stop();Check("controller-restore-display-clears-database",((List<AppRow>)Call(db,"HiddenPrograms")).Count==0);
 }
 }
 Click(w,"NavCurrent");Render(w,"main.png");Click(w,"NavSettings");Render(w,"settings.png");Console.WriteLine("OBSERVED fixtures-only; no real process termination/launch; UI rendered at 96-DPI logical coordinates");app.Shutdown();
 }}catch(Exception ex){fail++;Console.Error.WriteLine(ex);}Console.WriteLine("RESULT passed="+pass+" failed="+fail);return fail==0?0:1;}
 static IEnumerable<T> Descendants<T>(DependencyObject o)where T:DependencyObject{if(o==null)yield break;for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++){var c=VisualTreeHelper.GetChild(o,i);if(c is T)yield return (T)c;foreach(var x in Descendants<T>(c))yield return x;}}
}
