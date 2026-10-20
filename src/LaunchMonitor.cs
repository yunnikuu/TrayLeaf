using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace TrayLeaf {
 // Observe the executable path, not Shell/UAC return codes or a matching process name.
 internal sealed class LaunchMonitor {
  internal const int IntervalMilliseconds=3000,MaximumChecks=5;
  readonly Func<HashSet<string>> read;readonly Func<int,Task> delay;
  internal LaunchMonitor():this(ProcessService.RunningPaths,ms=>Task.Delay(ms)){}
  internal LaunchMonitor(Func<HashSet<string>> readRunningPaths,Func<int,Task> wait){read=readRunningPaths;delay=wait;}
  public async Task VerifyAsync(IEnumerable<AppRow> rows,OperationService ops){
   var pending=rows.Where(r=>r.ItemStatus=="launched").ToList();
   for(int attempt=1;attempt<=MaximumChecks&&pending.Count>0;attempt++){
    await delay(IntervalMilliseconds);HashSet<string> running=null;string error=null;
    try{var snapshot=await Task.Run(read);running=new HashSet<string>(snapshot,StringComparer.OrdinalIgnoreCase);}catch(Exception ex){error=ex.Message;}
    foreach(var row in pending.ToList()){
     if(running!=null&&running.Contains(row.Path??"")){row.Running=true;ops.State(row,"started","");row.State="启动成功";pending.Remove(row);}
     else if(attempt==MaximumChecks){row.Running=false;ops.State(row,"launchfailed",error==null?"15 秒内未检测到此路径的运行进程，可刷新状态后重新启动":"状态检测未完成："+error);row.State="启动失败";pending.Remove(row);}
     else row.State="成功发送请求";
    }
   }
  }
 }
}
