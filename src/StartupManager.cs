using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;
namespace TrayLeaf {
 internal static class StartupManager {
 const string Preferences=@"Software\TrayLeaf\Preferences";
 const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
 internal static string TaskName {get{return "TrayLeaf-"+WindowsIdentity.GetCurrent().User.Value;}}
 internal static string Exe {get{return Assembly.GetExecutingAssembly().Location;}}
 internal static bool PreferAdmin {get{using(var k=Registry.CurrentUser.OpenSubKey(Preferences))return Convert.ToString(k==null?"1":k.GetValue("RunAsAdmin","1"))!="0";}}
 internal static bool Enabled {get{return HasRunEntry||TaskEnabled;}}
 internal static bool TaskEnabled {get{object service=null,folder=null,task=null;try{service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));dynamic scheduler=service;scheduler.Connect();folder=scheduler.GetFolder("\\");dynamic f=folder;task=f.GetTask(TaskName);return ((dynamic)task).Enabled;}catch{return false;}finally{Release(task);Release(folder);Release(service);}}}
 static void Release(object o){if(o!=null&&Marshal.IsComObject(o))Marshal.FinalReleaseComObject(o);}
 internal static string Quote(string arg){var result=new System.Text.StringBuilder("\"");int slash=0;foreach(char c in arg??""){if(c=='\\'){slash++;continue;}if(c=='\"'){result.Append('\\',slash*2+1);result.Append(c);slash=0;continue;}result.Append('\\',slash);slash=0;result.Append(c);}result.Append('\\',slash*2);return result.Append('"').ToString();}
 internal static string CommandLine(string[] args){return String.Join(" ",args.Select(Quote));}
 // asInvoker manifest allows the preference to be disabled; explicit runas is only attempted at entry.
 internal static int Bootstrap(string[] args){if(args.Contains("--ui-smoke")||args.Contains("--no-elevate")||!PreferAdmin||ProcessService.Elevated)return -1;if(args.Contains("--elevated"))throw new InvalidOperationException("未能获得管理员权限");try{var start=new ProcessStartInfo(Exe,CommandLine(args.Concat(new[]{"--elevated"}).ToArray())){UseShellExecute=true,Verb="runas",WorkingDirectory=Path.GetDirectoryName(Exe)};using(var process=Process.Start(start)){}return 0;}catch(System.ComponentModel.Win32Exception ex){if(ex.NativeErrorCode==1223)return -1;throw;}}
 internal static bool HasRunEntry {get{using(var key=Registry.CurrentUser.OpenSubKey(RunKey))return key!=null&&key.GetValue("TrayLeaf")!=null;}}
 internal static int InstallerAction(string[] args){int index=Array.IndexOf(args,"--configure-startup");bool uninstall=args.Contains("--remove-startup");if(index<0&&!uninstall)return -1;bool enabled=!uninstall&&index+1<args.Length&&args[index+1]=="1";
  if(!ProcessService.Elevated&&((enabled&&PreferAdmin)||(!enabled&&TaskEnabled))){if(args.Contains("--elevated"))return 5;try{using(var child=Process.Start(new ProcessStartInfo(Exe,CommandLine(args.Concat(new[]{"--elevated"}).ToArray())){UseShellExecute=true,Verb="runas"})){child.WaitForExit();return child.ExitCode;}}catch(System.ComponentModel.Win32Exception e){return e.NativeErrorCode;}}
  try{Apply(enabled,PreferAdmin);return 0;}catch{return 1;}
 }
 internal static void ReconcileInstalledStartup(){if(!ProcessService.Elevated||!PreferAdmin||!HasRunEntry)return;using(var key=Registry.CurrentUser.OpenSubKey(@"Software\TrayLeaf\Installer")){string folder=Convert.ToString(key==null?null:key.GetValue("InstallPath"));if(!String.IsNullOrEmpty(folder)&&String.Equals(Path.GetFullPath(folder).TrimEnd('\\'),Path.GetDirectoryName(Exe).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))Apply(true,true);}}
 internal static void Apply(bool enabled,bool admin){
  // Register replacement first: failures preserve the previous startup route and preference.
  if(enabled&&admin){if(!ProcessService.Elevated)throw new InvalidOperationException("启用管理员自启动需先以管理员身份打开本程序，再保存此设置");RegisterTask();using(var key=Registry.CurrentUser.CreateSubKey(RunKey))key.DeleteValue("TrayLeaf",false);}
  else if(enabled){using(var key=Registry.CurrentUser.CreateSubKey(RunKey))key.SetValue("TrayLeaf",Quote(Exe)+" --startup");RemoveTask();}
  else {RemoveTask();using(var key=Registry.CurrentUser.CreateSubKey(RunKey))key.DeleteValue("TrayLeaf",false);}
  using(var key=Registry.CurrentUser.CreateSubKey(Preferences)){key.SetValue("RunAsAdmin",admin?"1":"0",RegistryValueKind.String);key.SetValue("AutoStart",enabled?"1":"",RegistryValueKind.String);}
 }
 internal static string TaskXml(string exe,string sid){Func<string,string> escape=value=>System.Security.SecurityElement.Escape(value??"");return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>"+
  "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"><RegistrationInfo><Description>轻羽归栈 · 当前用户登录时启动</Description></RegistrationInfo>"+
  "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>"+escape(sid)+"</UserId><Delay>PT5S</Delay></LogonTrigger></Triggers>"+
  "<Principals><Principal id=\"Author\"><UserId>"+escape(sid)+"</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>"+
  "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><ExecutionTimeLimit>PT0S</ExecutionTimeLimit></Settings>"+
  "<Actions Context=\"Author\"><Exec><Command>"+escape(exe)+"</Command><Arguments>--startup</Arguments><WorkingDirectory>"+escape(Path.GetDirectoryName(exe))+"</WorkingDirectory></Exec></Actions></Task>";}
 static void RegisterTask(){object service=null,folder=null,registered=null;try{service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));dynamic scheduler=service;scheduler.Connect();folder=scheduler.GetFolder("\\");string sid=WindowsIdentity.GetCurrent().User.Value;dynamic f=folder;registered=f.RegisterTask(TaskName,TaskXml(Exe,sid),6,sid,null,3,null);}finally{Release(registered);Release(folder);Release(service);}}
 // COM maps ERROR_FILE_NOT_FOUND to FileNotFoundException on some Windows versions.
 // Delete directly so an already-absent task (including a concurrent removal) is a successful no-op.
 internal static void RemoveTaskIfPresent(Action deleteTask){try{deleteTask();}catch(Exception ex){if((uint)ex.HResult!=0x80070002)throw;}}
 static void RemoveTask(){object service=null,folder=null;try{service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));dynamic s=service;s.Connect();folder=s.GetFolder("\\");dynamic f=folder;RemoveTaskIfPresent(()=>{f.DeleteTask(TaskName,0);});}finally{Release(folder);Release(service);}}
 }
}
