using System;using System.Linq;using System.Windows.Controls;
namespace TrayLeaf {public sealed partial class Controller {
 int pageVisit;bool closeProgressShown;internal Func<AppRow,Func<AppRow,bool>,string> closeOperation;
 static string FormatUi(string key,params object[] values){return UiLanguage.Format(key,values);}
 void VisitStatus(int visit,string text){if(visit==pageVisit)Status(text);}
 void UpdateCloseProgress(){var text=Get<TextBlock>("CloseProgressText");if(text!=null&&!closeProgressShown)text.Text=FormatUi("已勾选{0}个程序",current.Count(r=>r.Selected));}
 void SetCloseProgress(int done,int total,int visit){if(visit!=pageVisit||page!=0)return;closeProgressShown=true;Text("CloseProgressText",FormatUi("关闭进度{0}/{1}",done,total));}
 void FinishCloseProgress(int done,int total,int visit){if(visit!=pageVisit||page!=0)return;closeProgressShown=true;Text("CloseProgressText",done==total?FormatUi("关闭完成喵 {0}个程序已关闭",done):FormatUi("{0}个程序已关闭 有{1}个程序关闭失败",done,total-done));}
 } }
