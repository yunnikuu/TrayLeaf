using System;
using System.Windows;
using System.Windows.Controls;
namespace TrayLeaf {
 public sealed partial class Controller {
  void MigratePalette098(){if(db.Get("paletteVersion","")=="098")return;db.Execute("BEGIN IMMEDIATE");try{foreach(string key in new[]{"accent","windowTint"}){string raw=db.Get(key,"");int old;if(Int32.TryParse(raw,out old)&&old>=0&&old<6)db.Set(key,(old+1).ToString());}db.Set("paletteVersion","098");db.Execute("COMMIT");}catch{db.Execute("ROLLBACK");throw;}}
  void Setup098(){var input=Get<ComboBox>("LanguageInput");input.SelectedIndex=UiLanguage.Code=="zh-Hant"?1:0;input.SelectionChanged+=delegate{UiLanguage.Set(input.SelectedIndex==1?"zh-Hant":"zh-Hans");if(tray!=null)tray.Text=UiLanguage.Text("轻羽归栈 · 后台暂时休息");};UiLanguage.Apply(w);if(trayMenu!=null)UiLanguage.Apply(trayMenu);}
 }
}
