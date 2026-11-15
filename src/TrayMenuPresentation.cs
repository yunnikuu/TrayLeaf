using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Controls.Primitives;
namespace TrayLeaf { internal static class TrayMenuPresentation {
 internal static ContextMenu Create(Window owner){var resources=new ResourceDictionary();foreach(object key in owner.Resources.Keys)resources[key]=owner.Resources[key];
 string[] keys={"Canvas","Surface","PopupSurface","StatusSurface","Sidebar","Subtle","Text","Muted","Line","Hover","Accent","AccentSoft","AccentText","Warning"};
 string[] colors={"#F3F6F5","#FFFFFF","#FFFFFF","#F7F9F8","#EDF2EF","#F7F9F8","#1E302A","#63776C","#DFE7E2","#E7EEEA","#128474","#E0F2EC","#FFFFFF","#98600F"};
 for(int i=0;i<keys.Length;i++)resources[keys[i]]=ThemeManager.Brush(colors[i]);resources["GlassEnabled"]=false;
 return new ContextMenu{Resources=resources,Placement=PlacementMode.MousePoint,FontFamily=owner.FontFamily,FontSize=owner.FontSize,Background=(Brush)resources["Surface"],Foreground=(Brush)resources["Text"]};
 } } }
