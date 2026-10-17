using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
namespace TrayLeaf {
 internal static class HistoryPresentation {
  internal static void Configure(DataGrid grid){grid.Sorting+=delegate(object sender,DataGridSortingEventArgs e){
   if(String.IsNullOrEmpty(e.Column.SortMemberPath))return;
   e.Handled=true;var direction=e.Column.SortDirection==ListSortDirection.Ascending?ListSortDirection.Descending:ListSortDirection.Ascending;
   foreach(var column in grid.Columns)column.SortDirection=null;e.Column.SortDirection=direction;ApplyOrder(grid);
  };}
  internal static void ApplyOrder(DataGrid grid){if(grid.ItemsSource==null)return;var view=CollectionViewSource.GetDefaultView(grid.ItemsSource);if(!view.CanSort)return;
   var sorted=grid.Columns.FirstOrDefault(c=>c.SortDirection.HasValue&&!String.IsNullOrEmpty(c.SortMemberPath));
   using(view.DeferRefresh()){view.SortDescriptions.Clear();view.SortDescriptions.Add(new SortDescription("HistoryPriority",ListSortDirection.Descending));if(sorted!=null)view.SortDescriptions.Add(new SortDescription(sorted.SortMemberPath,sorted.SortDirection.Value));}
  }
 }
}
