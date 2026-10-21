using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace TrayLeaf {
 internal static class TablePresentation {
  internal static void Configure(Window window,params DataGrid[] grids) {
   Action fit=delegate {foreach(var grid in grids)foreach(var column in grid.Columns){
    string header=Convert.ToString(column.Header);if(header.Length==0)continue;
    double minimum=Measure(window,header)+52;
    if(column.SortMemberPath=="State")minimum=Math.Max(minimum,grid.Name=="ProcessGrid"?140:180);
     if(column.SortMemberPath=="MemoryMB")minimum=Math.Max(minimum,Measure(window,"123,456 MB")+28);
    // A user can resize every column, but not below the label/value safety width.
    column.MinWidth=Math.Max(header=="程序 / 进程"?170:header=="已记录的程序"?230:80,Math.Ceiling(minimum));
   }};
   fit();
   DependencyPropertyDescriptor.FromProperty(Control.FontFamilyProperty,typeof(Window)).AddValueChanged(window,delegate{fit();});
   DependencyPropertyDescriptor.FromProperty(Control.FontSizeProperty,typeof(Window)).AddValueChanged(window,delegate{fit();});
  }
  static double Measure(Window window,string text){return new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(window.FontFamily,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),window.FontSize,Brushes.Black,VisualTreeHelper.GetDpi(window).PixelsPerDip).WidthIncludingTrailingWhitespace;}
 }
}
