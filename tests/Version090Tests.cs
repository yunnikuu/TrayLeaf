using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using TrayLeaf;
class Version090Tests {
 static int passed,failed;
 static void Check(string n,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+n);if(ok)passed++;else failed++;}
 static string Order(DataGrid grid){return String.Join(",",grid.Items.OfType<AppRow>().Select(r=>r.ProcessName));}
 static IEnumerable<DependencyObject> Children(DependencyObject parent){foreach(object item in LogicalTreeHelper.GetChildren(parent)){var c=item as DependencyObject;if(c==null)continue;yield return c;foreach(var d in Children(c))yield return d;}}
 [STAThread] static int Main(string[] args){try{
  var g=new DataGrid{CanUserAddRows=false};var name=new DataGridTextColumn{SortMemberPath="Name"};g.Columns.Add(name);HistoryPresentation.Configure(g);
  g.ItemsSource=new List<AppRow>{new AppRow{Name="OPPO 互联",ProcessName="O+Connect.exe"},new AppRow{Name="Windows 服务主进程",ProcessName="svchost.exe"},new AppRow{Name="Windows 资源管理器",ProcessName="explorer.exe"},new AppRow{Name="Windows® 安装程序",ProcessName="msiexec.exe"},new AppRow{Name="哔哩哔哩",ProcessName="哔哩哔哩.exe"},new AppRow{Name="火绒安全软件托盘程序",ProcessName="HipsTray.exe"}};
  HistoryPresentation.ApplyOrder(g);Check("down-Chinese-leading-names-first",Order(g)=="哔哩哔哩.exe,HipsTray.exe,O+Connect.exe,svchost.exe,explorer.exe,msiexec.exe");
  name.SortDirection=ListSortDirection.Ascending;HistoryPresentation.ApplyOrder(g);Check("up-Latin-Z-A-before-Chinese",Order(g)=="msiexec.exe,explorer.exe,svchost.exe,O+Connect.exe,HipsTray.exe,哔哩哔哩.exe");
  g.ItemsSource=new List<AppRow>{new AppRow{Name="  中文工具",ProcessName="zeta.exe"},new AppRow{Name="Alpha 中文描述",ProcessName="Alpha.exe"}};name.SortDirection=ListSortDirection.Descending;HistoryPresentation.ApplyOrder(g);Check("leading-space-ignored-not-Chinese-suffix",Order(g)=="zeta.exe,Alpha.exe");
  var doc=XDocument.Load(Path.Combine(args[1],"installer","TrayLeaf.wxs"));XNamespace n="http://schemas.microsoft.com/wix/2006/wi";var product=doc.Root.Element(n+"Product");var package=product.Element(n+"Package");
  Check("installer-elevation-requested",(string)package.Attribute("InstallPrivileges")=="elevated"&&(string)package.Attribute("InstallScope")!="perMachine"&&(string)package.Attribute("InstallScope")!="perUser");
  Check("installer-user-context-retained",!product.Elements(n+"Property").Any(x=>(string)x.Attribute("Id")=="ALLUSERS"));
  var target=doc.Descendants(n+"Dialog").FirstOrDefault(x=>(string)x.Attribute("Id")=="TrayLeafInstallDirDlg");Check("target-folder-page-owned",target!=null);
  Check("no-separate-options-page",!doc.Descendants(n+"Dialog").Any(x=>(string)x.Attribute("Id")=="OptionsDlg"));
  Check("three-choices-on-target-page",target!=null&&target.Elements(n+"Control").Count(x=>(string)x.Attribute("Type")=="CheckBox")==3);
  Check("choices-beside-change-button",target!=null&&target.Elements(n+"Control").Where(x=>(string)x.Attribute("Type")=="CheckBox").All(x=>(int)x.Attribute("X")>=90&&(int)x.Attribute("Y")>=110&&(int)x.Attribute("Y")<150));
  var next=doc.Descendants(n+"Publish").Where(x=>(string)x.Attribute("Dialog")=="TrayLeafInstallDirDlg"&&(string)x.Attribute("Control")=="Next").ToList();
  Check("next-directly-executes-install",next.Any(x=>(string)x.Attribute("Event")=="EndDialog"&&(string)x.Attribute("Value")=="Return")&&!next.Any(x=>(string)x.Attribute("Event")=="NewDialog"));
  Check("folder-normalization-before-validation",next.Any(x=>(string)x.Attribute("Value")=="AppendProductFolder")&&next.Any(x=>(string)x.Attribute("Value")=="AppendProductFolderWithSlash"));
  Check("folder-normalization-in-silent-sequence",product.Element(n+"InstallExecuteSequence").Elements(n+"Custom").Count(x=>((string)x.Attribute("Action")??"").StartsWith("AppendProductFolder"))==2);
  var policy=typeof(AppRow).Assembly.GetType("TrayLeaf.CloseConfirmation");Check("confirmation-policy-present",policy!=null);
  if(policy!=null){var required=policy.GetMethod("Required",BindingFlags.Static|BindingFlags.NonPublic);var remember=policy.GetMethod("Remember",BindingFlags.Static|BindingFlags.NonPublic);string path=Path.Combine(args[0],"confirm.db");if(File.Exists(path))File.Delete(path);using(var db=new Database(path)){
   Check("confirmation-default-on",(bool)required.Invoke(null,new object[]{db}));remember.Invoke(null,new object[]{db,false,true});Check("cancel-does-not-suppress",(bool)required.Invoke(null,new object[]{db}));remember.Invoke(null,new object[]{db,true,false});Check("unchecked-does-not-suppress",(bool)required.Invoke(null,new object[]{db}));remember.Invoke(null,new object[]{db,true,true});Check("confirmed-suppression-saved",!(bool)required.Invoke(null,new object[]{db}));Check("force-warning-remains-independent",db.Get("confirmForce","1")=="1");}using(var db=new Database(path)){Check("suppression-persists-restart",!(bool)required.Invoke(null,new object[]{db}));db.Set("confirmClose","1");Check("settings-can-reenable",(bool)required.Invoke(null,new object[]{db}));}}
  Window shell;using(var stream=typeof(AppRow).Assembly.GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))shell=(Window)System.Windows.Markup.XamlReader.Load(stream);
  var input=shell.FindName("ConfirmCloseInput") as CheckBox;Check("settings-slider-for-close-confirmation",input!=null&&input.Style==shell.FindResource("Switch"));
  var build=typeof(ModernDialog).GetMethod("Build",BindingFlags.Static|BindingFlags.NonPublic);Check("testable-dialog-builder",build!=null);
  if(build!=null){foreach(string theme in new[]{"0","1"}){
   ThemeManager.Apply(shell,theme,0);
   var dialog=(Window)build.Invoke(null,new object[]{shell,"将关闭 12 个程序组\n\n请先保存工作，恢复只会重新启动程序，不恢复文件、窗口或会话","确认关闭所选程序",true,true});
   dialog.WindowStartupLocation=WindowStartupLocation.Manual;dialog.Left=-10000;dialog.Top=-10000;dialog.ShowActivated=false;dialog.Show();dialog.UpdateLayout();
   var box=Children(dialog).OfType<CheckBox>().FirstOrDefault(c=>c.Name=="SuppressConfirmation");
   Check("dialog-suppression-visible-theme-"+theme,box!=null&&box.Content.ToString()=="不再提示");
   Check("dialog-suppression-default-off-theme-"+theme,box!=null&&box.IsChecked!=true);
   Check("dialog-suppression-left-of-buttons-theme-"+theme,box!=null&&box.HorizontalAlignment==HorizontalAlignment.Left);
   Check("dialog-checkbox-text-color-theme-"+theme,box!=null&&box.Foreground==dialog.FindResource("Text"));
   Check("dialog-checkbox-text-measured-theme-"+theme,box!=null&&box.ActualWidth>75);
   if(box!=null){var yes=Children(dialog).OfType<Button>().First(b=>(b.Content as string)=="确认继续");Point a=box.TranslatePoint(new Point(box.ActualWidth,0),dialog),buttonStart=yes.TranslatePoint(new Point(),dialog);Check("dialog-controls-do-not-overlap-theme-"+theme,a.X<buttonStart.X);}
   var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(dialog.ActualWidth),(int)Math.Ceiling(dialog.ActualHeight),96,96,System.Windows.Media.PixelFormats.Pbgra32);
   var background=new System.Windows.Media.DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle((System.Windows.Media.Brush)dialog.FindResource("Canvas"),null,new Rect(0,0,image.PixelWidth,image.PixelHeight));image.Render(background);image.Render((System.Windows.Media.Visual)dialog.Content);
   var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var f=File.Create(Path.Combine(args[0],"confirmation-theme-"+theme+".png")))png.Save(f);dialog.Close();
  }}
 }catch(Exception e){failed++;Console.Error.WriteLine(e);}Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}
