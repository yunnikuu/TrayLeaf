using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace TrayLeaf {
 internal static class ThemeManager {
 internal static readonly string[] Accents={"#128474","#286BC9","#7951B0","#B14E79","#985F12","#596A75"};
 internal static bool IsDark(string mode){if(mode=="1")return true;if(mode!="2")return false;try{using(var k=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))return Convert.ToInt32(k==null?1:k.GetValue("AppsUseLightTheme",1))==0;}catch{return false;}}
 internal static SolidColorBrush Brush(string hex){var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));b.Freeze();return b;}
 internal static void Apply(Window w,string mode,int accent){bool dark=IsDark(mode);string[] keys={"Canvas","Surface","Sidebar","Subtle","Text","Muted","Line","Hover","Warning"};string[] light={"#F3F6F5","#FFFFFF","#EDF2EF","#F7F9F8","#1E302A","#63776C","#DFE7E2","#E7EEEA","#98600F"};string[] night={"#151B1A","#202827","#19211F","#252F2C","#E7F0EB","#A7BAB0","#35433D","#2C3933","#EDBE75"};for(int i=0;i<keys.Length;i++)w.Resources[keys[i]]=Brush((dark?night:light)[i]);accent=Math.Max(0,Math.Min(Accents.Length-1,accent));var color=(Color)ColorConverter.ConvertFromString(Accents[accent]);if(dark)color=Color.FromRgb((byte)(color.R*.55+255*.45),(byte)(color.G*.55+255*.45),(byte)(color.B*.55+255*.45));w.Resources["Accent"]=new SolidColorBrush(color);w.Resources["AccentText"]=Brush(dark?"#14251F":"#FFFFFF");w.Resources["AccentSoft"]=new SolidColorBrush(Color.FromRgb((byte)(color.R*.15+(dark?28:255)*.85),(byte)(color.G*.15+(dark?37:255)*.85),(byte)(color.B*.15+(dark?33:255)*.85)));w.SetResourceReference(Control.ForegroundProperty,"Text");w.SetResourceReference(Control.BackgroundProperty,"Canvas");TextOptions.SetTextFormattingMode(w,TextFormattingMode.Ideal);TextOptions.SetTextRenderingMode(w,TextRenderingMode.Auto);}
 }
}
