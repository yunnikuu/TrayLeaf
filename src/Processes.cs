using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Win32;
namespace TrayLeaf {
 public class Identity {public int Pid;public long Started;public string Path;}
 public class AppRow:INotifyPropertyChanged {
  public event PropertyChangedEventHandler PropertyChanged;
  void Change(string n){var h=PropertyChanged;if(h!=null)h(this,new PropertyChangedEventArgs(n));}
  bool selected,admin;string args="",state="",error="";
  public string Error {get{return error;}set{error=value??"";Change("Error");}}
  public bool Selected {get{return selected;}set{selected=value;Change("Selected");}}
  public bool Admin {get{return admin;}set{admin=value;Change("Admin");}}
  public string Args {get{return args;}set{args=value??"";Change("Args");}}
  public string State {get{return state;}set{state=value;Change("State");}}
  public string Name {get;set;} public string ProcessName {get;set;}public string Path {get;set;} public string Source {get;set;}
  public bool Risk {get;set;}public string RiskReason {get;set;} public bool ArgsKnown {get;set;}public double MemoryMB {get;set;}
  public string Memory {get{return MemoryMB>0?MemoryMB.ToString("N0")+" MB":"—";}}
  public string PidText {get{return String.Join(", ",Processes.Select(x=>x.Pid.ToString()).ToArray());}}
  public string Hint {get{return String.IsNullOrEmpty(Path)?"无法读取路径 / PID "+PidText:Path;}}
  public string RiskText {get{return Risk?"系统 / 服务":"普通应用";}}
  public ImageSource Icon {get;set;}
  public List<Identity> Processes=new List<Identity>();public long ItemId;string itemStatus;public string ItemStatus {get{return itemStatus;}set{itemStatus=value;Change("CanRestore");}}
  public bool CanRestore {get{return ItemStatus=="closed"||ItemStatus=="launchfailed";}}
 }
 public class ProcInfo {public int Id,Parent;public string Name,Path,Command;public long Started;public double Memory;public bool Service,Critical;}
 public class ScanResult {public List<AppRow> Rows=new List<AppRow>();public string Note="";public int Candidates;}
 public static class ProcessService {
  public static bool Elevated {get {using(var id=WindowsIdentity.GetCurrent())return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);}}
  public static List<ProcInfo> Snapshot(out string note) {
   var list=new List<ProcInfo>();note="";var services=new HashSet<int>();
   try {using(var s=new ManagementObjectSearcher("SELECT ProcessId FROM Win32_Service WHERE ProcessId > 0"))using(var items=s.Get())foreach(ManagementObject m in items)using(m){services.Add(Convert.ToInt32(m["ProcessId"]));}}catch(Exception ex){note="服务识别受限："+ex.Message;}
   try {using(var s=new ManagementObjectSearcher("SELECT ProcessId,ParentProcessId,Name,ExecutablePath,CommandLine FROM Win32_Process"))using(var items=s.Get())foreach(ManagementObject m in items)using(m){int pid=Convert.ToInt32(m["ProcessId"]);if(pid==Process.GetCurrentProcess().Id)continue;var info=new ProcInfo {Id=pid,Parent=Convert.ToInt32(m["ParentProcessId"]),Name=Convert.ToString(m["Name"]),Path=Convert.ToString(m["ExecutablePath"]),Command=m["CommandLine"] as string,Service=services.Contains(pid),Critical=Native.Critical((uint)pid)};try{using(var p=Process.GetProcessById(pid)){info.Started=p.StartTime.ToUniversalTime().Ticks;info.Memory=p.WorkingSet64/1048576.0;if(String.IsNullOrEmpty(info.Path))info.Path=p.MainModule.FileName;}}catch{}list.Add(info);}}
   catch(Exception ex){note+=" 进程参数读取受限："+ex.Message;foreach(var p in Process.GetProcesses())using(p){if(p.Id==Process.GetCurrentProcess().Id)continue;var i=new ProcInfo{Id=p.Id,Name=p.ProcessName+".exe",Path="",Service=services.Contains(p.Id),Critical=Native.Critical((uint)p.Id)};try{i.Path=p.MainModule.FileName;i.Started=p.StartTime.ToUniversalTime().Ticks;i.Memory=p.WorkingSet64/1048576.0;}catch{}list.Add(i);}}
   return list;
  }
  public static ScanResult Scan(bool all,bool candidates) {
   string note;var procs=Snapshot(out note);var result=new ScanResult{Note=note};var exact=new HashSet<int>();var probable=new HashSet<int>();var visible=new HashSet<int>();
   if(!all){visible=Native.VisiblePids();exact=Native.ToolbarPids();Dictionary<uint,List<IntPtr>> owners=null;
    try{using(var key=Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings")){if(key!=null)foreach(string sub in key.GetSubKeyNames())using(var k=key.OpenSubKey(sub)){if(k==null)continue;string path=Native.ExpandKnownFolder(Convert.ToString(k.GetValue("ExecutablePath")));if(String.IsNullOrEmpty(path))continue;var matched=procs.Where(x=>!String.IsNullOrEmpty(x.Path)&&String.Equals(x.Path,path,StringComparison.OrdinalIgnoreCase)).ToList();if(matched.Count==0)continue;Guid guid;bool hasGuid=Guid.TryParse(Convert.ToString(k.GetValue("IconGuid")),out guid);if(hasGuid&&Native.IconRect(IntPtr.Zero,0,guid)){foreach(var x in matched)exact.Add(x.Id);continue;}object uidValue=k.GetValue("UID");foreach(var x in matched){bool confirmed=false;if(uidValue!=null){if(owners==null)owners=Native.WindowOwners();List<IntPtr> windows;if(owners.TryGetValue((uint)x.Id,out windows)){uint uid=Convert.ToUInt32(uidValue);foreach(IntPtr h in windows)if(Native.IconRect(h,uid,Guid.Empty)){confirmed=true;break;}}}if(confirmed)exact.Add(x.Id);else probable.Add(x.Id);}}}}
    catch(Exception ex){result.Note+=" 托盘识别受限："+ex.Message;}
   }
   result.Candidates=probable.Except(exact).Except(visible).Count();
   var filtered=procs.Where(x=>all||visible.Contains(x.Id)||exact.Contains(x.Id)||(candidates&&probable.Contains(x.Id))).ToList();
   foreach(var group in filtered.GroupBy(x=>String.IsNullOrEmpty(x.Path)?"pid:"+x.Id:x.Path,StringComparer.OrdinalIgnoreCase)){
    var list=procs.Where(x=>!String.IsNullOrEmpty(group.First().Path)&&String.Equals(x.Path,group.First().Path,StringComparison.OrdinalIgnoreCase)).ToList();if(list.Count==0)list=group.ToList();
    var main=list.OrderBy(x=>list.Any(p=>p.Id==x.Parent)?1:0).ThenBy(x=>x.Id).First();string name=System.IO.Path.GetFileNameWithoutExtension(main.Name);
    try{if(!String.IsNullOrEmpty(main.Path)&&!main.Path.StartsWith(@"\\")){string desc=FileVersionInfo.GetVersionInfo(main.Path).FileDescription;if(!String.IsNullOrWhiteSpace(desc))name=desc;}}catch{}
    bool risk=list.Any(x=>Policy.HighRisk(x.Name,x.Service,x.Critical));var row=new AppRow{Name=name,ProcessName=main.Name,Path=main.Path??"",Args=Policy.Arguments(main.Command),ArgsKnown=main.Command!=null,Risk=risk,RiskReason=list.Any(x=>x.Critical)?"Windows 标记的关键进程":list.Any(x=>x.Service)?"Windows 服务进程（强制关闭可能中断系统服务）":risk?"系统基础或安全相关进程":"",MemoryMB=list.Sum(x=>x.Memory),Source=all?"全部进程":group.Any(x=>exact.Contains(x.Id))?"通知区域":group.Any(x=>visible.Contains(x.Id))?"任务栏窗口":"托盘候选",State="运行中"};
    row.Processes=list.Select(x=>new Identity{Pid=x.Id,Started=x.Started,Path=x.Path??""}).ToList();result.Rows.Add(row);
   }
   result.Rows=result.Rows.OrderBy(x=>x.Risk).ThenBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToList();return result;
  }
  static bool Verify(Process p,Identity expected) {
   if(p.HasExited)return false;
   // Open and pin the process handle before checking identity; keep this object through Kill.
   IntPtr handle=p.Handle;
   if(expected.Started<=0||p.StartTime.ToUniversalTime().Ticks!=expected.Started)throw new InvalidOperationException("PID 身份已变化或启动时间无法核实，已阻止关闭");
   if(!String.IsNullOrEmpty(expected.Path)&&!String.Equals(p.MainModule.FileName,expected.Path,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("进程路径已变化，已阻止关闭");
   return true;
  }
  public static bool Alive(Identity expected) {try{using(var p=Process.GetProcessById(expected.Pid))return Verify(p,expected);}catch(ArgumentException){return false;}}
  public static int Graceful(AppRow row) {
   foreach(var id in row.Processes)try{using(var p=Process.GetProcessById(id.Pid)){if(Verify(p,id))p.CloseMainWindow();}}catch(ArgumentException){}
   DateTime end=DateTime.UtcNow.AddSeconds(5);while(DateTime.UtcNow<end){if(!row.Processes.Any(Alive))return 0;Thread.Sleep(100);}return row.Processes.Count(Alive);
  }
  public static void Force(AppRow row) {
   foreach(var id in row.Processes){if(id.Pid<=4||id.Pid==Process.GetCurrentProcess().Id)throw new InvalidOperationException("该系统保留进程或工具自身不能通过此工具结束");try{using(var p=Process.GetProcessById(id.Pid)){if(Verify(p,id)){p.Kill();if(!p.WaitForExit(4000))throw new IOException("结束请求已发送，但进程仍未退出");}}}catch(ArgumentException){}}
  }
  public static bool IsRunning(string path){foreach(var p in Process.GetProcesses())using(p){try{if(!p.HasExited&&String.Equals(p.MainModule.FileName,path,StringComparison.OrdinalIgnoreCase))return true;}catch{}}return false;}
  public static void Launch(string path,string args,bool admin) {
   if(!System.IO.Path.IsPathRooted(path)||!File.Exists(path))throw new FileNotFoundException("程序路径无效或文件不存在",path);
   if(args!=null&&(args.IndexOf('\0')>=0||args.Length>30000))throw new ArgumentException("启动参数无效或过长");
   string dir=System.IO.Path.GetDirectoryName(path);
   if(admin){var si=new ProcessStartInfo(path,args??""){UseShellExecute=true,Verb="runas",WorkingDirectory=dir};using(var p=Process.Start(si)){}return;}
   if(!Elevated){using(var p=Process.Start(new ProcessStartInfo(path,args??""){UseShellExecute=false,WorkingDirectory=dir})){}return;}
   Exception failure=null;var t=new Thread(delegate(){object shellWindows=null,desktop=null,application=null;try{shellWindows=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")));dynamic windows=shellWindows;object location=0,root=0;int hwnd;desktop=windows.FindWindowSW(ref location,ref root,8,out hwnd,1);if(desktop==null)throw new InvalidOperationException("未找到标准权限的桌面 Shell");dynamic view=desktop;application=view.Document.Application;dynamic shell=application;shell.ShellExecute(path,args??"",dir,"open",1);}catch(Exception ex){failure=ex;}finally{foreach(object o in new[]{application,desktop,shellWindows})if(o!=null&&Marshal.IsComObject(o))Marshal.ReleaseComObject(o);}});t.IsBackground=true;t.SetApartmentState(ApartmentState.STA);t.Start();if(!t.Join(15000))throw new TimeoutException("桌面 Shell 启动请求超时，请先检查程序是否已启动，避免重复执行");if(failure!=null)throw new InvalidOperationException("普通权限启动失败（未回退为管理员启动）："+failure.Message,failure);
  }
 }
}