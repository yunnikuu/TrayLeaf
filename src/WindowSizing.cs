using System;
using System.Windows;
namespace TrayLeaf {
 internal static class WindowSizing {
  internal static void ApplyDefault(Window window,Rect workArea){double width=Math.Max(320,workArea.Width-32),height=Math.Max(240,workArea.Height-32);
   window.MinWidth=Math.Min(960,width);window.MinHeight=Math.Min(640,height);window.Width=Math.Min(1280,width);window.Height=Math.Min(960,height);
  }
 }
}
