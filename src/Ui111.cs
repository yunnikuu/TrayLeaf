using System;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using System.Windows.Controls;using System.Windows.Data;using System.Windows.Threading;
namespace TrayLeaf {public sealed partial class Controller {
 bool restoreProgressShown,selectionOrderQueued,restoreOrderQueued;int restoreRefreshGeneration;
 internal Func<AppRow,HashSet<string>,string> restoreOperation=null;
 internal Func<IEnumerable<AppRow>,OperationService,Task> verifyRestored=null;
 internal Func<Task> refreshAfterRestore=null;
 void UpdateRestoreProgress(){var text=Get<TextBlock>("RestoreProgressText");if(text!=null&&!restoreProgressShown)text.Text=FormatUi("已勾选{0}个程序",history.Count(r=>r.Selected));}
 void SetRestoreProgress(int done,int total,int visit){if(visit!=pageVisit||page!=1)return;restoreProgressShown=true;Text("RestoreProgressText",FormatUi("恢复进度{0}/{1}",done,total));}
 void FinishRestoreProgress(int done,int total,int visit){if(visit!=pageVisit||page!=1)return;restoreProgressShown=true;Text("RestoreProgressText",done==total?FormatUi("恢复完成喵 {0}个程序已恢复",done):FormatUi("{0}个程序已恢复 有{1}个程序恢复失败",done,total-done));}
 void QueueSelectionOrder(bool past){if(past?restoreOrderQueued:selectionOrderQueued)return;if(past)restoreOrderQueued=true;else selectionOrderQueued=true;w.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(delegate{if(past)restoreOrderQueued=false;else selectionOrderQueued=false;if(exiting)return;var grid=Get<DataGrid>(past?"RestoreGrid":"ProcessGrid");if(!grid.CommitEdit(DataGridEditingUnit.Cell,true)||!grid.CommitEdit(DataGridEditingUnit.Row,true))return;HistoryPresentation.ApplyOrder(grid);}));}
 async void ScheduleRestoreRefresh(){int request=++restoreRefreshGeneration;try{await Task.Delay(3000);while(busy&&!exiting&&request==restoreRefreshGeneration)await Task.Delay(100);if(exiting||request!=restoreRefreshGeneration)return;if(refreshAfterRestore!=null){await refreshAfterRestore();return;}if(demo)return;SetBusy(true);try{await Scan();}finally{SetBusy(false);}}catch(Exception ex){if(!exiting)Status("刷新失败："+ex.Message);}}
 } }
