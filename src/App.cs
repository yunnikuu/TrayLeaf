using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace TrayLeaf {
 public sealed class HotkeySpec {
  public uint Mods,Key;
  public string Code {get{return Mods+"|"+Key;}}
  public static HotkeySpec Parse(string text){var p=text.Split('|');uint m,k;if(p.Length!=2||!UInt32.TryParse(p[0],out m)||!UInt32.TryParse(p[1],out k)||m>15||k>254)throw new FormatException("无效快捷键");if(k!=0&&(m&11)==0)throw new FormatException("快捷键至少包含 Ctrl、Alt 或 Win");return new HotkeySpec{Mods=m,Key=k};}
  public override string ToString(){if(Key==0)return "未设置";return ((Mods&2)!=0?"Ctrl + ":"")+((Mods&1)!=0?"Alt + ":"")+((Mods&4)!=0?"Shift + ":"")+((Mods&8)!=0?"Win + ":"")+(Key>=48&&Key<=57?((char)Key).ToString():KeyInterop.KeyFromVirtualKey((int)Key).ToString());}
 }
 public static class Entry {
  [STAThread] public static int Main(string[] args) {
   RuntimeConfiguration.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TrayLeaf.config"));return Run(args);
  }
  [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
  static int Run(string[] args) {
   string smoke=Value(args,"--ui-smoke"),database=Value(args,"--db");bool test=smoke!=null;Mutex mutex=null;
   try {
    int installAction=StartupManager.InstallerAction(args);if(installAction>=0)return installAction;
    int bootstrap=StartupManager.Bootstrap(args);if(bootstrap>=0)return bootstrap;
    if(!test){try{StartupManager.ReconcileInstalledStartup();}catch{}bool created;mutex=new Mutex(true,"Local\\TrayLeaf."+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value,out created);if(!created){StartupNotice.Show("轻羽归栈已经运行，请从通知区域打开","轻羽归栈");return 0;}}
    var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
    if(database==null)database=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TrayLeaf","trayleaf.db");
    using(var db=new Database(database)) {
     using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayLeaf.MainWindow.xaml")) {
      var window=(Window)XamlReader.Load(stream);app.MainWindow=window;
      var controller=new Controller(app,window,db,test);controller.Start(smoke);app.Run();
     }
    }
    return 0;
   }catch(Exception ex){string message=ex.ToString();try{string log=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TrayLeaf","last-error.txt");Directory.CreateDirectory(Path.GetDirectoryName(log));File.WriteAllText(log,message);}catch{}if(test&&smoke!=null)File.WriteAllText(smoke+".error.txt",message);else StartupNotice.Show("启动失败："+ex.Message+"\n详细记录：%LOCALAPPDATA%\\TrayLeaf\\last-error.txt","轻羽归栈");return 1;}
   finally{if(mutex!=null)mutex.Dispose();}
  }
  static string Value(string[] a,string key){int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null;}
 }
 public sealed class BatchInfo {public long Id;public string Label;public override string ToString(){return Label;}}
 public sealed partial class Controller {
  readonly Application app;readonly Window w;readonly Database db;readonly OperationService ops;readonly bool demo;
  List<AppRow> current=new List<AppRow>(),history=new List<AppRow>();Forms.NotifyIcon tray;System.Drawing.Icon trayIcon;
  bool busy,exiting,loading,initializing=true;IntPtr handle;HwndSource source;HotkeySpec closeKey,restoreKey,editClose,editRestore;
  DispatcherTimer picker;bool pickReleased;int page;string scanNote="";
  T Get<T>(string name) where T:class {return (T)w.FindName(name);}
  void Text(string name,string text){Get<TextBlock>(name).Text=text;}
  bool Setting(string key,bool fallback){return db.Get(key,fallback?"1":"0")=="1";}
  void Click(string name,Action action){Get<Button>(name).Click+=delegate{Guard(action);};}
  void Guard(Action action){try{action();}catch(Exception ex){Error(ex);}}
  void Error(Exception ex){Show();ModernDialog.Confirm(w,ex.Message,"操作未完成",false);Text("StatusText",ex.Message);}
  bool Ask(string message,string title){Show();return ModernDialog.Confirm(w,message,title,true);}
  bool AskClose(string message,string title){if(!CloseConfirmation.Required(db))return true;Show();bool suppress;bool confirmed=ModernDialog.ConfirmClose(w,message,title,out suppress);CloseConfirmation.Remember(db,confirmed,suppress);Get<CheckBox>("ConfirmCloseInput").IsChecked=CloseConfirmation.Required(db);return confirmed;}
  bool AskOptional(string message,string title,string key,string input){if(!ConfirmationPreference.Required(db,key))return true;Show();bool suppress;bool confirmed=ModernDialog.ConfirmOptional(w,message,title,out suppress);ConfirmationPreference.Remember(db,key,confirmed,suppress);Get<CheckBox>(input).IsChecked=ConfirmationPreference.Required(db,key);return confirmed;}
  void Status(string text){Text("StatusText",text);}
  public Controller(Application application,Window window,Database database,bool preview){app=application;w=window;db=database;ops=new OperationService(db);demo=preview;}
  public void Start(string smokePath) {
   WindowSizing.ApplyDefault(w,SystemParameters.WorkArea);
   w.Icon=AppIdentity.WindowIcon;Get<Image>("BrandIcon").Source=AppIdentity.BrandImage;
   w.FontFamily=Appearance.Resolve(db.Get("fontFamily",Appearance.DefaultFont));
   closeKey=HotkeySpec.Parse(db.Get("closeHotkey","3|49"));restoreKey=HotkeySpec.Parse(db.Get("restoreHotkey","3|50"));editClose=closeKey;editRestore=restoreKey;
   Get<TextBox>("CloseHotkeyInput").Text=editClose.ToString();Get<TextBox>("RestoreHotkeyInput").Text=editRestore.ToString();
   Get<CheckBox>("ConfirmCloseInput").IsChecked=CloseConfirmation.Required(db);Get<CheckBox>("ConfirmHideInput").IsChecked=ConfirmationPreference.Required(db,"confirmHide");Get<CheckBox>("ConfirmForceInput").IsChecked=ConfirmationPreference.Required(db,"confirmForce");Get<CheckBox>("ShowCandidates").IsChecked=Setting("candidates",false);
   Get<CheckBox>("MinimizeToTray").IsChecked=Setting("minimizeTray",true);Get<CheckBox>("LaunchHidden").IsChecked=Setting("launchHidden",false);
   Text("DbPathText",db.FilePath);Text("PermissionText",demo?"界面预览 · 示例数据":ProcessService.Elevated?"管理员模式":"普通权限 · 部分进程受限");UpdateKeys();
   Get<ComboBox>("ScanMode").SelectedIndex=Setting("allProcesses",false)?1:0;
   Get<ComboBox>("FontFamilyInput").SelectionChanged+=delegate{var f=Get<ComboBox>("FontFamilyInput").SelectedValue as string;if(f!=null)Get<TextBlock>("FontPreview").FontFamily=Appearance.Resolve(f);};
   Click("ResetFontButton",delegate{EnsureFonts();Get<ComboBox>("FontFamilyInput").SelectedValue=Appearance.DefaultFont;});
   Click("NavCurrent",delegate{Navigate(0);});Click("NavRestore",delegate{if(!busy)ReloadHistory(0);Navigate(1);});Click("NavSettings",delegate{Navigate(2);});
   Click("RefreshButton",delegate{Refresh();});Click("AddButton",AddFile);Click("PickButton",PickWindow);
   Click("SelectAllButton",delegate{foreach(var r in current.Where(Matches))if(r.CanSelect&&!r.Risk&&r.Source!="托盘候选"&&r.Processes.Count>0)r.Selected=true;});
   Click("SelectNoneButton",delegate{foreach(var r in current.Where(Matches))r.Selected=false;});
   Click("CloseButton",delegate{CloseSelected();});Click("RestoreButton",delegate{RestoreSelected();});
   Click("EditButton",delegate{EditRow(Get<DataGrid>("ProcessGrid").SelectedItem as AppRow,false);});Click("EditRestoreButton",delegate{EditRow(Get<DataGrid>("RestoreGrid").SelectedItem as AppRow,true);});
   Click("ForgetButton",Forget);Click("ManageHiddenButton",ManageHidden);Click("ReloadHistoryButton",delegate{ReloadHistory(0);});Click("RefreshRestoreButton",RefreshRestore);
   Click("SelectRestoreButton",delegate{foreach(var r in history)r.Selected=r.CanRestore;});Click("SaveSettingsButton",SaveSettings);Click("ExitButton",Exit);
   Get<TextBox>("Search").TextChanged+=delegate{ApplyFilter();};Get<ComboBox>("ScanMode").SelectionChanged+=delegate{if(!initializing){db.Set("allProcesses",Get<ComboBox>("ScanMode").SelectedIndex==1?"1":"0");Refresh();}};
   Get<ComboBox>("BatchCombo").SelectionChanged+=delegate{if(!loading)Guard(LoadHistory);};
   Get<TextBox>("CloseHotkeyInput").PreviewKeyDown+=delegate(object s,KeyEventArgs e){CaptureKey(s as TextBox,e,true);};
   Get<TextBox>("RestoreHotkeyInput").PreviewKeyDown+=delegate(object s,KeyEventArgs e){CaptureKey(s as TextBox,e,false);};
   w.Closing+=delegate(object s,CancelEventArgs e){if(!exiting){e.Cancel=true;w.Hide();}};
   w.StateChanged+=delegate{if(!demo&&w.WindowState==WindowState.Minimized&&Setting("minimizeTray",true))w.Hide();};
   w.SourceInitialized+=delegate{handle=new WindowInteropHelper(w).Handle;source=HwndSource.FromHwnd(handle);source.AddHook(Hook);if(!demo)try{RegisterKeys(closeKey,restoreKey);}catch(Exception ex){Status("快捷键未启用："+ex.Message);w.Dispatcher.BeginInvoke(new Action(delegate{Error(ex);}));}};
   SetupModern();
   SetupCollections();Get<DataGrid>("ProcessGrid").PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){var dep=e.OriginalSource as DependencyObject;var row=ItemsControl.ContainerFromElement(Get<DataGrid>("ProcessGrid"),dep) as DataGridRow;var item=row==null?null:row.Item as AppRow;if(item!=null&&item.Antivirus){e.Handled=true;Show();ModernDialog.Confirm(w,"杀毒类软件不支持关闭喵","无法关闭",false);}};
   TablePresentation.Configure(w,Get<DataGrid>("ProcessGrid"),Get<DataGrid>("RestoreGrid"));
   app.DispatcherUnhandledException+=delegate(object s,DispatcherUnhandledExceptionEventArgs e){e.Handled=true;Error(e.Exception);};
   initializing=false;
   if(demo){FillDemo();w.Show();if(smokePath==null)return;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1000)};timer.Tick+=delegate{timer.Stop();try{w.UpdateLayout();var content=(FrameworkElement)w.Content;var bitmap=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(smokePath))encoder.Save(f);}catch(Exception ex){File.WriteAllText(smokePath+".error.txt",ex.ToString());}exiting=true;w.Close();app.Shutdown();};timer.Start();return;}
   ops.RecoverInterrupted();db.Prune();CreateTray();w.Show();if(Setting("launchHidden",false)||Environment.GetCommandLineArgs().Contains("--startup"))w.Hide();Refresh();ReloadHistory(0);
  }
  void Show(){if(!w.IsVisible)w.Show();if(w.WindowState==WindowState.Minimized)w.WindowState=WindowState.Normal;w.Activate();}
  void EnsureFonts(){var combo=Get<ComboBox>("FontFamilyInput");if(combo.ItemsSource!=null)return;combo.ItemsSource=Appearance.InstalledFonts();combo.SelectedValue=w.FontFamily.Source;}
  void Navigate(int n){if(n==2){EnsureFonts();UpdateHiddenCount();}page=n;
   Get<Grid>("CurrentPanel").Visibility=n==0?Visibility.Visible:Visibility.Collapsed;
   Get<Grid>("RestorePanel").Visibility=n==1?Visibility.Visible:Visibility.Collapsed;
   Get<Grid>("SettingsHost").Visibility=n==2?Visibility.Visible:Visibility.Collapsed;
   Get<ScrollViewer>("AboutPanel").Visibility=n==3?Visibility.Visible:Visibility.Collapsed;
   Text("PageTitle",new[]{"给重要的事，留出空间","刚才的后台，随时回来","按你的习惯，轻装运行","关于轻羽归栈"}[n]);
   Text("PageSubtitle",new[]{"选择暂时休息的应用，结束后按需找回","选择需要开启的应用，参数与权限随记录保存","让工具融入你的桌面与使用习惯","轻一点的后台，自在一点的桌面"}[n]);PaintNavigation();
  }
  ContextMenu trayMenu;
  void CreateTray(){
   trayIcon=AppIdentity.CreateTrayIcon();
   tray=new Forms.NotifyIcon{Icon=trayIcon,Text="轻羽归栈 · 后台暂时休息",Visible=true};
   trayMenu=new ContextMenu{Resources=w.Resources,Placement=System.Windows.Controls.Primitives.PlacementMode.MousePoint};
   AddTrayMenu("打开轻羽归栈",Show);AddTrayMenu("关闭所选程序",delegate{HotkeyAction(false);});AddTrayMenu("恢复所选程序",delegate{HotkeyAction(true);});
   AddTrayMenu("设置",delegate{Show();Navigate(2);});AddTrayMenu("关于",delegate{Show();Navigate(3);});AddTrayMenu("退出程序",Exit);
   tray.MouseUp+=delegate(object sender,Forms.MouseEventArgs e){if(e.Button==Forms.MouseButtons.Right)w.Dispatcher.BeginInvoke(new Action(delegate{Native.SetForegroundWindow(handle);trayMenu.IsOpen=true;}));};
   tray.DoubleClick+=delegate{w.Dispatcher.BeginInvoke(new Action(Show));};
   SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(w.Dispatcher));
  }
  void AddTrayMenu(string title,Action action){var item=new MenuItem{Header=title};item.Click+=delegate{Guard(action);};trayMenu.Items.Add(item);}
  void Exit(){if(busy){Status("请等待当前操作结束后退出");Show();return;}exiting=true;if(picker!=null)picker.Stop();if(handle!=IntPtr.Zero){Native.UnregisterHotKey(handle,1);Native.UnregisterHotKey(handle,2);}if(tray!=null){tray.Visible=false;if(trayMenu!=null)trayMenu.IsOpen=false;tray.Dispose();}if(trayIcon!=null)trayIcon.Dispose();w.Close();app.Shutdown();}
  void SetBusy(bool value){busy=value;
   // Keep tables, navigation, search and selection interactive; serialize commands that alter the operation's data set.
   foreach(string name in new[]{"ApplyCloseProfileButton","SaveCloseProfileButton","EditCloseProfileButton","DeleteCloseProfileButton","SaveCloseHistoryProfileButton","ApplyRestoreProfileButton","SaveRestoreProfileButton","EditRestoreProfileButton","DeleteRestoreProfileButton","RefreshButton","AddButton","PickButton","CloseButton","RestoreButton","EditButton","EditRestoreButton","ForgetButton","ManageHiddenButton","SaveSettingsButton","ReloadHistoryButton","RefreshRestoreButton"})Get<Button>(name).IsEnabled=!value;
   Get<ComboBox>("ScanMode").IsEnabled=!value;Get<ComboBox>("BatchCombo").IsEnabled=!value;foreach(string c in new[]{"CloseHistoryCombo","CloseProfileCombo","RestoreProfileCombo"})Get<ComboBox>(c).IsEnabled=!value;
  }
  async void Refresh(){if(busy||demo)return;SetBusy(true);Status("正在识别程序与启动信息…");try{await Scan();}catch(Exception ex){Error(ex);}finally{SetBusy(false);}}
  internal Func<bool,bool,IEnumerable<string>,ScanResult> scanPrograms=ProcessService.Scan;
  async Task Scan(){bool all=Get<ComboBox>("ScanMode").SelectedIndex==1;bool candidates=Setting("candidates",false);
   var custom=db.Query("SELECT key FROM settings WHERE key LIKE 'custom:%' AND value='1'").Select(x=>x[0].Substring(7)).ToList();
   var result=await Task.Run(()=>{
    var snapshot=scanPrograms(all,candidates,custom);
    var saved=db.Query("SELECT path,name,args,admin,chosen FROM managed").ToDictionary(x=>x[0],x=>x,StringComparer.OrdinalIgnoreCase);
    var argsKeys=new HashSet<string>(db.Query("SELECT key FROM settings WHERE key LIKE 'args:%' AND value='1'").Select(x=>x[0].Substring(5)),StringComparer.OrdinalIgnoreCase);
    foreach(string path in custom.Where(path=>!snapshot.Rows.Any(x=>String.Equals(x.Path,path,StringComparison.OrdinalIgnoreCase)))){string[] data;saved.TryGetValue(path,out data);snapshot.Rows.Add(new AppRow{Name=data==null?Path.GetFileNameWithoutExtension(path):data[1],ProcessName=Path.GetFileName(path),Path=path,Source="自定义",State="未运行",ArgsKnown=true});}
    snapshot.Rows=db.FilterVisible(snapshot.Rows);
    foreach(var row in snapshot.Rows){row.Antivirus=Policy.IsAntivirus(row.ProcessName,row.Path);string[] v;if(saved.TryGetValue(row.Path??"",out v)){row.Admin=v[3]=="1";row.Selected=v[4]=="1";if(argsKeys.Contains(row.Path))row.Args=v[2];}if(row.Source=="托盘候选"||row.Antivirus)row.Selected=false;}
    Parallel.ForEach(snapshot.Rows,new ParallelOptions{MaxDegreeOfParallelism=4},row=>ProgramPresentation.LoadIcon(row));return snapshot;
   });
   // Read the latest UI choices after awaiting so edits made during scanning are retained.
   var latest=current.Where(r=>!String.IsNullOrWhiteSpace(r.Path)).GroupBy(r=>r.Path,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Last(),StringComparer.OrdinalIgnoreCase);
   foreach(var row in result.Rows){AppRow old;if(latest.TryGetValue(row.Path??"",out old)){row.Selected=!row.Antivirus&&old.Selected;row.Admin=old.Admin;if(old.Args!=null)row.Args=old.Args;row.ClosePriority=old.ClosePriority;}Wire(row,false);}
   current=result.Rows;
   scanNote="刷新于 "+DateTime.Now.ToString("HH:mm:ss")+" · 按 EXE 路径分组，同路径全部实例一并关闭";
   if(result.Candidates>0)scanNote+=" "+result.Candidates+" 个历史托盘候选"+(candidates?"已显示，默认不勾选":"未计入，可在设置中显示");
   if(!String.IsNullOrWhiteSpace(result.Note))scanNote+=" "+result.Note;ApplyHistoryPins();Get<DataGrid>("ProcessGrid").ItemsSource=current;ApplyFilter();Status(scanNote);
  }
  void LoadIcon(AppRow row){ProgramPresentation.LoadIcon(row);}
  bool syncingPreferences;
  void Wire(AppRow row,bool past){row.PropertyChanged+=delegate(object sender,PropertyChangedEventArgs e){
   if(syncingPreferences||(e.PropertyName!="Selected"&&e.PropertyName!="Admin"&&e.PropertyName!="Args"))return;
   try{
    if(past){if(row.ItemStatus=="configured"&&e.PropertyName=="Selected")row.State=row.Selected?"配置 · 将启动":"配置 · 不启动";db.SaveHistoryChoice(row);if(e.PropertyName!="Selected")db.RememberLaunch(row);}
    else if(!String.IsNullOrEmpty(row.Path)){db.SaveManaged(row.Path,row.Name,row.Args,row.Admin,row.Selected);if(e.PropertyName=="Args")db.Set("args:"+row.Path,"1");}
    if(e.PropertyName!="Selected"){
     syncingPreferences=true;try{foreach(var other in current.Concat(history).Where(x=>!Object.ReferenceEquals(x,row)&&String.Equals(x.Path,row.Path,StringComparison.OrdinalIgnoreCase))){other.Args=row.Args;other.Admin=row.Admin;if(other.ItemId>0)db.SaveHistoryChoice(other);}}finally{syncingPreferences=false;}
    }
    if(!past)Selection();
   }catch(Exception ex){Status("设置未保存："+ex.Message);}
  };}
  bool Matches(AppRow row){string q=Get<TextBox>("Search").Text.Trim();return q.Length==0||((row.Name??"")+" "+row.ProcessName+" "+row.Path).IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0;}
  void ApplyFilter(){if(Get<DataGrid>("ProcessGrid").ItemsSource==null)return;var view=CollectionViewSource.GetDefaultView(Get<DataGrid>("ProcessGrid").ItemsSource);view.Filter=o=>Matches((AppRow)o);HistoryPresentation.ApplyOrder(Get<DataGrid>("ProcessGrid"));Text("CountText",current.Count(Matches)+" 个程序组 / 共 "+current.Count+" 组");Selection();}
  void Selection(){var picked=current.Where(x=>x.Selected).ToList();Text("SelectionText",picked.Count==0?"尚未选择程序":"已选 "+picked.Count+" 组 · 工作集约 "+picked.Sum(x=>x.MemoryMB).ToString("N0")+" MB");}
  async void CloseSelected(){if(busy||demo)return;SetBusy(true);try{await CloseCore();}catch(Exception ex){Error(ex);}finally{SetBusy(false);}}
  async Task CloseCore(){
   if(current.Any(x=>x.Selected&&x.State=="请刷新识别"))await Scan();
   var rows=db.FilterVisible(current).Where(x=>x.Selected&&!x.Antivirus&&!Policy.IsAntivirus(x.ProcessName,x.Path)&&x.Processes.Count>0).ToList();if(rows.Count==0){Show();Navigate(0);Status("请先勾选正在运行的程序；未关闭任何程序");return;}
   var risks=rows.Where(x=>x.Risk).ToList();
   string riskNotice=risks.Count==0?"":"\n\n包含系统 / 服务进程："+String.Join("、",risks.Select(x=>x.Name))+"\n关闭可能中断服务、导致黑屏、注销或系统崩溃";
   if(!AskClose("将关闭 "+rows.Count+" 个程序组（相同 EXE 路径的全部实例）\n\n"+String.Join("\n",rows.Take(12).Select(x=>"• "+x.Name))+"\n\n请先保存工作,未保存的数据可能丢失！"+riskNotice+"\n\n恢复只会重新启动程序，不恢复文件、窗口或会话","确认关闭所选程序"))return;
   if(rows.Any(x=>!x.ArgsKnown)&&!Ask("部分程序的启动参数无法读取；已知路径仍会保存，但重新启动的行为可能与原来不同\n可取消后通过“启动信息”手动补充参数；是否继续？","启动信息不完整"))return;
   var commands=rows.Select(ProgramPresentation.Command).ToList();long batch=ops.Prepare(commands);
   var limiter=new SemaphoreSlim(4);var promptGate=new object();
   try{await Task.WhenAll(rows.Select(async (row,index)=>{var command=commands[index];row.State="正在关闭…";row.ItemId=command.ItemId;await limiter.WaitAsync();try{
    string result=await Task.Run(()=>ops.Close(command,r=>{lock(promptGate)return (bool)w.Dispatcher.Invoke(new Func<bool>(()=>AskOptional(r.Name+" 属于系统 / 服务进程，退出请求后仍在运行\n强制结束可能中断服务、导致黑屏或系统崩溃，也可能丢失未保存数据；是否仍要强制结束？","确认强制关闭","confirmForce","ConfirmForceInput")));}));
    row.ItemStatus=command.ItemStatus;row.Error=command.Error;row.State=result;if(row.ItemStatus=="closed")row.Running=false;
   }finally{limiter.Release();}}));}finally{limiter.Dispose();}
   PinClosedRows(rows);CompleteClose(batch,commands.Count(r=>r.ItemStatus=="closed"),rows.Count);
  }
  void ReloadHistory(long preferred){loading=true;try{var rows=db.Query("SELECT b.id,b.created,COUNT(i.id),SUM(CASE WHEN i.status IN ('closed','launchfailed') THEN 1 ELSE 0 END) FROM batches b LEFT JOIN items i ON b.id=i.batch GROUP BY b.id ORDER BY b.id DESC");var batches=rows.Select(x=>new BatchInfo{Id=Int64.Parse(x[0]),Label=x[1]+"  ·  "+x[2]+" 项"}).ToList();var combo=Get<ComboBox>("BatchCombo");combo.ItemsSource=batches;combo.SelectedItem=batches.FirstOrDefault(x=>x.Id==preferred)??batches.FirstOrDefault();}finally{loading=false;}LoadHistory();}
  void LoadHistory(){var b=Get<ComboBox>("BatchCombo").SelectedItem as BatchInfo;history=b==null?new List<AppRow>():ops.History(b.Id);foreach(var r in history){LoadIcon(r);Wire(r,true);}Get<DataGrid>("RestoreGrid").ItemsSource=history;HistoryPresentation.ApplyOrder(Get<DataGrid>("RestoreGrid"));}
  async void RestoreSelected(){if(busy||demo)return;SetBusy(true);try{await RestoreCore();}catch(Exception ex){Error(ex);}finally{SetBusy(false);}}
   async void RefreshRestore(){if(busy||demo)return;SetBusy(true);try{await RefreshRestoreCore();}catch(Exception ex){Error(ex);}finally{SetBusy(false);}}
   async Task RefreshRestoreCore(){var rows=history.ToList();Status("正在检查恢复列表中的程序状态…");var running=await Task.Run(()=>ProcessService.RunningPaths());ops.RefreshStatuses(rows,running);int active=rows.Count(r=>r.Running==true);Status("已刷新 "+rows.Count+" 项："+active+" 项运行中，"+(rows.Count-active)+" 项未运行；可勾选未运行的程序启动");}
   async Task RestoreCore(){var rows=history.Where(x=>x.Selected&&x.CanRestore).ToList();if(rows.Count==0){Show();Navigate(1);Status("请在恢复列表中勾选要开启的程序，或刷新状态后重新选择");return;}
    if(rows.Any(x=>x.ItemId==0))ops.PrepareLaunch(rows);
    var commands=rows.Select(ProgramPresentation.Command).ToList();var running=await Task.Run(()=>ProcessService.RunningPaths());
    for(int index=0;index<rows.Count;index++){var r=rows[index];var command=commands[index];r.State="正在请求启动…";string text=await Task.Run(()=>ops.Restore(command,running));r.ItemStatus=command.ItemStatus;r.Error=command.Error;r.Running=command.Running;r.State=r.ItemStatus=="launched"?"成功发送请求":text;r.Selected=false;}
    Status("已处理启动请求，正在确认程序是否启动…");
    await new LaunchMonitor().VerifyAsync(rows,ops);HistoryPresentation.ApplyOrder(Get<DataGrid>("RestoreGrid"));
    db.Prune();var failed=rows.Where(r=>r.ItemStatus=="launchfailed"||r.ItemStatus=="uncertain").ToList();int started=rows.Count(r=>r.ItemStatus=="started"),already=rows.Count(r=>r.ItemStatus=="skipped");Status("共计 "+rows.Count+" 个程序，启动成功 "+started+" 个"+(already>0?"，已在运行 "+already+" 个":"")+(failed.Count>0?"；未启动成功："+String.Join("、",failed.Select(r=>r.Name)):"；已完成启动状态检查"));
   }
  async void HotkeyAction(bool restore){if(busy||demo)return;SetBusy(true);try{if(restore){if(!history.Any(x=>x.ItemStatus=="configured")){var b=db.Query("SELECT batch FROM items WHERE status IN ('closed','launchfailed') ORDER BY batch DESC LIMIT 1");if(b.Count>0)ReloadHistory(Int64.Parse(b[0][0]));}Navigate(1);await RestoreCore();}else{await Scan();await CloseCore();}}catch(Exception ex){Error(ex);}finally{SetBusy(false);}}
  void AddFile(){if(busy)return;var dialog=new Microsoft.Win32.OpenFileDialog{Title="添加需要管理的程序",Filter="应用程序 (*.exe)|*.exe",CheckFileExists=true};if(dialog.ShowDialog(w)==true){string p=Path.GetFullPath(dialog.FileName);db.SaveManaged(p,Path.GetFileNameWithoutExtension(p),"",false,false);db.Set("custom:"+p,"1");db.AllowProgram(new AppRow{Path=p,ProcessName=Path.GetFileName(p)});Refresh();}}
  void UpdateHiddenCount(){int count=db.HiddenPrograms().Count;Text("HiddenCountText",count==0?"尚未隐藏程序；隐藏只影响当前列表，不会关闭或卸载程序":"已从当前列表隐藏 "+count+" 个程序；刷新与重启后继续生效，可随时恢复显示");}
  void Forget(){if(busy)return;var rows=current.Where(x=>x.Selected).ToList();if(rows.Count==0){Status("请先勾选要隐藏的程序");return;}if(!AskOptional("将隐藏 "+rows.Count+" 个程序\n\n"+String.Join("\n",rows.Take(12).Select(x=>"• "+x.Name))+"\n\n隐藏只影响程序列表，不会关闭或卸载应用，可在“设置 → 隐藏程序管理”恢复显示","隐藏所选程序","confirmHide","ConfirmHideInput"))return;db.HidePrograms(rows);foreach(var row in rows)row.Selected=false;current=db.FilterVisible(current);Get<DataGrid>("ProcessGrid").ItemsSource=current;ApplyFilter();UpdateHiddenCount();Status("已隐藏 "+rows.Count+" 个程序；未关闭或卸载应用；可在“设置 → 隐藏程序管理”恢复显示");}
  void ManageHidden(){if(busy)return;var rows=db.HiddenPrograms();if(rows.Count==0){Status("没有已隐藏的程序");return;}var dialog=new ProgramSelectionDialog(w,rows,true);if(dialog.ShowDialog()!=true)return;db.RestorePrograms(dialog.Chosen);UpdateHiddenCount();Refresh();Status("已允许 "+dialog.Chosen.Count+" 个程序重新显示；下次识别时加入列表，不会自动勾选或启动");}
  void EditRow(AppRow row,bool past){if(row==null){Status("先单击一行，再编辑启动信息");return;}
   var dialog=new Window{UseLayoutRounding=true,SnapsToDevicePixels=true,Owner=w,Title="启动信息 · "+row.Name,Width=650,Height=390,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=new SolidColorBrush(Color.FromRgb(244,247,251)),FontFamily=w.FontFamily,FontSize=13};dialog.Resources=w.Resources;var panel=new StackPanel{Margin=new Thickness(24)};dialog.Content=panel;
   panel.Children.Add(new TextBlock{Text="程序路径（只读）",FontWeight=FontWeights.SemiBold});panel.Children.Add(new TextBox{Text=row.Path,IsReadOnly=true,Margin=new Thickness(0,10,0,16)});
   panel.Children.Add(new TextBlock{Text="启动参数 · 按原始格式保存，引号也需保留",FontWeight=FontWeights.SemiBold});var arguments=new TextBox{Text=row.Args,Height=60,AcceptsReturn=false,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,12)};panel.Children.Add(arguments);
   var admin=new CheckBox{Content="恢复时使用管理员权限",IsChecked=row.Admin};panel.Children.Add(admin);panel.Children.Add(new TextBlock{Text=(row.ArgsKnown?"已读取参数":"注意：原始参数无法读取，请手动补充")+"\n"+(String.IsNullOrEmpty(row.RiskReason)?"PID："+row.PidText:row.RiskReason),TextWrapping=TextWrapping.Wrap,Foreground=(Brush)w.FindResource("Muted"),Margin=new Thickness(0,12,0,12)});
   admin.Style=(Style)w.FindResource("Switch");Chrome.Decorate(dialog,false);var save=new Button{Content="保存启动信息",HorizontalAlignment=HorizontalAlignment.Right,Padding=new Thickness(14,8,14,8)};save.Click+=delegate{row.Args=arguments.Text;row.Admin=admin.IsChecked==true;row.ArgsKnown=true;dialog.DialogResult=true;};panel.Children.Add(save);dialog.ShowDialog();
  }
  void PickWindow(){if(busy||demo)return;SetBusy(true);pickReleased=false;w.Hide();tray.Text="轻羽归栈 · 单击目标窗口 / Esc 取消";picker=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(35)};DateTime end=DateTime.UtcNow.AddSeconds(30);picker.Tick+=async delegate{bool down=(Native.GetAsyncKeyState(1)&0x8000)!=0;if((Native.GetAsyncKeyState(27)&0x8000)!=0||DateTime.UtcNow>end){EndPick();Status("窗口定位已取消");return;}if(!down){pickReleased=true;return;}if(!pickReleased)return;Native.POINT point;Native.GetCursorPos(out point);IntPtr window=Native.GetAncestor(Native.WindowFromPoint(point),2);int pid=(int)Native.Pid(window);EndPick();try{SetBusy(true);var scan=await Task.Run(()=>ProcessService.Scan(true,false));var row=scan.Rows.FirstOrDefault(r=>r.Processes.Any(p=>p.Pid==pid));if(row==null||String.IsNullOrEmpty(row.Path))throw new IOException("无法定位该窗口的可执行路径；它可能是桌面、系统界面或受保护进程");db.SaveManaged(row.Path,row.Name,row.Args,false,false);db.Set("custom:"+row.Path,"1");db.AllowProgram(row);db.Set("args:"+row.Path,"1");await Scan();Navigate(0);var added=current.FirstOrDefault(r=>String.Equals(r.Path,row.Path,StringComparison.OrdinalIgnoreCase));Get<DataGrid>("ProcessGrid").SelectedItem=added;if(added!=null)Get<DataGrid>("ProcessGrid").ScrollIntoView(added);Status("已定位并添加："+row.ProcessName+" / PID "+pid+"；请检查后勾选");}catch(Exception ex){Error(ex);}finally{SetBusy(false);}};picker.Start();}
  void EndPick(){picker.Stop();tray.Text="轻羽归栈 · 后台暂时休息";Show();SetBusy(false);}
  void UpdateKeys(){Text("CloseHotkeyText",closeKey.ToString());Text("RestoreHotkeyText",restoreKey.ToString()+" · 快捷键处理最近一个含待恢复项的批次");}
  void CaptureKey(TextBox box,KeyEventArgs e,bool close){e.Handled=true;Key key=e.Key==Key.System?e.SystemKey:e.Key;if(key==Key.Tab){e.Handled=false;return;}if(key==Key.LeftCtrl||key==Key.RightCtrl||key==Key.LeftAlt||key==Key.RightAlt||key==Key.LeftShift||key==Key.RightShift||key==Key.LWin||key==Key.RWin)return;var m=Keyboard.Modifiers;uint mods=((m&ModifierKeys.Alt)!=0?1u:0)|((m&ModifierKeys.Control)!=0?2u:0)|((m&ModifierKeys.Shift)!=0?4u:0)|((m&ModifierKeys.Windows)!=0?8u:0);var spec=new HotkeySpec{Mods=mods,Key=(uint)KeyInterop.VirtualKeyFromKey(key)};if(key==Key.Delete||key==Key.Back)spec=new HotkeySpec();else if((mods&11)==0){Status("快捷键至少包含 Ctrl、Alt 或 Win");return;}if(close)editClose=spec;else editRestore=spec;box.Text=spec.ToString();}
  void RegisterKeys(HotkeySpec close,HotkeySpec restore){Native.UnregisterHotKey(handle,1);Native.UnregisterHotKey(handle,2);if(close.Key!=0&&!Native.RegisterHotKey(handle,1,close.Mods|0x4000,close.Key))throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),"关闭快捷键注册失败；可能已被其他程序占用");if(restore.Key!=0&&restore.Code!=close.Code&&!Native.RegisterHotKey(handle,2,restore.Mods|0x4000,restore.Key)){Native.UnregisterHotKey(handle,1);throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),"恢复快捷键注册失败；可能已被其他程序占用");}}
  IntPtr Hook(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled){if(msg==0x312){handled=true;if(Get<TextBox>("CloseHotkeyInput").IsKeyboardFocused||Get<TextBox>("RestoreHotkeyInput").IsKeyboardFocused||busy)return IntPtr.Zero;Guard(delegate{bool restore=wp.ToInt32()==2;if(closeKey.Code==restoreKey.Code)restore=Policy.SharedAction(history.Any(x=>x.ItemStatus=="configured"&&x.Selected)||db.Query("SELECT id FROM items WHERE status IN ('closed','launchfailed') LIMIT 1").Count>0)=="restore";HotkeyAction(restore);});}return IntPtr.Zero;}
  void SaveSettings(){bool oldAuto=demo?Setting("autoStart",false):StartupManager.Enabled,oldAdmin=demo?Setting("runAsAdmin",true):StartupManager.PreferAdmin;bool startupApplied=false;
   try{if(!demo)RegisterKeys(editClose,editRestore);}catch(Exception original){try{if(!demo)RegisterKeys(closeKey,restoreKey);}catch(Exception rollback){throw new InvalidOperationException(original.Message+"\n原快捷键也未能恢复："+rollback.Message);}throw;}
   try{db.Execute("BEGIN IMMEDIATE");db.Set("closeHotkey",editClose.Code);db.Set("restoreHotkey",editRestore.Code);db.Set("fontFamily",Appearance.Resolve(Get<ComboBox>("FontFamilyInput").SelectedValue as string??w.FontFamily.Source).Source);
    foreach(var pair in new[]{new[]{"confirmClose","ConfirmCloseInput"},new[]{"confirmHide","ConfirmHideInput"},new[]{"confirmForce","ConfirmForceInput"},new[]{"candidates","ShowCandidates"},new[]{"minimizeTray","MinimizeToTray"},new[]{"launchHidden","LaunchHidden"}})db.Set(pair[0],Get<CheckBox>(pair[1]).IsChecked==true?"1":"0");
    SaveModern();startupApplied=true;db.Execute("COMMIT");
   }catch{db.Execute("ROLLBACK");if(!demo){RegisterKeys(closeKey,restoreKey);if(startupApplied)StartupManager.Apply(oldAuto,oldAdmin);}throw;}
   w.FontFamily=Appearance.Resolve(db.Get("fontFamily",Appearance.DefaultFont));closeKey=editClose;restoreKey=editRestore;UpdateKeys();Status("设置已保存");
  }
  void FillDemo(){Navigate(0);var names=new[]{"音乐播放器","聊天应用","云盘同步","创意工具更新器","Windows 资源管理器"};var exe=new[]{"MusicPlayer.exe","Messenger.exe","CloudSync.exe","CreativeUpdater.exe","explorer.exe"};for(int i=0;i<5;i++)current.Add(new AppRow{Name=names[i],ProcessName=exe[i],Path=@"C:\Program Files\Example\"+exe[i],Source=i<3?"通知区域":"任务栏窗口",MemoryMB=new[]{248.0,312,89,65,143}[i],Selected=i<3,ArgsKnown=true,Risk=i==4,RiskReason=i==4?"系统基础进程":"",State="运行中"});foreach(var row in current)LoadIcon(row);Get<DataGrid>("ProcessGrid").ItemsSource=current;ApplyFilter();Status("界面预览使用示例数据 · 正式运行时自动识别实际进程");}
 }
}

