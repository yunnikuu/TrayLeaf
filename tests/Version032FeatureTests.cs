using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TrayLeaf;
class Version032FeatureTests {
 static int passed,failed;static string dir,fixture,other;
 static void Check(string s,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+s);if(ok)passed++;else failed++;}
 static void Throws(string s,Action a){bool threw=false;try{a();}catch{threw=true;}Check(s,threw);}
 static T Get<T>(Window w,string n) where T:class{return (T)w.FindName(n);}
 static object Call(object target,string method,params object[] args){return target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);}
 static AppRow Row(string path,string args,bool admin,bool selected){return new AppRow{Name=Path.GetFileNameWithoutExtension(path),ProcessName=Path.GetFileName(path),Path=path,Args=args,Admin=admin,Selected=selected,ArgsKnown=true};}
 static IEnumerable<T> Find<T>(DependencyObject o) where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++){var c=VisualTreeHelper.GetChild(o,i);if(c is T)yield return (T)c;foreach(var v in Find<T>(c))yield return v;}}
 static void Idle(Window w){w.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));w.UpdateLayout();}
 static void Render(Window w,string name,double scale){Idle(w);var e=(FrameworkElement)w.Content;var bitmap=new RenderTargetBitmap((int)(e.ActualWidth*scale),(int)(e.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(e);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(dir,name)))enc.Save(f);Console.WriteLine("RENDER "+name+" "+bitmap.PixelWidth+"x"+bitmap.PixelHeight);}
 static void Store(){string file=Path.Combine(dir,"store.db");long keep;
 using(var db=new Database(file)){
  var sample=new List<AppRow>{Row(fixture,"--token \"中文 a b\"",true,true),Row(other,"--safe",false,false)};
  keep=db.SaveProfile("close","游戏前",sample,0);db.SaveProfile("restore","游戏后",sample,0);
  var copy=db.LoadSet(keep);Check("profile-exact-args-and-choices",copy.Count==2&&copy[0].Args==sample[0].Args&&copy[0].Admin&&copy[0].Selected&&!copy[1].Selected);
  sample[0].Args="changed";Check("profile-snapshot-independent",db.LoadSet(keep)[0].Args!="changed");
  for(int i=1;i<5;i++){db.SaveProfile("close","关闭 "+i,copy,0);db.SaveProfile("restore","恢复 "+i,copy,0);}
  Check("five-per-kind-independent",db.ListSets("profile_close").Count==5&&db.ListSets("profile_restore").Count==5);
  Throws("reject-sixth-close-profile",()=>db.SaveProfile("close","sixth",copy,0));Throws("reject-sixth-restore-profile",()=>db.SaveProfile("restore","sixth",copy,0));
  copy[0].Args="--updated";db.SaveProfile("close","已编辑",copy,keep);Check("update-at-limit",db.LoadSet(keep)[0].Args=="--updated"&&db.ListSets("profile_close").Count==5);
  Throws("invalid-name-rejected",()=>db.SaveProfile("close"," ",copy,keep));Check("failed-edit-preserves-profile",db.LoadSet(keep)[0].Args=="--updated");
  Throws("invalid-args-rejected",()=>db.SaveProfile("restore","Invalid",new[]{Row(fixture,"x\0y",false,true)},0));
  db.DeleteProfile("restore",keep);Check("delete-respects-kind",db.LoadSet(keep).Count==2);
  long latest=0;for(int i=0;i<16;i++){sample[0].Args="--batch "+i;latest=db.Batch(sample);}
  Check("restore-history-ten",db.Query("SELECT id FROM batches").Count==10);Check("close-history-ten",db.ListSets("history_close").Count==10);
  Check("history-latest-content",db.LoadSet(db.ListSets("history_close")[0].Id)[0].Args=="--batch 15");
  Check("history-items-cascade",db.Query("SELECT id FROM items").Count==20&&db.Query("SELECT id FROM items WHERE batch NOT IN(SELECT id FROM batches)").Count==0);
  Check("history-snapshots-cascade",db.Query("SELECT id FROM saved_entries WHERE set_id NOT IN(SELECT id FROM saved_sets)").Count==0);
  Check("profiles-survive-history-pruning",db.LoadSet(keep)[0].Args=="--updated");
  var item=new OperationService(db).History(latest)[0];item.Selected=false;item.Admin=true;item.Args="--remember \"sp ace\"";db.SaveHistoryChoice(item);db.RememberLaunch(item);
  Check("restore-choice-persists",!new OperationService(db).History(latest)[0].Selected);
  var saved=db.Query("SELECT args,admin FROM managed WHERE path=?",fixture)[0];Check("administrator-and-args-shared",saved[0]==item.Args&&saved[1]=="1"&&db.Get("args:"+fixture,"0")=="1");
  Check("selection-does-not-grow-settings",db.Query("SELECT key FROM settings WHERE key LIKE 'restore:%'").Count==0);
  int h=db.ListSets("history_close").Count;new OperationService(db).PrepareLaunch(new List<AppRow>{Row(fixture,"--launch-profile",false,true)});Check("profile-launch-records-new-restore-batch",db.Query("SELECT id FROM batches").Count==10);Check("launch-does-not-fake-close-history",db.ListSets("history_close").Count==h);
  long deleted=db.ListSets("profile_close").First(x=>x.Id!=keep).Id;db.DeleteProfile("close",deleted);Check("delete-cascades-entries",db.LoadSet(deleted).Count==0);db.SaveProfile("close","替补",copy,0);Check("deleted-slot-reusable",db.ListSets("profile_close").Count==5);
 }
 using(var db=new Database(file)){Check("reopen-profiles-persist",db.ListSets("profile_close").Count==5);Check("reopen-retention-persists",db.ListSets("history_close").Count==10&&db.Query("SELECT id FROM batches").Count==10);Check("foreign-key-integrity",db.Query("PRAGMA foreign_key_check").Count==0);Check("sqlite-integrity",db.Query("PRAGMA integrity_check")[0][0]=="ok");}
 // Simulate an older database schema, with a legacy deselection setting.
 string old=Path.Combine(dir,"old.db");using(var db=new Database(old)){
  db.Execute("DELETE FROM saved_entries");db.Execute("DELETE FROM saved_sets");db.Execute("DELETE FROM settings");db.Execute("DROP TABLE items");
  db.Execute("CREATE TABLE items(id INTEGER PRIMARY KEY AUTOINCREMENT,batch INTEGER NOT NULL REFERENCES batches(id) ON DELETE CASCADE,name TEXT NOT NULL,path TEXT NOT NULL,args TEXT NOT NULL,admin TEXT NOT NULL,status TEXT NOT NULL,error TEXT NOT NULL DEFAULT '')");
  db.Execute("INSERT INTO batches(created) VALUES('legacy')");db.Execute("INSERT INTO items(batch,name,path,args,admin,status) VALUES(1,'旧程序',?,'--legacy','1','closed')",fixture);db.Set("restore:1","0");db.Execute("PRAGMA user_version=2");
 }
 using(var db=new Database(old)){var row=new OperationService(db).History(1)[0];Check("legacy-items-preserved",row.Args=="--legacy"&&row.Admin&&row.CanRestore);Check("legacy-deselection-migrated",!row.Selected);Check("legacy-history-backfilled",db.ListSets("history_close").Count==1);Check("legacy-setting-cleaned",db.Query("SELECT key FROM settings WHERE key LIKE 'restore:%'").Count==0);Check("schema-version-three",db.Query("PRAGMA user_version")[0][0]=="3");}
 }
 delegate bool EnumWindow(IntPtr h,IntPtr p);
 [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")]static extern bool EnumThreadWindows(uint id,EnumWindow callback,IntPtr p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder text,int size);
 [DllImport("user32.dll")]static extern IntPtr GetDlgItem(IntPtr h,int id);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
 static void ConfirmDelete(Controller controller,bool yes){
  uint thread=GetCurrentThreadId();bool found=false;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};
  timer.Tick+=delegate{EnumThreadWindows(thread,delegate(IntPtr h,IntPtr unused){var text=new StringBuilder(128);GetWindowText(h,text,text.Capacity);if(text.ToString()=="删除配置"){var button=GetDlgItem(h,yes?6:7);if(button!=IntPtr.Zero){found=true;PostMessage(button,0x00F5,IntPtr.Zero,IntPtr.Zero);timer.Stop();}}return true;},IntPtr.Zero);};
  timer.Start();try{Call(controller,"DeleteProfile","restore");}finally{timer.Stop();}Check(yes?"delete-confirmation-yes-dialog":"delete-confirmation-no-dialog",found);
 }
 static void LaunchFlow(){
  using(var db=new Database(Path.Combine(dir,"launch-flow.db"))){
   string log=Path.Combine(dir,"profile-launch.txt"),skip=Path.Combine(dir,"not-selected.txt");
   long id=db.SaveProfile("restore","启动流程",new[]{Row(fixture,"--log \""+log+"\" --marker \"profile 世界\"",false,true),Row(other,"--log \""+skip+"\"",true,false)},0);
   var rows=db.LoadSet(id);foreach(var r in rows)r.ItemStatus="configured";var chosen=rows.Where(r=>r.Selected&&r.CanRestore).ToList();var ops=new OperationService(db);ops.PrepareLaunch(chosen);
   Check("profile-exec-only-selected-recorded",db.Query("SELECT id FROM items").Count==1);
   string result=ops.Restore(chosen[0]);var clock=Stopwatch.StartNew();while(!File.Exists(log)&&clock.ElapsedMilliseconds<6000)Thread.Sleep(50);
   Check("profile-exec-normal-launch-success",result=="已发送启动请求"&&File.Exists(log));
   if(File.Exists(log)){string contents=File.ReadAllText(log);int pid=Int32.Parse(contents.Split('\n')[0]);using(var child=Process.GetProcessById(pid)){try{Check("profile-exec-exact-arguments",contents.Contains("--marker \"profile 世界\""));Check("profile-exec-unselected-not-launched",!File.Exists(skip));Check("profile-exec-admin-choice-retained",!chosen[0].Admin&&db.LoadSet(id)[1].Admin);Check("profile-exec-state-launched",chosen[0].ItemStatus=="launched");}finally{if(!child.HasExited&&String.Equals(child.MainModule.FileName,fixture,StringComparison.OrdinalIgnoreCase)){child.Kill();child.WaitForExit(4000);}}}}
  }
 }
 static void Ui(){var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};using(var db=new Database(Path.Combine(dir,"ui.db"))){
 Window w;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))w=(Window)XamlReader.Load(stream);
 var c=new Controller(app,w,db,true);c.Start(null);Idle(w);
 var entries=new List<AppRow>{Row(fixture,"--one",true,true),Row(other,"--two",false,false)};long batch=db.Batch(entries);foreach(var id in db.Query("SELECT id FROM items"))db.Execute("UPDATE items SET status='closed' WHERE id=?",id[0]);
 Call(c,"ReloadHistory",batch);Call(c,"Navigate",1);Idle(w);var g=Get<DataGrid>(w,"RestoreGrid");var rows=g.ItemsSource.Cast<AppRow>().ToList();Check("restore-icons-all-loaded",rows.All(r=>r.Icon!=null));Check("restore-batch-count-only",Get<ComboBox>(w,"BatchCombo").SelectedItem.ToString().EndsWith("2 项")&&!Get<ComboBox>(w,"BatchCombo").SelectedItem.ToString().Contains("待恢复"));
 rows[0].Admin=false;rows[0].Args="--changed in UI";rows[0].Selected=false;Check("restore-grid-edits-persist",new OperationService(db).History(batch)[0].Args=="--changed in UI"&&!new OperationService(db).History(batch)[0].Admin&&!new OperationService(db).History(batch)[0].Selected);
 Check("restore-preferences-follow-to-next-scan",db.Query("SELECT args,admin FROM managed WHERE path=?",fixture)[0].SequenceEqual(new[]{"--changed in UI","0"}));
 var dgrow=(DataGridRow)g.ItemContainerGenerator.ContainerFromItem(rows[0]);Check("row-context-menu-two-level",dgrow!=null&&dgrow.ContextMenu!=null&&((MenuItem)dgrow.ContextMenu.Items[0]).Items.Count==2);
 var menu=ProgramPresentation.PathMenu(Row("", "",false,false),s=>{});Check("missing-path-menu-disabled",((MenuItem)((MenuItem)menu.Items[0]).Items[0]).IsEnabled==false);
 var profileId=db.SaveProfile("restore","日常恢复",entries,0);Call(c,"ReloadCollections");Call(c,"ApplyProfile","restore");Idle(w);rows=g.ItemsSource.Cast<AppRow>().ToList();Check("apply-profile-keeps-start-selection",rows[0].Selected&&!rows[1].Selected);Check("apply-profile-launchable-not-auto-executed",rows.All(x=>x.CanRestore&&x.ItemId==0)&&rows[0].Admin&&rows[0].Args=="--one");
 Check("restore-checkboxes-center",Find<CheckBox>(g).Where(x=>x.DataContext is AppRow).All(x=>x.HorizontalAlignment==HorizontalAlignment.Center&&x.VerticalAlignment==VerticalAlignment.Center));Render(w,"restore-150pct.png",1.5);
 Call(c,"Navigate",0);Call(c,"ApplyCloseRows",entries,"测试配置");Idle(w);var cg=Get<DataGrid>(w,"ProcessGrid");var cr=cg.ItemsSource.Cast<AppRow>().First(x=>x.Path==fixture);Check("close-profile-path-merge",cr.Selected&&cr.Args=="--one"&&cr.Admin);Check("close-profile-new-row-needs-safe-scan",cr.Processes.Count==0&&cr.State=="请刷新识别");
 Call(c,"CompleteClose",batch,2,2);Idle(w);Check("close-completion-stays-current-page",Get<Grid>(w,"CurrentPanel").Visibility==Visibility.Visible&&Get<Grid>(w,"RestorePanel").Visibility==Visibility.Collapsed);Check("close-success-inline",Get<TextBlock>(w,"StatusText").Text=="共计 2 个程序，已全部关闭。");Call(c,"CompleteClose",batch,1,2);Check("partial-close-inline-accurate",Get<TextBlock>(w,"StatusText").Text.Contains("未关闭 1 个"));
 var mem=cg.Columns.First(x=>Convert.ToString(x.Header)=="内存占用");Check("memory-label-and-numeric-sort",mem.SortMemberPath=="MemoryMB");Check("duplicate-admin-column-removed",!cg.Columns.Any(x=>Convert.ToString(x.Header)=="恢复时提权"));Render(w,"current-150pct.png",1.5);
 w.Width=960;w.Height=640;Idle(w);foreach(string n in new[]{"CloseHistoryCombo","SaveCloseProfileButton","DeleteCloseProfileButton","CloseButton"}){var e=Get<FrameworkElement>(w,n);var point=e.TranslatePoint(new Point(),(UIElement)w.Content);Check("minimum-visible-"+n,point.X>=0&&point.Y>=0&&point.X+e.ActualWidth<=((FrameworkElement)w.Content).ActualWidth+1&&point.Y+e.ActualHeight<=((FrameworkElement)w.Content).ActualHeight+1);}Render(w,"current-minimum.png",1);
 var editor=new ProfileEditor(w,"restore","编辑测试",entries,(name,values)=>{});editor.Show();Idle(editor);var field=typeof(ProfileEditor).GetField("entries",BindingFlags.NonPublic|BindingFlags.Instance);var editorRows=((IEnumerable<AppRow>)field.GetValue(editor)).ToList();editorRows[0].Args="draft";Check("profile-editor-cancel-isolated",entries[0].Args=="--one");Render(editor,"profile-editor.png",1.5);editor.Close();
 var modal=new ProfileEditor(w,"restore","可保存配置",entries,(name,values)=>db.SaveProfile("restore",name,values,0));modal.Loaded+=delegate{modal.Dispatcher.BeginInvoke(new Action(delegate{Find<Button>(modal).First(b=>Convert.ToString(b.Content)=="保存配置").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}));};Check("profile-editor-save-dialog",modal.ShowDialog()==true);Check("profile-editor-persisted",db.ListSets("profile_restore").Any(x=>x.Name=="可保存配置"));Call(c,"ReloadCollections");Call(c,"Navigate",1);int beforeDelete=db.ListSets("profile_restore").Count;ConfirmDelete(c,false);Check("delete-cancel-keeps-profile",db.ListSets("profile_restore").Count==beforeDelete);ConfirmDelete(c,true);Check("delete-confirm-removes-profile",db.ListSets("profile_restore").Count==beforeDelete-1);
 Call(c,"Exit");}app.Shutdown();}
 [STAThread]static int Main(string[] args){dir=Path.GetFullPath(args[0]);fixture=Path.Combine(dir,"TrayLeaf.ControlledFixture.exe");other=Path.Combine(dir,"SecondFixture.exe");File.Copy(fixture,other,true);try{Store();LaunchFlow();Ui();}catch(Exception ex){failed++;Console.Error.WriteLine(ex);}Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}
