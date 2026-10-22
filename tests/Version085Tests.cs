using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using TrayLeaf;
class Version085Tests {
 static int passed,failed;static void Check(string n,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+n);if(ok)passed++;else failed++;}
 static T Get<T>(Window w,string n)where T:class{return w.FindName(n)as T;}
 static object Call(object c,string n,params object[] a){return c.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,a);}
 static string Order(DataGrid g){return String.Join(",",g.Items.Cast<AppRow>().Select(r=>r.ProcessName));}
 static void Sort(DataGrid g,string key,ListSortDirection dir){foreach(var c in g.Columns)c.SortDirection=c.SortMemberPath==key?(ListSortDirection?)dir:null;HistoryPresentation.ApplyOrder(g);}
 [STAThread]static int Main(string[] args){try{
 var grid=new DataGrid{CanUserAddRows=false};foreach(string key in new[]{"Name","Source","MemoryMB","Risk","State"})grid.Columns.Add(new DataGridTextColumn{SortMemberPath=key});HistoryPresentation.Configure(grid);
 var rows=new List<AppRow>{new AppRow{Name="A title",ProcessName="zeta.exe",Source="任务栏窗口",MemoryMB=9,State="已关闭",Risk=false},new AppRow{Name="Z title",ProcessName="Alpha.exe",Source="通知区域",MemoryMB=1000,State="运行中",Risk=true},new AppRow{Name="M title",ProcessName="beta.exe",Source="任务栏窗口",MemoryMB=80,State="未运行",Risk=false}};
 grid.ItemsSource=rows;HistoryPresentation.ApplyOrder(grid);Check("default-display-A-Z",Order(grid)=="zeta.exe,beta.exe,Alpha.exe");
 Sort(grid,"Name",ListSortDirection.Ascending);Check("up-arrow-visible-title-Z-A",Order(grid)=="Alpha.exe,beta.exe,zeta.exe");
 Sort(grid,"MemoryMB",ListSortDirection.Descending);Check("memory-numeric-desc",Order(grid)=="Alpha.exe,beta.exe,zeta.exe");Sort(grid,"MemoryMB",ListSortDirection.Ascending);Check("memory-numeric-asc",Order(grid)=="zeta.exe,beta.exe,Alpha.exe");
 Sort(grid,"State",ListSortDirection.Ascending);Check("state-order-reversed-088",Order(grid)=="zeta.exe,beta.exe,Alpha.exe");Sort(grid,"State",ListSortDirection.Descending);Check("state-desc-reversed-088",Order(grid)=="Alpha.exe,beta.exe,zeta.exe");
 Sort(grid,"Source",ListSortDirection.Ascending);Check("source-groups-stable-name-ties",grid.Items.Cast<AppRow>().Where(r=>r.Source=="任务栏窗口").Select(r=>r.ProcessName).SequenceEqual(new[]{"zeta.exe","beta.exe"}));
 Sort(grid,"Risk",ListSortDirection.Ascending);Check("category-system-first-088",Order(grid)=="Alpha.exe,zeta.exe,beta.exe");Sort(grid,"Risk",ListSortDirection.Descending);Check("category-ordinary-first-desc-088",Order(grid)=="zeta.exe,beta.exe,Alpha.exe");
 var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Window w;using(var stream=typeof(Entry).Assembly.GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))w=(Window)XamlReader.Load(stream);
 using(var db=new Database(Path.Combine(args[0],"test085.db"))){db.Set("closeHotkey","0|0");db.Set("restoreHotkey","0|0");var c=new Controller(app,w,db,true);c.Start(null);
 Call(c,"CreateTray");var menu=(ContextMenu)typeof(Controller).GetField("trayMenu",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);Check("tray-no-separator",!menu.Items.OfType<Separator>().Any());
 Call(c,"SetBusy",true);Check("busy-current-grid-usable",Get<DataGrid>(w,"ProcessGrid").IsEnabled);Check("busy-restore-grid-usable",Get<DataGrid>(w,"RestoreGrid").IsEnabled);Check("busy-selection-usable",Get<Button>(w,"SelectAllButton").IsEnabled);Check("busy-navigation-usable",Get<Button>(w,"NavSettings").IsEnabled);Check("busy-overlap-disabled",!Get<Button>(w,"CloseButton").IsEnabled&&!Get<Button>(w,"RefreshButton").IsEnabled);Call(c,"SetBusy",false);
 var row=new AppRow{Name="fixture",ProcessName="fixture.exe",Path=Path.Combine(args[0],"fixture.exe"),Selected=true};db.SaveProfile("close","same",new List<AppRow>{row},0);Call(c,"ReloadCollections");var combo=Get<ComboBox>(w,"CloseProfileCombo");combo.SelectedIndex=0;Call(c,"ApplyProfile","close");var field=typeof(Controller).GetField("current",BindingFlags.Instance|BindingFlags.NonPublic);var list=(List<AppRow>)field.GetValue(c);list.Single(r=>r.Path==row.Path).Selected=false;
 typeof(ComboBox).GetMethod("OnDropDownOpened",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combo,new object[]{EventArgs.Empty});typeof(ComboBox).GetMethod("OnDropDownClosed",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combo,new object[]{EventArgs.Empty});Check("same-profile-reapplies",list.Single(r=>r.Path==row.Path).Selected);
 var pending=new AppRow{Path="C:\\missing.exe",ItemStatus="launched",State="成功发送请求"};bool stable=true;int calls=0;var monitor=new LaunchMonitor(()=>new HashSet<string>(),ms=>{calls++;if(calls>1)stable&=pending.State=="成功发送请求";return Task.FromResult(0);});var task=monitor.VerifyAsync(new[]{pending},new OperationService(db));while(!task.IsCompleted)w.Dispatcher.Invoke(new Action(()=>{}),System.Windows.Threading.DispatcherPriority.ApplicationIdle);task.GetAwaiter().GetResult();Check("pending-status-no-check-counter",stable&&calls==5&&pending.State=="启动失败");
 Call(c,"Exit");}
 }catch(Exception ex){failed++;Console.Error.WriteLine(ex);}Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}

