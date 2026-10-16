using System;
using System.Collections.Generic;
using System.IO;
namespace TrayLeaf {
 public static class Policy {
  static readonly HashSet<string> RiskNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {"system","idle","registry","secure system","memory compression","smss","csrss","wininit","winlogon","lsass","lsaiso","services","svchost","dwm","fontdrvhost","sihost","taskhostw","audiodg","spoolsv","msmpeng","securityhealthservice","securityhealthsystray","explorer","trustedinstaller","winlogon","wmiprvse"};
  public static string SharedAction(bool pending) {return pending?"restore":"close";}
  public static bool HighRisk(string name,bool service,bool critical) {return service||critical||RiskNames.Contains(Path.GetFileNameWithoutExtension(name??""));}
  public static string Arguments(string commandLine) {
   if(String.IsNullOrWhiteSpace(commandLine))return "";
   string s=commandLine.TrimStart(); bool quoted=false; int slashes=0;
   for(int i=0;i<s.Length;i++) { char c=s[i]; if(c=='\\'){slashes++;continue;} if(c=='"'&&slashes%2==0)quoted=!quoted; else if(Char.IsWhiteSpace(c)&&!quoted)return s.Substring(i).TrimStart(); slashes=0; }
   return "";
  }
 }
}