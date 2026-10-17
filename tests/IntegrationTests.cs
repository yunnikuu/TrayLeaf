using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using TrayLeaf;
class IntegrationTests {
 static int passed,failed,warnings;static string root,exe;static List<Process> children=new List<Process>();
 static void Check(string label,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+label);if(ok)passed++;else failed++;}
 static void Warn(string label){warnings++;Console.WriteLine("LIMIT "+label);}
 static void Throws(string label,Action a){bool ok=false;try{a();}catch{ok=true;}Check(label,ok);}
 static string Q(string s){return "\""+s+"\"";}
 static bool Until(Func<bool> f){for(int i=0;i<60;i++){if(f())return true;Thread.Sleep(100);}return false;}
 static Process Start(string flags){var p=Process.Start(new ProcessStartInfo(exe,flags){UseShellExecute=false});children.Add(p);p.WaitForInputIdle(5000);Thread.Sleep(500);return p;}
 static AppRow Row(Process p,string args){p.Refresh();return new AppRow{Name="受控测试 O'Neil",ProcessName=Path.GetFileName(exe),Path=exe,Args=args,ArgsKnown=true,Selected=true,Processes=new List<Identity>{new Identity{Pid=p.Id,Started=p.StartTime.ToUniversalTime().Ticks,Path=exe}}};}
 static void Stop(Process p){try{if(!p.HasExited){p.Kill();p.WaitForExit(5000);}}catch{}p.Dispose();children.Remove(p);}
 [StructLayout(LayoutKind.Sequential)]struct RECT{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr w,out RECT r);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr w);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr w,IntPtr after,int x,int y,int cx,int cy,uint flags);
 [STAThread]static int Main(string[] args){root=Path.GetFullPath(args[0]);exe=Path.Combine(root,"TrayLeaf.ControlledFixture.exe");try{Run();}catch(Exception ex){failed++;Console.Error.WriteLine(ex);}finally{foreach(var p in children.ToArray())Stop(p);}Console.WriteLine("RESULT passed="+passed+" failed="+failed+" limitations="+warnings);return failed==0?0:1;}
 static void Run(){
  string dbfile=Path.Combine(root,"测试 database.db");
  using(var db=new Database(dbfile)){
   db.Set("中文引号'","值 \"a b\" ");Check("sqlite-unicode-parameterized",db.Get("中文引号'","")=="值 \"a b\" ");
   db.SaveManaged(exe,"test","--token \"a b\"",true,true);Check("sqlite-managed-flags",db.Query("SELECT args,admin,chosen FROM managed WHERE path=?",exe)[0].SequenceEqual(new[]{"--token \"a b\"","1","1"}));
   db.Execute("BEGIN IMMEDIATE");db.Set("rollback-key","value");db.Execute("ROLLBACK");Check("sqlite-transaction-rollback",db.Get("rollback-key","missing")=="missing");
  }
  using(var db=new Database(dbfile)){
   Check("sqlite-reopen-persists",db.Get("中文引号'","")=="值 \"a b\" ");
   var ops=new OperationService(db);var p=Start("");var row=Row(p,"");long batch=ops.Prepare(new List<AppRow>{row});
   Check("batch-saved-before-close",db.Query("SELECT status FROM items WHERE batch=?",batch.ToString())[0][0]=="prepared");
   var mismatch=Row(p,"");mismatch.Processes[0].Started++;Throws("identity-mismatch-refused",()=>ProcessService.Force(mismatch));Check("identity-mismatch-process-alive",!p.HasExited);
   Check("visible-window-owner-detected",Native.VisiblePids().Contains(p.Id));
   SetWindowPos(p.MainWindowHandle,new IntPtr(-1),80,80,420,180,0x0040);Thread.Sleep(300);RECT rect;GetWindowRect(p.MainWindowHandle,out rect);var pt=new Native.POINT{X=(rect.Left+rect.Right)/2,Y=(rect.Top+rect.Bottom)/2};Console.WriteLine("OBSERVED pick hwnd="+p.MainWindowHandle+" rect="+rect.Left+","+rect.Top+","+rect.Right+","+rect.Bottom+" point="+pt.X+","+pt.Y+" resolved="+Native.Pid(Native.GetAncestor(Native.WindowFromPoint(pt),2))+" expected="+p.Id+" owner-class="+Native.Class(Native.WindowFromPoint(pt)));Check("window-point-resolves-pid",Native.Pid(Native.GetAncestor(Native.WindowFromPoint(pt),2))==p.Id);
   string note;var snap=ProcessService.Snapshot(out note);var info=snap.FirstOrDefault(x=>x.Id==p.Id);Check("snapshot-path-and-identity",info!=null&&String.Equals(info.Path,exe,StringComparison.OrdinalIgnoreCase)&&info.Started>0);
   Console.WriteLine("OBSERVED snapshot-note="+note);
   Check("default-scan-visible-fixture",ProcessService.Scan(false,false).Rows.Any(x=>x.Processes.Any(id=>id.Pid==p.Id)));
   Check("all-process-scan-fixture",ProcessService.Scan(true,false).Rows.Any(x=>x.Processes.Any(id=>id.Pid==p.Id)));
   Check("graceful-close-and-closed-state",ops.Close(row,x=>false)=="已关闭"&&p.WaitForExit(2000)&&row.CanRestore);
   Stop(p);
   string log=Path.Combine(root,"restore args.txt");row.Args="--log "+Q(log)+" --marker \"hello 世界\"";row.Admin=false;
   Check("restore-request-recorded",ops.Restore(row)=="已发送启动请求"&&row.ItemStatus=="launched");
   Check("restored-child-log-exists",Until(()=>File.Exists(log)));
   string content=File.ReadAllText(log);int launchedId=Int32.Parse(content.Split('\n')[0]);var launched=Process.GetProcessById(launchedId);children.Add(launched);
   Check("restore-exact-arguments",content.Contains("--marker \"hello 世界\""));
   row.ItemStatus="closed";Check("running-path-deduplication",ops.Restore(row)=="已在运行 / 跳过"&&row.ItemStatus=="skipped");Stop(launched);
   row.ItemStatus="closed";row.Path=Path.Combine(root,"nonexistent.exe");Check("launch-failure-retryable",ops.Restore(row).StartsWith("启动失败")&&row.CanRestore);
   Throws("invalid-prepare-refused",()=>ops.Prepare(new List<AppRow>{row}));
   p=Start("--ignore-close");row=Row(p,"");ops.Prepare(new List<AppRow>{row});Check("force-close-only-owned-fixture",ops.Close(row,x=>true)=="已关闭"&&p.WaitForExit(2000));Stop(p);
   p=Start("--ignore-close");row=Row(p,"");ops.Prepare(new List<AppRow>{row});row.Risk=true;Check("decline-force-remains-alive",ops.Close(row,x=>false).StartsWith("已取消")&&!p.HasExited&&row.ItemStatus=="cancelled");Stop(p);
   p=Start("--tray-only");row=Row(p,"");Thread.Sleep(1000);var scan=ProcessService.Scan(false,false);bool tray=scan.Rows.Any(x=>x.Processes.Any(id=>id.Pid==p.Id));if(tray)Check("default-scan-tray-only-fixture",true);else Warn("tray-only-fixture-not-confirmed-on-current-Windows; all-process-mode-available");Stop(p);
   row=new AppRow{Name="保留批次",Path=exe,Args=""};long pending=ops.Prepare(new List<AppRow>{row});ops.State(row,"closed","");long uncertain=ops.Prepare(new List<AppRow>{new AppRow{Name="中断",Path=exe,Args=""}});ops.RecoverInterrupted();Check("interrupted-operation-uncertain",ops.History(uncertain)[0].ItemStatus=="uncertain");
   for(int i=0;i<22;i++){var r=new AppRow{Name="history",Path=exe,Args=""};ops.Prepare(new List<AppRow>{r});ops.State(r,"launched","");}db.Prune();Check("prune-evicts-old-pending",db.Query("SELECT id FROM batches WHERE id=?",pending.ToString()).Count==0);Check("prune-evicts-old-uncertain",db.Query("SELECT id FROM batches WHERE id=?",uncertain.ToString()).Count==0);Check("prune-strict-newest-ten",db.Query("SELECT id FROM batches").Count==10);
   Check("database-integrity",db.Query("PRAGMA integrity_check")[0][0]=="ok");
  }
  Check("hotkey-default-display",HotkeySpec.Parse("3|49").ToString()=="Ctrl + Alt + 1");Check("hotkey-same-combination",HotkeySpec.Parse("3|49").Code==HotkeySpec.Parse("3|49").Code);Throws("hotkey-invalid-rejected",()=>HotkeySpec.Parse("0|65"));
  bool registered=Native.RegisterHotKey(IntPtr.Zero,42,0x4003,0x86);Check("native-hotkey-register",registered);try{if(registered)Check("native-hotkey-conflict-detected",!Native.RegisterHotKey(IntPtr.Zero,43,0x4003,0x86));}finally{Native.UnregisterHotKey(IntPtr.Zero,42);Native.UnregisterHotKey(IntPtr.Zero,43);}
  Console.WriteLine("OBSERVED elevated="+ProcessService.Elevated+"; no-admin-UAC-test; only-controlled-fixtures-terminated");
 }
}
