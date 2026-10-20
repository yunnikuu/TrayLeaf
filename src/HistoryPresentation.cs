using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
namespace TrayLeaf {
 internal static class HistoryPresentation {
  internal static void Configure(DataGrid grid){
   var name=grid.Columns.FirstOrDefault(c=>c.SortMemberPath=="Name");if(name!=null)name.SortDirection=ListSortDirection.Ascending;
   grid.Sorting+=delegate(object sender,DataGridSortingEventArgs e){
    if(String.IsNullOrEmpty(e.Column.SortMemberPath))return;
    e.Handled=true;var direction=e.Column.SortDirection==ListSortDirection.Ascending?ListSortDirection.Descending:ListSortDirection.Ascending;
    // An explicit header sort takes precedence over temporary history/close pins.
    foreach(var row in grid.Items.OfType<AppRow>()){row.HistoryPriority=0;row.ClosePriority=0;}
    foreach(var column in grid.Columns)column.SortDirection=null;e.Column.SortDirection=direction;ApplyOrder(grid);
   };
  }
  internal static void ApplyOrder(DataGrid grid){if(grid.ItemsSource==null)return;var view=CollectionViewSource.GetDefaultView(grid.ItemsSource) as ListCollectionView;if(view==null)return;
   var sorted=grid.Columns.FirstOrDefault(c=>c.SortDirection.HasValue&&!String.IsNullOrEmpty(c.SortMemberPath));
   using(view.DeferRefresh()){view.SortDescriptions.Clear();view.CustomSort=new RowComparer(sorted==null?"Name":sorted.SortMemberPath,sorted==null?ListSortDirection.Ascending:sorted.SortDirection.Value);}
  }
  sealed class RowComparer:IComparer {
   readonly string key;readonly int direction;internal RowComparer(string k,ListSortDirection d){key=k;direction=d==ListSortDirection.Ascending?1:-1;}
   static string Name(AppRow r){return !String.IsNullOrWhiteSpace(r.ProcessName)?r.ProcessName:!String.IsNullOrWhiteSpace(r.Path)?Path.GetFileName(r.Path):r.Name??"";}
   static int State(AppRow r){if(r.Running==true||r.State=="运行中"||r.State=="启动成功"||r.ItemStatus=="started"||r.ItemStatus=="running"||r.ItemStatus=="skipped")return 0;if(r.ItemStatus=="closed"||r.State=="已关闭")return 2;return 1;}
   public int Compare(object x,object y){var a=x as AppRow;var b=y as AppRow;if(a==null||b==null)return 0;
    int n=b.ClosePriority.CompareTo(a.ClosePriority);if(n!=0)return n;n=b.HistoryPriority.CompareTo(a.HistoryPriority);if(n!=0)return n;
    switch(key){case "MemoryMB":n=a.MemoryMB.CompareTo(b.MemoryMB);break;case "Source":n=StringComparer.CurrentCulture.Compare(a.Source??"",b.Source??"");break;case "Risk":case "RiskText":n=a.Risk.CompareTo(b.Risk);break;case "State":n=State(a).CompareTo(State(b));break;case "Admin":n=a.Admin.CompareTo(b.Admin);break;default:n=StringComparer.OrdinalIgnoreCase.Compare(Name(a),Name(b));break;}
    if(n!=0)return n*direction;n=StringComparer.OrdinalIgnoreCase.Compare(Name(a),Name(b));return n!=0?n:StringComparer.OrdinalIgnoreCase.Compare(a.Path??"",b.Path??"");
   }
  }
 }
}
