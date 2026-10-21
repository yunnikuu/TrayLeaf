using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace TrayLeaf {
 public sealed class SavedSet {
  public long Id {get;set;} public string Kind {get;set;} public string Name {get;set;} public string Created {get;set;} public int Count {get;set;}
  public override string ToString(){return Name+"  ·  "+Count+" 项";}
 }
 public sealed partial class Database {
  void InitializeCollections(){lock(gate){Execute("BEGIN IMMEDIATE");try{
   if(!Query("PRAGMA table_info(items)").Any(x=>x[1]=="chosen")){
    Execute("ALTER TABLE items ADD COLUMN chosen TEXT NOT NULL DEFAULT '1'");
    Execute("UPDATE items SET chosen=COALESCE((SELECT value FROM settings WHERE key='restore:'||items.id),'1')");
   }
   Execute("DELETE FROM settings WHERE key LIKE 'restore:%'");
   Execute("CREATE TABLE IF NOT EXISTS saved_sets(id INTEGER PRIMARY KEY AUTOINCREMENT,kind TEXT NOT NULL,name TEXT NOT NULL,created TEXT NOT NULL)");
   Execute("CREATE TABLE IF NOT EXISTS saved_entries(id INTEGER PRIMARY KEY AUTOINCREMENT,set_id INTEGER NOT NULL REFERENCES saved_sets(id) ON DELETE CASCADE,name TEXT NOT NULL,path TEXT NOT NULL,args TEXT NOT NULL,admin TEXT NOT NULL,chosen TEXT NOT NULL,state TEXT NOT NULL)");
   Execute("CREATE INDEX IF NOT EXISTS saved_kind ON saved_sets(kind,id)");
   // Backfill existing close batches once, before trimming old batches.
   if(Get("collectionsMigrated","0")!="1"){
    var ops=new OperationService(this);
    foreach(var b in Query("SELECT id,created FROM (SELECT id,created FROM batches ORDER BY id DESC LIMIT 10) ORDER BY id"))InsertSet("history_close",b[1],ops.History(Int64.Parse(b[0])));
    Set("collectionsMigrated","1");
   }
   PruneCore();Execute("PRAGMA user_version=3");Execute("COMMIT");
  }catch{Execute("ROLLBACK");throw;}}}
  void PruneCore(){
   Execute("DELETE FROM batches WHERE id NOT IN (SELECT id FROM batches ORDER BY id DESC LIMIT 10)");
   Execute("DELETE FROM saved_sets WHERE kind='history_close' AND id NOT IN (SELECT id FROM saved_sets WHERE kind='history_close' ORDER BY id DESC LIMIT 10)");
   Execute("DELETE FROM settings WHERE key LIKE 'restore:%'");
  }
  static void Kind(string kind){if(kind!="close"&&kind!="restore")throw new ArgumentException("未知配置类型");}
  long InsertSet(string kind,string name,IEnumerable<AppRow> rows){
   Execute("INSERT INTO saved_sets(kind,name,created) VALUES(?,?,?)",kind,name,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));long id=sqlite3_last_insert_rowid(db);
   foreach(var row in rows)Execute("INSERT INTO saved_entries(set_id,name,path,args,admin,chosen,state) VALUES(?,?,?,?,?,?,?)",id.ToString(),row.Name??"",row.Path??"",row.Args??"",row.Admin?"1":"0",row.Selected?"1":"0",row.ItemStatus??"");
   return id;
  }
  public long SaveProfile(string kind,string name,IEnumerable<AppRow> rows,long existing){Kind(kind);name=(name??"").Trim();if(name.Length==0||name.Length>50)throw new ArgumentException("配置名称需为 1–50 个字符");
   var entries=rows.ToList();if(entries.Count==0)throw new ArgumentException("请先添加程序");
   foreach(var row in entries){if(String.IsNullOrWhiteSpace(row.Path)||!Path.IsPathRooted(row.Path))throw new ArgumentException("配置中存在无效的程序路径");if(row.Args.IndexOf('\0')>=0||row.Args.Length>30000)throw new ArgumentException("启动参数无效或过长");}
   lock(gate){Execute("BEGIN IMMEDIATE");try{
    string type="profile_"+kind;long id=existing;
    if(existing==0){if(ListSets(type).Count>=5)throw new InvalidOperationException("此类配置最多保存 5 个，请先删除或编辑已有配置");id=InsertSet(type,name,entries);}
    else{
     if(Query("SELECT id FROM saved_sets WHERE id=? AND kind=?",existing.ToString(),type).Count==0)throw new InvalidOperationException("配置已不存在，请刷新后重试");
     Execute("UPDATE saved_sets SET name=? WHERE id=?",name,id.ToString());Execute("DELETE FROM saved_entries WHERE set_id=?",id.ToString());
     foreach(var row in entries)Execute("INSERT INTO saved_entries(set_id,name,path,args,admin,chosen,state) VALUES(?,?,?,?,?,?,?)",id.ToString(),row.Name??"",row.Path??"",row.Args??"",row.Admin?"1":"0",row.Selected?"1":"0",row.ItemStatus??"");
    }
    Execute("COMMIT");return id;
   }catch{Execute("ROLLBACK");throw;}}
  }
  public List<SavedSet> ListSets(string kind){return Query("SELECT s.id,s.kind,s.name,s.created,COUNT(e.id) FROM saved_sets s LEFT JOIN saved_entries e ON s.id=e.set_id WHERE s.kind=? GROUP BY s.id ORDER BY s.id DESC",kind).Select(x=>new SavedSet{Id=Int64.Parse(x[0]),Kind=x[1],Name=x[2],Created=x[3],Count=Int32.Parse(x[4])}).ToList();}
  public List<AppRow> LoadSet(long id){return Query("SELECT name,path,args,admin,chosen,state FROM saved_entries WHERE set_id=? ORDER BY id",id.ToString()).Select(x=>new AppRow{Name=x[0],Path=x[1],ProcessName=Path.GetFileName(x[1]),Args=x[2],Admin=x[3]=="1",Selected=x[4]=="1",ItemStatus=x[5],ArgsKnown=true}).ToList();}
  public void DeleteProfile(string kind,long id){Kind(kind);Execute("DELETE FROM saved_sets WHERE id=? AND kind=?",id.ToString(),"profile_"+kind);}
  public void RememberLaunch(AppRow row){if(String.IsNullOrWhiteSpace(row.Path))return;lock(gate){Execute("BEGIN IMMEDIATE");try{
   Execute("INSERT OR IGNORE INTO managed(path,name,args,admin,chosen) VALUES(?,?,?,?,?)",row.Path,row.Name??Path.GetFileNameWithoutExtension(row.Path),row.Args,row.Admin?"1":"0","0");
   Execute("UPDATE managed SET args=?,admin=? WHERE path=?",row.Args,row.Admin?"1":"0",row.Path);Set("args:"+row.Path,"1");Execute("COMMIT");
  }catch{Execute("ROLLBACK");throw;}}}
  public void SaveHistoryChoice(AppRow row){if(row.ItemId<=0)return;Execute("UPDATE items SET args=?,admin=?,chosen=? WHERE id=?",row.Args,row.Admin?"1":"0",row.Selected?"1":"0",row.ItemId.ToString());}
 }
}
