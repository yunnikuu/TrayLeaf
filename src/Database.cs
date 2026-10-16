using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace TrayLeaf {
 public sealed class Database:IDisposable {
  IntPtr db; readonly object gate=new object(); public string FilePath {get;private set;}
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)]static extern int sqlite3_open16(string file,out IntPtr db);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_close(IntPtr db);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_prepare_v2(IntPtr db,byte[] sql,int size,out IntPtr stmt,IntPtr tail);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_step(IntPtr stmt);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_finalize(IntPtr stmt);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_bind_text(IntPtr stmt,int pos,byte[] text,int length,IntPtr destroy);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_column_text(IntPtr stmt,int col);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_column_bytes(IntPtr stmt,int col);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_column_count(IntPtr stmt);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_errmsg(IntPtr db);
  [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern long sqlite3_last_insert_rowid(IntPtr db);
  static string Utf(IntPtr p,int size) {if(p==IntPtr.Zero||size==0)return "";byte[] b=new byte[size];Marshal.Copy(p,b,0,size);return Encoding.UTF8.GetString(b);}
  string Error() {IntPtr p=sqlite3_errmsg(db);int i=0;while(Marshal.ReadByte(p,i)!=0)i++;return Utf(p,i);}
  public Database(string file) {
   FilePath=Path.GetFullPath(file);Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
   if(sqlite3_open16(FilePath,out db)!=0){string error=Error();sqlite3_close(db);db=IntPtr.Zero;throw new IOException("数据库打开失败："+error);}
   Execute("PRAGMA busy_timeout=3000");Execute("PRAGMA journal_mode=WAL");Execute("PRAGMA foreign_keys=ON");
   Execute("CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY,value TEXT NOT NULL)");
   Execute("CREATE TABLE IF NOT EXISTS managed(path TEXT PRIMARY KEY COLLATE NOCASE,name TEXT NOT NULL,args TEXT NOT NULL,admin TEXT NOT NULL,chosen TEXT NOT NULL)");
   Execute("CREATE TABLE IF NOT EXISTS batches(id INTEGER PRIMARY KEY AUTOINCREMENT,created TEXT NOT NULL)");
   Execute("CREATE TABLE IF NOT EXISTS items(id INTEGER PRIMARY KEY AUTOINCREMENT,batch INTEGER NOT NULL REFERENCES batches(id) ON DELETE CASCADE,name TEXT NOT NULL,path TEXT NOT NULL,args TEXT NOT NULL,admin TEXT NOT NULL,status TEXT NOT NULL,error TEXT NOT NULL DEFAULT '')");
   Execute("CREATE TABLE IF NOT EXISTS hidden_programs(key TEXT PRIMARY KEY COLLATE NOCASE,name TEXT NOT NULL,path TEXT NOT NULL,process TEXT NOT NULL)");
   Execute("PRAGMA user_version=2");
  }
  IntPtr Prepare(string sql,string[] args) {
   IntPtr stmt;byte[] bytes=Encoding.UTF8.GetBytes(sql+"\0");int rc=sqlite3_prepare_v2(db,bytes,bytes.Length,out stmt,IntPtr.Zero);if(rc!=0)throw new IOException(Error());
   try {for(int i=0;i<args.Length;i++){byte[] b=Encoding.UTF8.GetBytes((args[i]??"")+"\0");if(sqlite3_bind_text(stmt,i+1,b,b.Length-1,new IntPtr(-1))!=0)throw new IOException(Error());}return stmt;}catch{sqlite3_finalize(stmt);throw;}
  }
  public void Execute(string sql,params string[] args) {lock(gate){IntPtr s=Prepare(sql,args);try{int r;do{r=sqlite3_step(s);}while(r==100);if(r!=101)throw new IOException(Error());}finally{sqlite3_finalize(s);}}}
  public List<string[]> Query(string sql,params string[] args) {lock(gate){IntPtr s=Prepare(sql,args);var result=new List<string[]>();try {int rc;while((rc=sqlite3_step(s))==100){string[] row=new string[sqlite3_column_count(s)];for(int i=0;i<row.Length;i++)row[i]=Utf(sqlite3_column_text(s,i),sqlite3_column_bytes(s,i));result.Add(row);}if(rc!=101)throw new IOException(Error());return result;}finally{sqlite3_finalize(s);}}}
  public string Get(string key,string fallback) {var rows=Query("SELECT value FROM settings WHERE key=?",key);return rows.Count==0?fallback:rows[0][0];}
  public void Set(string key,string value) {Execute("INSERT OR REPLACE INTO settings(key,value) VALUES(?,?)",key,value);}
  public void SaveManaged(string path,string name,string args,bool admin,bool chosen) {Execute("INSERT OR REPLACE INTO managed(path,name,args,admin,chosen) VALUES(?,?,?,?,?)",path,name,args,admin?"1":"0",chosen?"1":"0");}
  static string NameKey(string name){return String.IsNullOrWhiteSpace(name)?"":"name:"+name.ToUpperInvariant();}
  static string ProgramKey(AppRow row){return String.IsNullOrWhiteSpace(row.Path)?NameKey(row.ProcessName):"path:"+row.Path.ToUpperInvariant();}
  public List<AppRow> HiddenPrograms(){return Query("SELECT name,path,process FROM hidden_programs ORDER BY name COLLATE NOCASE").Select(x=>new AppRow{Name=x[0],Path=x[1],ProcessName=x[2],Selected=false}).ToList();}
  public List<AppRow> FilterVisible(IEnumerable<AppRow> rows){var keys=new HashSet<string>(Query("SELECT key FROM hidden_programs").Select(x=>x[0]),StringComparer.OrdinalIgnoreCase);return rows.Where(r=>!keys.Contains(ProgramKey(r))&&!keys.Contains(NameKey(r.ProcessName))).ToList();}
  public void HidePrograms(IEnumerable<AppRow> rows){lock(gate){Execute("BEGIN IMMEDIATE");try{foreach(var r in rows){string key=ProgramKey(r);if(key.Length==0)throw new ArgumentException("无法识别要移除的程序");Execute("INSERT OR REPLACE INTO hidden_programs(key,name,path,process) VALUES(?,?,?,?)",key,r.Name??r.ProcessName,r.Path??"",r.ProcessName??"");if(!String.IsNullOrEmpty(r.Path))Execute("UPDATE managed SET chosen='0' WHERE path=?",r.Path);}Execute("COMMIT");}catch{Execute("ROLLBACK");throw;}}}
  public void RestorePrograms(IEnumerable<AppRow> rows){lock(gate){Execute("BEGIN IMMEDIATE");try{foreach(var r in rows)Execute("DELETE FROM hidden_programs WHERE key=?",ProgramKey(r));Execute("COMMIT");}catch{Execute("ROLLBACK");throw;}}}
  public void AllowProgram(AppRow row){lock(gate){Execute("BEGIN IMMEDIATE");try{Execute("DELETE FROM hidden_programs WHERE key=? OR key=?",ProgramKey(row),NameKey(row.ProcessName));Execute("COMMIT");}catch{Execute("ROLLBACK");throw;}}}
  public long Batch(List<AppRow> rows) {lock(gate){Execute("BEGIN IMMEDIATE");try{Execute("INSERT INTO batches(created) VALUES(?)",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));long id=sqlite3_last_insert_rowid(db);foreach(var r in rows)Execute("INSERT INTO items(batch,name,path,args,admin,status) VALUES(?,?,?,?,?,?)",id.ToString(),r.Name,r.Path,r.Args,r.Admin?"1":"0","prepared");Execute("COMMIT");return id;}catch{Execute("ROLLBACK");throw;}}}
  public void Prune(){Execute("DELETE FROM batches WHERE id NOT IN (SELECT id FROM batches ORDER BY id DESC LIMIT 20) AND NOT EXISTS(SELECT 1 FROM items WHERE items.batch=batches.id AND status IN ('closed','launchfailed','prepared','launching','uncertain'))");}
  public void Dispose(){lock(gate){if(db!=IntPtr.Zero){sqlite3_close(db);db=IntPtr.Zero;}}}
 }
}