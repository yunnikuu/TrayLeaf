using System;
using System.Windows;
namespace TrayLeaf { internal static class WindowSizing {
 internal static void ApplyDefault(Window w,Rect area){w.MinWidth=Math.Min(1040,Math.Max(640,area.Width-32));w.MinHeight=Math.Min(680,Math.Max(440,area.Height-32));w.Width=Math.Min(1240,Math.Max(w.MinWidth,area.Width-32));w.Height=Math.Min(820,Math.Max(w.MinHeight,area.Height-32));}
}}
