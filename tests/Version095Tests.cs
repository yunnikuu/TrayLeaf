using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TrayLeaf;
class Version095Tests {
 static int passed,failed;
 static void Check(string name,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(ok)passed++;else failed++;}
 static object Call(object o,string name,params object[] args){var m=o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic);return m==null?null:m.Invoke(o,args);}
 static void Wait(System.Threading.Tasks.Task task){var frame=new System.Windows.Threading.DispatcherFrame();task.ContinueWith(t=>Application.Current.Dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));System.Windows.Threading.Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();}
 static void Render(Window w,string path,double dpi){w.UpdateLayout();var image=new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth*dpi/96),(int)Math.Ceiling(w.ActualHeight*dpi/96),dpi,dpi,PixelFormats.Pbgra32);image.Render(w);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var f=File.Create(path))png.Save(f);}
 [STAThread] static int Main(string[] args){try{
  string work=args[0],source=args[1];Directory.CreateDirectory(work);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  Window shell;using(var s=typeof(Entry).Assembly.GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))shell=(Window)System.Windows.Markup.XamlReader.Load(s);
  shell.ShowActivated=false;shell.Left=-10000;shell.Top=-10000;
  string fixture=Path.Combine(work,"fixture.db");if(File.Exists(fixture))File.Delete(fixture);
  using(var db=new Database(fixture)){var c=new Controller(app,shell,db,true);c.Start(null);shell.Show();System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(shell.Dispatcher));shell.UpdateLayout();
   var grid=(DataGrid)shell.FindName("ProcessGrid");var history=(ComboBox)shell.FindName("CloseHistoryCombo");
   Check("version-095",typeof(Entry).Assembly.GetName().Version.ToString()=="0.9.5.0");
   Check("history-half-width",history.ActualWidth>120&&history.ActualWidth<grid.ActualWidth*.61);
   Check("selection-buttons-below-history",((Button)shell.FindName("SelectAllButton")).TranslatePoint(new Point(),shell).Y>history.TranslatePoint(new Point(),shell).Y);
   Check("automatic-row-height",Double.IsNaN(grid.RowHeight));
   Check("soft-font-formatting",TextOptions.GetTextFormattingMode(shell)==TextFormattingMode.Ideal&&TextOptions.GetTextRenderingMode(shell)==TextRenderingMode.Auto);
   var reset=(Button)shell.FindName("ResetSelectionButton");Check("reset-button-exists",reset!=null);
   var rows=grid.Items.OfType<AppRow>().ToList();foreach(var row in rows){row.Selected=true;row.ClosePriority=1;row.HistoryPriority=1;}
   if(reset!=null)reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check("reset-unchecks-all",rows.All(r=>!r.Selected));Check("reset-clears-pins",rows.All(r=>r.ClosePriority==0&&r.HistoryPriority==0));
   Check("reset-default-sort",grid.Columns.Single(col=>col.SortMemberPath=="Name").SortDirection==ListSortDirection.Descending&&grid.Columns.Count(col=>col.SortDirection.HasValue)==1);
   if(reset!=null){var sort=typeof(DataGrid).GetMethod("OnSorting",BindingFlags.Instance|BindingFlags.NonPublic);var col=grid.Columns.Single(column=>column.SortMemberPath=="Source");sort.Invoke(grid,new object[]{new DataGridSortingEventArgs(col)});sort.Invoke(grid,new object[]{new DataGridSortingEventArgs(col)});reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var name=grid.Columns.Single(column=>column.SortMemberPath=="Name");sort.Invoke(grid,new object[]{new DataGridSortingEventArgs(name)});Check("reset-clears-header-click-memory",name.SortDirection==ListSortDirection.Descending);}
   var close=(TextBox)shell.FindName("CloseHotkeyInput");string original=close.Text;
   close.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,null,close){RoutedEvent=Keyboard.GotKeyboardFocusEvent});
   Check("recording-label",close.Text=="正在录制操作");
   var capture=c.GetType().GetMethod("CaptureKey",BindingFlags.NonPublic|BindingFlags.Instance);
   capture.Invoke(c,new object[]{close,new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(shell),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent},true});
   Check("escape-restores-shortcut",close.Text==original);
   close.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,null,close){RoutedEvent=Keyboard.GotKeyboardFocusEvent});
   capture.Invoke(c,new object[]{close,new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(shell),0,Key.Back){RoutedEvent=Keyboard.PreviewKeyDownEvent},true});Check("backspace-clears-shortcut",close.Text=="未设置");
   var lookup=typeof(Entry).Assembly.GetType("TrayLeaf.ProgramLookup");Check("shared-duplicate-lookup",lookup!=null);
   if(lookup!=null){var m=lookup.GetMethod("Find",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);var sample=new List<AppRow>{new AppRow{Path=@"C:\Apps\one.exe",Name="One"}};Check("duplicate-case-insensitive",m.Invoke(null,new object[]{sample,@"c:\apps\ONE.exe"})!=null);Check("same-name-different-path",m.Invoke(null,new object[]{sample,@"D:\Other\one.exe"})==null);}
   Call(c,"Navigate",1);shell.UpdateLayout();var batch=(ComboBox)shell.FindName("BatchCombo");var restore=(DataGrid)shell.FindName("RestoreGrid");Check("restore-history-half-width",batch.ActualWidth>120&&batch.ActualWidth<restore.ActualWidth*.61);
   Call(c,"Navigate",0);
   foreach(var font in new[]{"Microsoft YaHei","Segoe UI","SimSun"}){shell.FontFamily=Appearance.Resolve(font);shell.FontSize=18;shell.UpdateLayout();Check("font-row-fit-"+font,grid.MinRowHeight>=shell.FontFamily.LineSpacing*shell.FontSize*2+16);}
   Render(shell,Path.Combine(work,"large-font-144dpi.png"),144);shell.FontSize=13;shell.FontFamily=Appearance.Resolve(Appearance.DefaultFont);Render(shell,Path.Combine(work,"current-96dpi.png"),96);
   Call(c,"Navigate",1);Render(shell,Path.Combine(work,"restore-192dpi.png"),192);
   var scanType=typeof(Entry).Assembly.GetType("TrayLeaf.ScanResult");
   var scanner=c.GetType().GetField("scanPrograms",BindingFlags.Instance|BindingFlags.NonPublic);
   var notify=c.GetType().GetField("duplicateNotice",BindingFlags.Instance|BindingFlags.NonPublic);
   var focus=c.GetType().GetMethod("FocusExistingProgram",BindingFlags.Instance|BindingFlags.NonPublic);
   if(scanner!=null&&notify!=null&&focus!=null){
    int notices=0;bool all=false;var existing=new AppRow{Name="Existing Outside Tray",ProcessName="outside.exe",Path=@"C:\Fixture\outside.exe",Source="全部进程"};
    scanner.SetValue(c,new Func<bool,bool,IEnumerable<string>,ScanResult>((a,b,paths)=>{all=a;return new ScanResult{Rows=new List<AppRow>{existing}};}));
    notify.SetValue(c,new Action<Window,string>((owner,message)=>{if(message=="该应用已在列表内 无需再次添加")notices++;}));
    Wait((System.Threading.Tasks.Task)focus.Invoke(c,new object[]{existing}));
    Check("duplicate-full-process-scan",all);Check("duplicate-one-notice",notices==1);Check("duplicate-selected-existing-row",((AppRow)grid.SelectedItem).Path==existing.Path);Check("duplicate-one-visible-row",grid.Items.OfType<AppRow>().Count(r=>r.Path==existing.Path)==1);
    Check("duplicate-not-persisted-as-custom",db.Get("custom:"+existing.Path,"0")=="0");
   }else Check("duplicate-flow-available",false);
   Call(c,"Navigate",2);Call(c,"SelectSettings",0);shell.UpdateLayout();var scale=(Slider)shell.FindName("FontScaleInput");Check("font-scale-setting",scale!=null);
   if(scale!=null){scale.Value=150;shell.UpdateLayout();Check("font-scale-applies",Math.Abs(shell.FontSize-19.5)<.01);Check("heading-scales",Math.Abs((double)shell.Resources["FontSize23"]-34.5)<.01);Call(c,"SaveModern");Check("font-scale-persists",db.Get("fontScale","")=="150");Render(shell,Path.Combine(work,"appearance-150percent.png"),96);Call(c,"Navigate",0);shell.Width=1040;shell.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>{}));Render(shell,Path.Combine(work,"current-150percent-1040.png"),96);var commands=(StackPanel)shell.FindName("SearchCommands");var toolbar=(Grid)shell.FindName("SearchToolbar");Check("large-font-toolbar-fits",commands.TranslatePoint(new Point(commands.ActualWidth,0),toolbar).X<=toolbar.ActualWidth+1&&((TextBox)shell.FindName("Search")).ActualWidth>=170);scale.Value=100;}
   Call(c,"Navigate",0);ThemeManager.Apply(shell,"1",0);shell.Width=1040;Render(shell,Path.Combine(work,"dark-current-1040.png"),96);shell.Hide();
  }
  string installer=File.ReadAllText(Path.Combine(source,"installer","TrayLeaf.wxs"));Check("installer-preparing-text",installer.Contains("正在准备安装"));Check("installer-custom-progress",installer.Contains("<Dialog Id=\"TrayLeafProgressDlg\""));
  Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed");return failed==0?0:1;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 2;}}
}
