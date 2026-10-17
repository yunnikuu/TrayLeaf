using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace TrayLeaf {
 public sealed partial class Controller {
  bool loadingCollections;
  readonly HashSet<string> closeHistoryPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  void ApplyHistoryPins(){foreach(var row in current){if(closeHistoryPaths.Contains(row.Path??"")){if(row.HistoryOriginalSource==null)row.HistoryOriginalSource=row.Source;row.Source="历史记录";row.HistoryPriority=1;}else{if(row.HistoryOriginalSource!=null)row.Source=row.HistoryOriginalSource;row.HistoryOriginalSource=null;row.HistoryPriority=0;}}}
  void CompleteClose(long batch,int done,int total){db.Prune();ReloadHistory(batch);ReloadCollections();Status(CloseSummary(done,total));}
  static string CloseSummary(int done,int total){return done==total?"共计 "+total+" 个程序，已全部关闭":"共计 "+total+" 个程序，已关闭 "+done+" 个，未关闭 "+(total-done)+" 个；请查看状态";}
  void SetupCollections(){
   HistoryPresentation.Configure(Get<DataGrid>("ProcessGrid"));
   foreach(string kind in new[]{"close","restore"}){
    string prefix=kind=="close"?"Close":"Restore";string captured=kind;
    Click("Save"+prefix+"ProfileButton",delegate{SaveProfile(captured,false,null);});
    Click("Edit"+prefix+"ProfileButton",delegate{SaveProfile(captured,true,null);});
    Click("Apply"+prefix+"ProfileButton",delegate{ApplyProfile(captured);});
    Click("Delete"+prefix+"ProfileButton",delegate{DeleteProfile(captured);});
   }
   Click("SaveCloseHistoryProfileButton",delegate{var set=Get<ComboBox>("CloseHistoryCombo").SelectedItem as SavedSet;if(set==null){Status("先选择一条关闭历史");return;}SaveProfile("close",false,db.LoadSet(set.Id));});
   Get<ComboBox>("CloseHistoryCombo").SelectionChanged+=delegate{if(loadingCollections||busy)return;Guard(delegate{var set=Get<ComboBox>("CloseHistoryCombo").SelectedItem as SavedSet;if(set!=null)ApplyCloseRows(db.LoadSet(set.Id),"关闭历史");});};
   ProgramPresentation.AttachMenus(Get<DataGrid>("ProcessGrid"),Status);ProgramPresentation.AttachMenus(Get<DataGrid>("RestoreGrid"),Status);
   ReloadCollections();
  }
  void ReloadCollections(){loadingCollections=true;try{
   Get<ComboBox>("CloseHistoryCombo").ItemsSource=db.ListSets("history_close");
   foreach(string kind in new[]{"close","restore"}){var combo=Get<ComboBox>(kind=="close"?"CloseProfileCombo":"RestoreProfileCombo");var old=combo.SelectedItem as SavedSet;var sets=db.ListSets("profile_"+kind);combo.ItemsSource=sets;combo.SelectedItem=sets.FirstOrDefault(x=>old!=null&&x.Id==old.Id)??sets.FirstOrDefault();}
  }finally{loadingCollections=false;}}
  void SaveProfile(string kind,bool edit,List<AppRow> supplied){if(busy)return;var combo=Get<ComboBox>(kind=="close"?"CloseProfileCombo":"RestoreProfileCombo");var selected=combo.SelectedItem as SavedSet;
   if(edit&&selected==null){Status("先选择要编辑的配置");return;}
   var rows=edit?db.LoadSet(selected.Id):supplied??(kind=="close"?current:history);
   if(rows.Count==0){Status("当前列表没有程序；请先添加程序或选择历史");return;}
   var dialog=new ProfileEditor(w,kind,edit?selected.Name:"",rows,delegate(string name,List<AppRow> values){db.SaveProfile(kind,name,values,edit?selected.Id:0);});
   if(dialog.ShowDialog()==true){ReloadCollections();Status("配置已保存，勾选状态、启动参数与管理员权限均已记录");}
  }
  void DeleteProfile(string kind){if(busy)return;var set=Get<ComboBox>(kind=="close"?"CloseProfileCombo":"RestoreProfileCombo").SelectedItem as SavedSet;if(set==null){Status("先选择要删除的配置");return;}
   if(!Ask("确定删除配置“"+set.Name+"”？\n此操作不能撤销，不会删除程序或历史记录","删除配置"))return;
   db.DeleteProfile(kind,set.Id);ReloadCollections();Status("已删除配置："+set.Name);
  }
  void ApplyProfile(string kind){if(busy)return;var set=Get<ComboBox>(kind=="close"?"CloseProfileCombo":"RestoreProfileCombo").SelectedItem as SavedSet;if(set==null){Status("先选择一个配置");return;}
   var rows=db.LoadSet(set.Id);if(kind=="close"){ApplyCloseRows(rows,"关闭配置");return;}
   loading=true;try{Get<ComboBox>("BatchCombo").SelectedItem=null;}finally{loading=false;}
   history=rows;foreach(var r in history){r.ItemId=0;r.ItemStatus="configured";r.State=r.Selected?"配置 · 将启动":"配置 · 不启动";LoadIcon(r);Wire(r,true);}
   Get<DataGrid>("RestoreGrid").ItemsSource=history;Status("已应用恢复配置“"+set.Name+"”；勾选项将在点击“开启所选程序”时启动");
  }
  void ApplyCloseRows(List<AppRow> rows,string source){
   bool fromHistory=source=="关闭历史";closeHistoryPaths.Clear();ApplyHistoryPins();
   var byPath=rows.Where(r=>!String.IsNullOrWhiteSpace(r.Path)).GroupBy(r=>r.Path,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Last(),StringComparer.OrdinalIgnoreCase);
   if(fromHistory){foreach(string path in byPath.Keys){closeHistoryPaths.Add(path);db.Set("custom:"+path,"1");}Get<TextBox>("Search").Text="";}
   foreach(var r in current){AppRow saved;r.Selected=false;if(byPath.TryGetValue(r.Path??"",out saved)){r.Args=saved.Args;r.Admin=saved.Admin;r.Selected=fromHistory||saved.Selected;db.RememberLaunch(r);}}
   foreach(var saved in byPath.Values.Where(s=>!current.Any(r=>String.Equals(r.Path,s.Path,StringComparison.OrdinalIgnoreCase)))){
    var r=ProgramPresentation.Copy(saved);r.ItemId=0;r.ItemStatus=null;r.Source="自定义";r.State="请刷新识别";r.Selected=fromHistory||saved.Selected;db.Set("custom:"+r.Path,"1");db.AllowProgram(r);db.SaveManaged(r.Path,r.Name,r.Args,r.Admin,r.Selected);db.RememberLaunch(r);LoadIcon(r);Wire(r,false);current.Add(r);
   }
   ApplyHistoryPins();Get<DataGrid>("ProcessGrid").ItemsSource=null;Get<DataGrid>("ProcessGrid").ItemsSource=current;ApplyFilter();Status("");
  }
 }
}
