using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using TrayLeaf;
class Version032ContractTests {
 static int passed,failed;
 static void Check(string n,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+n);if(ok)passed++;else failed++;}
 [STAThread]static int Main(string[] args){try{
 using(var db=new Database(Path.Combine(args[0],"contract.db"))){
  for(int i=0;i<14;i++){db.Batch(new List<AppRow>{new AppRow{Name="App "+i,Path="C:\\Example.exe",Args="--value \""+i+" 世界\"",Admin=i%2==0,Selected=true}});}
  db.Prune();Check("restore-batches-limit-10",db.Query("SELECT id FROM batches").Count==10);
  Check("strict-prune-prepared-rows",db.Query("SELECT id FROM items").Count==10);
  Check("close-history-is-separate",db.Query("SELECT name FROM sqlite_master WHERE name='saved_sets'").Count==1);
  Check("profile-storage-api",typeof(Database).GetMethod("SaveProfile")!=null);
  Check("launch-preference-api",typeof(Database).GetMethod("RememberLaunch")!=null);
  Check("item-selection-database-column",db.Query("PRAGMA table_info(items)").Any(x=>x[1]=="chosen"));
 }
 var app=new Application();Window w;using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))w=(Window)XamlReader.Load(s);
 var grid=(DataGrid)w.FindName("ProcessGrid");var restore=(DataGrid)w.FindName("RestoreGrid");
 Check("memory-occupancy-label",grid.Columns.Any(x=>Convert.ToString(x.Header)=="内存占用"));
 Check("no-duplicate-elevation-column",!grid.Columns.Any(x=>Convert.ToString(x.Header)=="恢复时提权"));
 Check("restore-list-navigation",Convert.ToString(((Button)w.FindName("NavRestore")).Content).Contains("恢复列表"));
 Check("close-history-picker",w.FindName("CloseHistoryCombo")!=null);
 Check("close-profiles-controls",w.FindName("CloseProfileCombo")!=null&&w.FindName("SaveCloseProfileButton")!=null);
 Check("restore-profiles-controls",w.FindName("RestoreProfileCombo")!=null&&w.FindName("SaveRestoreProfileButton")!=null);
 Check("restore-icon-template",restore.Columns.OfType<DataGridTemplateColumn>().Select(c=>c.CellTemplate.LoadContent()).OfType<System.Windows.Controls.Grid>().Any(g=>g.Children.OfType<Image>().Any()));
 w.Close();app.Shutdown();
 }catch(Exception ex){Console.Error.WriteLine(ex);failed++;}Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}
