using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
namespace TrayLeaf {
 internal static class DataLocation {
  internal static string Marker {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TrayLeaf","data-location.txt");}}
  internal static string Resolve(string marker){if(File.Exists(marker)){string directory=File.ReadAllText(marker,Encoding.UTF8).Trim();if(String.IsNullOrWhiteSpace(directory)||!Path.IsPathRooted(directory))throw new IOException("数据目录记录无效，请检查："+marker);string file=Path.Combine(directory,"trayleaf.db");if(!File.Exists(file))throw new IOException("已设置的数据目录不可用，请连接对应磁盘后重试："+directory);return file;}return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TrayLeaf","trayleaf.db");}
  internal static void Save(string marker,string directory){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(marker)));string temp=marker+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,directory, new UTF8Encoding(false));if(File.Exists(marker))File.Replace(temp,marker,null);else File.Move(temp,marker);}finally{if(File.Exists(temp))File.Delete(temp);}}
 }
 public sealed partial class Database {
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_backup_init(IntPtr destination,byte[] destinationName,IntPtr source,byte[] sourceName);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_backup_step(IntPtr backup,int pages);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_backup_finish(IntPtr backup);
  public void Relocate(string directory,string locationFile){lock(gate){
   directory=Path.GetFullPath(directory);string target=Path.Combine(directory,"trayleaf.db");if(String.Equals(target,FilePath,StringComparison.OrdinalIgnoreCase))return;
   Directory.CreateDirectory(directory);if(File.Exists(target)||File.Exists(target+"-wal")||File.Exists(target+"-shm"))throw new IOException("所选目录已有数据库，请选择空目录，以免覆盖现有记录");
   string temp=Path.Combine(directory,".trayleaf-"+Guid.NewGuid().ToString("N")+".db");bool created=false;IntPtr destination=IntPtr.Zero;
   try{if(sqlite3_open16(temp,out destination)!=0)throw new IOException("无法写入所选数据目录");var name=Encoding.UTF8.GetBytes("main\0");IntPtr backup=sqlite3_backup_init(destination,name,db,name);if(backup==IntPtr.Zero)throw new IOException("无法开始数据库迁移");int rc,finish;try{rc=sqlite3_backup_step(backup,-1);}finally{finish=sqlite3_backup_finish(backup);}if(rc!=101||finish!=0)throw new IOException("数据库备份失败，原数据未改动（"+rc+"/"+finish+"）");sqlite3_close(destination);destination=IntPtr.Zero;
    using(var verify=new Database(temp)){var result=verify.Query("PRAGMA quick_check");if(result.Count!=1||result[0][0]!="ok")throw new IOException("迁移数据校验失败，原数据未改动");}
    File.Move(temp,target);created=true;using(var next=new Database(target)){DataLocation.Save(locationFile,directory);IntPtr old=db;db=next.db;next.db=IntPtr.Zero;FilePath=target;sqlite3_close(old);}created=false;
   }catch{if(created){foreach(string file in new[]{target,target+"-wal",target+"-shm"})try{if(File.Exists(file))File.Delete(file);}catch{}}throw;}
   finally{if(destination!=IntPtr.Zero)sqlite3_close(destination);foreach(string file in new[]{temp,temp+"-wal",temp+"-shm"})try{if(File.Exists(file))File.Delete(file);}catch{}}
  }}
  public List<AppRow> CustomPrograms(){return Query("SELECT m.path,m.name,m.args,m.admin FROM managed m INNER JOIN settings s ON s.key=('custom:' || m.path) COLLATE NOCASE WHERE s.value='1'").Select(x=>new AppRow{Path=x[0],Name=x[1],ProcessName=Path.GetFileNameWithoutExtension(x[0]),Args=x[2],Admin=x[3]=="1",Selected=false}).ToList();}
  public void RemoveCustomPrograms(IEnumerable<AppRow> rows){lock(gate){Execute("BEGIN IMMEDIATE");try{foreach(var row in rows){Execute("DELETE FROM settings WHERE key=? COLLATE NOCASE OR key=? COLLATE NOCASE","custom:"+row.Path,"args:"+row.Path);Execute("DELETE FROM managed WHERE path=?",row.Path);}Execute("COMMIT");}catch{Execute("ROLLBACK");throw;}}}
 }
}
