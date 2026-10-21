using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]
namespace TrayLeaf {
 public sealed class FontChoice {
  public string Source {get;set;}
  public string Label {get;set;}
 }
 public static class Appearance {
  public const string DefaultFont="Microsoft YaHei";
  // Does not enumerate the installed-font catalog at startup; only resolve the saved family.
  public static FontFamily Resolve(string source) {
   if(!String.IsNullOrWhiteSpace(source)&&source.Length<=200&&source.IndexOfAny(new[]{'/', '\\', ':', '#', ','})<0)try {
    var family=new FontFamily(source);
    // Composite families have no single GlyphTypeface. Match their resolved names without loading the full catalog.
    if(family.FamilyNames.Values.Any(name=>String.Equals(name,source,StringComparison.OrdinalIgnoreCase))||family.GetTypefaces().Any(t=>{GlyphTypeface glyph;return t.TryGetGlyphTypeface(out glyph);}))return family;
   }catch(ArgumentException){}catch(System.IO.IOException){}
   return new FontFamily(DefaultFont);
  }
  public static List<FontChoice> InstalledFonts() {
   var language=System.Windows.Markup.XmlLanguage.GetLanguage("zh-CN");
   return Fonts.SystemFontFamilies.Select(f=>{string name;return new FontChoice{Source=f.Source,Label=(f.FamilyNames.TryGetValue(language,out name)?name:f.Source)+"  ·  "+f.Source};})
    .OrderBy(f=>f.Label,StringComparer.Create(CultureInfo.GetCultureInfo("zh-CN"),true)).ToList();
  }
 }
}
