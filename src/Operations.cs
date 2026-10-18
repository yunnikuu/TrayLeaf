using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
namespace TrayLeaf {
 public sealed class OperationService {
  readonly Database db;
  public OperationService(Database database){db=database;}
  public long Prepare(List<AppRow> rows) {
   foreach(var r in rows) if(String.IsNullOrWhiteSpace(r.Path)||!File.Exists(r.Path))throw new IOException("无法保存可恢复的程序路径："+r.Name+"；未关闭任何程序");
   long batch=db.Batch(rows);var ids=db.Query("SELECT id FROM items WHERE batch=? ORDER BY id",batch.ToString());
   for(int i=0;i<rows.Count;i++){rows[i].ItemId=Int64.Parse(ids[i][0]);rows[i].ItemStatus="prepared";}
   return batch;
  }
  public long PrepareLaunch(List<AppRow> rows){
   long batch=db.Batch(rows,false);var ids=db.Query("SELECT id FROM items WHERE batch=? ORDER BY id",batch.ToString());
   for(int i=0;i<rows.Count;i++){rows[i].ItemId=Int64.Parse(ids[i][0]);State(rows[i],"closed","");db.RememberLaunch(rows[i]);}return batch;
  }
  public void State(AppRow row,string state,string error) {db.Execute("UPDATE items SET status=?,error=? WHERE id=?",state,error??"",row.ItemId.ToString());row.ItemStatus=state;row.Error=error;}
  public string Close(AppRow row,Func<AppRow,bool> permitForce) {
   try {
    if(ProcessService.Graceful(row)>0) {
     if(row.Risk&&!permitForce(row)){State(row,"cancelled","已放弃强制关闭；可能已有部分同路径进程退出");return "已取消 / 部分退出";}
     ProcessService.Force(row);
    }
    if(row.Processes.Any(ProcessService.Alive))throw new IOException("仍有同路径进程未退出");
    State(row,"closed","");return "已关闭";
   }catch(Exception ex){State(row,"closefailed",ex.Message);return "关闭失败："+ex.Message;}
  }
  public string Restore(AppRow row) {
   if(!row.CanRestore)return "该记录不可恢复";
   try {
    db.RememberLaunch(row);
    if(ProcessService.IsRunning(row.Path)){row.Running=true;row.Selected=false;State(row,"skipped","同路径程序已经运行，未重复启动");return "已在运行 / 跳过";}
    db.Execute("UPDATE items SET args=?,admin=? WHERE id=?",row.Args,row.Admin?"1":"0",row.ItemId.ToString());
    State(row,"launching","");ProcessService.Launch(row.Path,row.Args,row.Admin);
    // Shell/UAC 返回只说明请求成功，不保证应用继续运行。
    row.Running=null;State(row,"launched","");return "已发送启动请求";
   }catch(TimeoutException ex){State(row,"uncertain",ex.Message);return "启动请求超时 / 需核查";}catch(Exception ex){State(row,"launchfailed",ex.Message);return "启动失败："+ex.Message;}
  }
  public void RefreshStatuses(IEnumerable<AppRow> rows,HashSet<string> runningPaths){
   foreach(var row in rows){bool running=!String.IsNullOrWhiteSpace(row.Path)&&runningPaths.Contains(row.Path);row.Running=running;
    if(running){bool started=row.ItemStatus=="started";State(row,started?"started":"running","");row.State=started?"启动成功":"已在运行";row.Selected=false;}
    else if(row.ItemStatus=="configured"){row.State=row.Selected?"配置 · 将启动":"配置 · 不启动";}
    else if(row.ItemStatus=="launchfailed"){row.State="启动失败";}
    else {State(row,"closed","");row.State="未运行 / 可启动";}
   }
  }
  public List<AppRow> History(long batch) {return db.Query("SELECT id,name,path,args,admin,status,error,chosen FROM items WHERE batch=? ORDER BY id",batch.ToString()).Select(x=>new AppRow {ItemId=Int64.Parse(x[0]),Name=x[1],ProcessName=Path.GetFileName(x[2]),Path=x[2],Args=x[3],Admin=x[4]=="1",ItemStatus=x[5],State=StateText(x[5]),Selected=x[7]=="1",RiskReason=x[6],Error=x[6],ArgsKnown=true}).ToList();}
  public static string StateText(string state){switch(state){case "closed":return "待恢复";case "launched":return "已发送启动请求";case "started":return "启动成功";case "running":return "已在运行";case "launchfailed":return "启动失败 / 可重试";case "closefailed":return "关闭失败";case "cancelled":return "关闭已取消";case "skipped":return "已在运行 / 跳过";case "uncertain":return "操作中断 / 需核查";default:return state;}}
  public void RecoverInterrupted(){db.Execute("UPDATE items SET status='uncertain',error='上次操作中断；请核查程序是否运行，必要时手动开启；' WHERE status IN ('prepared','launching','launched')");}
 }
}
