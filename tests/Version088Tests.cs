using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Xml.Linq;
using TrayLeaf;
class Version088Tests {
 static int passed,failed;
 static void Check(string label,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+label);if(ok)passed++;else failed++;}
 static string Order(DataGrid g){return String.Join(",",g.Items.Cast<AppRow>().Select(r=>r.ProcessName));}
 static void Click(DataGrid g,DataGridColumn c){typeof(DataGrid).GetMethod("OnSorting",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,new object[]{new DataGridSortingEventArgs(c)});}
 [STAThread] static int Main(string[] args){try{
  var grid=new DataGrid{CanUserAddRows=false};var name=new DataGridTextColumn{SortMemberPath="Name"};var source=new DataGridTextColumn{SortMemberPath="Source"};grid.Columns.Add(name);grid.Columns.Add(source);HistoryPresentation.Configure(grid);
  var rows=new List<AppRow>{new AppRow{Name="中文工具",ProcessName="zeta.exe",Source="任务栏窗口"},new AppRow{Name="English",ProcessName="beta.exe",Source="通知区域"},new AppRow{Name="English",ProcessName="Alpha.exe",Source="通知区域"}};grid.ItemsSource=rows;HistoryPresentation.ApplyOrder(grid);
  Check("default-down-arrow",name.SortDirection==ListSortDirection.Descending);
  Check("Chinese-display-name-before-English",Order(grid)=="zeta.exe,Alpha.exe,beta.exe");
  name.SortDirection=ListSortDirection.Descending;Click(grid,name);Check("first-header-click-keeps-shown-direction",name.SortDirection==ListSortDirection.Descending);Check("down-arrow-Chinese-first-English-A-Z",Order(grid)=="zeta.exe,Alpha.exe,beta.exe");
  Click(grid,name);Check("repeat-header-click-toggles",name.SortDirection==ListSortDirection.Ascending);
  Check("up-arrow-English-Z-A-Chinese-last",Order(grid)=="beta.exe,Alpha.exe,zeta.exe");
  Click(grid,source);Check("source-up-arrow-category-order",Order(grid)=="Alpha.exe,beta.exe,zeta.exe");Click(grid,source);Check("source-repeat-descending",source.SortDirection==ListSortDirection.Descending);Check("source-down-arrow-reversed-categories",Order(grid)=="zeta.exe,Alpha.exe,beta.exe");Click(grid,name);Click(grid,source);Check("returning-column-keeps-last-direction",source.SortDirection==ListSortDirection.Descending);
  name.SortDirection=null;source.SortDirection=null;HistoryPresentation.ApplyOrder(grid);Check("missing-sort-falls-back-to-Chinese-first-A-Z",Order(grid)=="zeta.exe,Alpha.exe,beta.exe");
  var installer=XDocument.Load(Path.Combine(args[1],"installer","TrayLeaf.wxs"));XNamespace wix="http://schemas.microsoft.com/wix/2006/wi";var product=installer.Root.Element(wix+"Product");
  Check("installer-new-product-code-per-build",(string)product.Attribute("Id")=="*");
  Check("installer-stable-upgrade-code",(string)product.Attribute("UpgradeCode")=="{49F5C6BF-41D9-5CF1-8FFA-917E45CCB2DC}");
  Check("installer-same-version-upgrades-enabled",(string)product.Element(wix+"MajorUpgrade").Attribute("AllowSameVersionUpgrades")=="yes");
  Check("installer-version-current",(string)product.Attribute("Version")=="0.9.2");
  Check("release-no-checksum-sidecar",!File.ReadAllText(Path.Combine(args[1],"build-release.ps1")).Contains(".sha256"));
  Check("installer-no-checksum-sidecar",!File.ReadAllText(Path.Combine(args[1],"installer","build-installer.ps1")).Contains(".sha256"));
  Check("Defender-recognized",Policy.IsAntivirus("MsMpEng.exe",@"C:\Program Files\Windows Defender\MsMpEng.exe"));Check("Norton-name-recognized",Policy.IsAntivirus("NortonSecurity.exe",""));Check("Tencent-QQ-not-antivirus",!Policy.IsAntivirus("QQ.exe",@"C:\Program Files\Tencent\QQ\QQ.exe"));Check("360-browser-not-antivirus",!Policy.IsAntivirus("360se.exe",@"C:\Program Files\360\360se\360se.exe"));
  var protectedRow=new AppRow{Name="Protected fixture",ProcessName="MsMpEng.exe",Path=@"C:\missing-test.exe",Antivirus=true,Selected=true};Check("copied-antivirus-flag",ProgramPresentation.Copy(protectedRow).Antivirus);
  using(var db=new Database(Path.Combine(args[0],"antivirus.db"))){var ops=new OperationService(db);Check("antivirus-close-blocked-in-service",ops.Close(protectedRow,r=>true)=="杀毒类软件不支持关闭喵");}
  var normalize=typeof(ProcessService).GetMethod("LaunchArguments",BindingFlags.Static|BindingFlags.NonPublic);Check("Windhawk-launch-policy-exists",normalize!=null);
  if(normalize!=null){Check("Windhawk-service-record-repaired",(string)normalize.Invoke(null,new object[]{@"C:\Windhawk\windhawk.exe","-service"})=="-tray-only");Check("Windhawk-tray-args-preserved",(string)normalize.Invoke(null,new object[]{@"C:\Windhawk\windhawk.exe","-tray-only"})=="-tray-only");Check("other-service-args-preserved",(string)normalize.Invoke(null,new object[]{@"C:\App\other.exe","-service --x"})=="-service --x");}
  var select=typeof(ProcessService).GetMethod("LaunchProcess",BindingFlags.Static|BindingFlags.NonPublic);Check("interactive-launch-selection-exists",select!=null);
  if(select!=null){var service=new ProcInfo{Id=10,Name="windhawk.exe",Service=true,Command="windhawk.exe -service"};var tray=new ProcInfo{Id=20,Parent=10,Name="windhawk.exe",Command="windhawk.exe -tray-only"};var selected=(ProcInfo)select.Invoke(null,new object[]{new List<ProcInfo>{service,tray},new HashSet<int>{20}});Check("interactive-process-preferred-to-service",Object.ReferenceEquals(selected,tray));}
 }catch(Exception e){failed++;Console.Error.WriteLine(e);}Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}
