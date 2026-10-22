using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TrayLeaf;
class Version092Tests {
 static int passed,failed;
 static void Check(string name,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(ok)passed++;else failed++;}
 static IEnumerable<DependencyObject> Children(DependencyObject p){foreach(object o in LogicalTreeHelper.GetChildren(p)){var d=o as DependencyObject;if(d==null)continue;yield return d;foreach(var c in Children(d))yield return c;}}
 static DataGrid Grid(List<AppRow> rows){var g=new DataGrid();g.Columns.Add(new DataGridTextColumn{Header="程序 / 进程",SortMemberPath="Name"});HistoryPresentation.Configure(g);g.ItemsSource=rows;HistoryPresentation.ApplyOrder(g);return g;}
 static void Render(Window w,string path){w.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>{}));w.UpdateLayout();var b=new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth),(int)Math.Ceiling(w.ActualHeight),96,96,PixelFormats.Pbgra32);b.Render(w);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(path))png.Save(f);}
 [STAThread] static int Main(string[] args){try{
  string work=args[0],source=args[1];Directory.CreateDirectory(work);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  var rows=new List<AppRow>{new AppRow{Name="Zulu Tools",ProcessName="aaa.exe"},new AppRow{Name="Alpha App",ProcessName="zzz.exe"},new AppRow{Name="Beta Host",ProcessName="aaa2.exe"},new AppRow{Name="中文程序",ProcessName="cn.exe"},new AppRow{Name="  Delta Service",ProcessName="service.exe"}};
  var g=Grid(rows);Check("down-visible-label-order",g.Items.OfType<AppRow>().Select(r=>r.Name).SequenceEqual(new[]{"中文程序","Alpha App","Beta Host","  Delta Service","Zulu Tools"}));
  g.Columns[0].SortDirection=ListSortDirection.Ascending;HistoryPresentation.ApplyOrder(g);Check("up-visible-label-order",g.Items.OfType<AppRow>().Select(r=>r.Name).SequenceEqual(new[]{"Zulu Tools","  Delta Service","Beta Host","Alpha App","中文程序"}));
  var many=Enumerable.Range(0,300).Select(i=>new AppRow{Name="Application "+i.ToString("D3"),ProcessName="process"+(300-i).ToString("D3")+".exe"}).Reverse().ToList();g=Grid(many);Check("all-300-a-z",g.Items.OfType<AppRow>().Select(r=>r.Name).SequenceEqual(many.Select(r=>r.Name).OrderBy(n=>n,StringComparer.OrdinalIgnoreCase)));
  g.Columns[0].SortDirection=ListSortDirection.Ascending;HistoryPresentation.ApplyOrder(g);Check("all-300-z-a",g.Items.OfType<AppRow>().Select(r=>r.Name).SequenceEqual(many.Select(r=>r.Name).OrderByDescending(n=>n,StringComparer.OrdinalIgnoreCase)));
  rows[0].HistoryPriority=1;g=Grid(rows);Check("history-pin-preserved",ReferenceEquals(g.Items[0],rows[0]));rows[0].HistoryPriority=0;
  Window shell;using(var s=typeof(Entry).Assembly.GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))shell=(Window)System.Windows.Markup.XamlReader.Load(s);
  shell.Left=-10000;shell.Top=-10000;shell.ShowActivated=false;ThemeManager.Apply(shell,"1",0);shell.Show();
  Check("program-label",((CheckBox)shell.FindName("ConfirmCloseInput")).Content.ToString()=="程序关闭弹窗确认");Check("hide-label",((CheckBox)shell.FindName("ConfirmHideInput")).Content.ToString()=="隐藏程序弹窗确认");
  var force=shell.FindName("ConfirmForceInput") as CheckBox;Check("force-independent",force!=null&&force.Content.ToString()=="强制关闭弹窗确认");Check("duplicate-removed",shell.FindName("WarnRisk")==null);
  using(var db=new Database(Path.Combine(work,"prefs.db"))){Check("force-default",ConfirmationPreference.Required(db,"confirmForce"));ConfirmationPreference.Remember(db,"confirmForce",false,true);Check("force-cancel",ConfirmationPreference.Required(db,"confirmForce"));ConfirmationPreference.Remember(db,"confirmForce",true,true);Check("force-suppress",!ConfirmationPreference.Required(db,"confirmForce"));Check("program-independent",CloseConfirmation.Required(db));Check("hide-independent",ConfirmationPreference.Required(db,"confirmHide"));db.Set("confirmForce","1");Check("force-reenable",ConfirmationPreference.Required(db,"confirmForce"));}
  Dialogs(shell,work);
  var m=new ProgramSelectionDialog(shell,rows,true);m.Left=-10000;m.Top=-10000;m.WindowStartupLocation=WindowStartupLocation.Manual;m.ShowActivated=false;m.Show();m.UpdateLayout();var mg=(DataGrid)m.FindName("Programs");Check("hidden-sort-enabled",mg.CanUserSortColumns);Check("hidden-same-order",mg.Items.OfType<AppRow>().Select(r=>r.Name).SequenceEqual(new[]{"中文程序","Alpha App","Beta Host","  Delta Service","Zulu Tools"}));Check("hidden-icons",mg.Items.OfType<AppRow>().All(r=>r.Icon!=null));Check("hidden-icon-template",System.Windows.Markup.XamlWriter.Save(((DataGridTemplateColumn)mg.Columns[1]).CellTemplate).Contains("Image"));mg.Columns[1].SortDirection=ListSortDirection.Ascending;HistoryPresentation.ApplyOrder(mg);Check("hidden-sort-up",((AppRow)mg.Items[0]).Name=="Zulu Tools");Render(m,Path.Combine(work,"hidden.png"));m.Close();shell.Close();
  string c=File.ReadAllText(Path.Combine(source,"src","App.cs"));Check("force-wired",c.Contains("\"confirmForce\",\"ConfirmForceInput\""));Check("risk-merged",!c.Contains("\"warnRisk\",\"WarnRisk\""));string wxs=File.ReadAllText(Path.Combine(source,"installer","TrayLeaf.wxs"));Check("native-folder-wired",wxs.Contains("Value=\"BrowseInstallFolder\"")&&wxs.Contains("DllEntry=\"BrowseInstallFolder\""));Check("legacy-browser-removed",!wxs.Contains("Id=\"TrayLeafBrowseDlg\""));
 }catch(Exception e){failed++;Console.Error.WriteLine(e);}Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
 static void Dialogs(Window shell,string work){
  var build=typeof(ModernDialog).GetMethod("Build",BindingFlags.Static|BindingFlags.NonPublic);
  foreach(string theme in new[]{"0","1"})foreach(double size in new[]{13.0,20.0}){ThemeManager.Apply(shell,theme,0);shell.FontSize=size;var d=(Window)build.Invoke(null,new object[]{shell,"Windhawk 退出请求后仍在运行\n强制结束可能中断服务或丢失未保存的数据；是否继续？","确认强制关闭",true,true});d.Left=-10000;d.Top=-10000;d.WindowStartupLocation=WindowStartupLocation.Manual;d.ShowActivated=false;d.Show();d.UpdateLayout();var title=Children(d).OfType<TextBlock>().First(t=>t.Text=="确认强制关闭");var body=Children(d).OfType<TextBlock>().First(t=>t.Text.StartsWith("Windhawk"));var yes=Children(d).OfType<Button>().First(b=>(b.Content as string)=="确认继续");double ty=title.TranslatePoint(new Point(),d).Y,by=body.TranslatePoint(new Point(),d).Y,fy=yes.TranslatePoint(new Point(),d).Y;
   Check("title-raised-"+theme+"-"+size,ty<=26);Check("body-centered-"+theme+"-"+size,by>=ty+title.ActualHeight+18&&by+body.ActualHeight<=fy-18);Check("suppression-visible-"+theme+"-"+size,d.Tag is CheckBox&&((CheckBox)d.Tag).ActualWidth>70);Render(d,Path.Combine(work,"dialog-"+theme+"-"+size+".png"));d.Close();}
 }
}
