using System;
using System.Collections.Generic;
using System.IO;
<<<<<<< HEAD
using System.Linq;
namespace TrayLeaf {
 public static class Policy {
  static readonly HashSet<string> RiskNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {"system","idle","registry","secure system","memory compression","smss","csrss","wininit","winlogon","lsass","lsaiso","services","svchost","dwm","fontdrvhost","sihost","taskhostw","audiodg","spoolsv","msmpeng","securityhealthservice","securityhealthsystray","explorer","trustedinstaller","wmiprvse","avp","avgui","avgnt","avguard","avastui","avastsvc","bdagent","bdservicehost","bdss","bitdefender","ekrn","egui","mbam","mbamservice","mfevtps","mcshield","nortonsecurity","nissrv","savservice","savtray","sophoshealth","sophosui","tmccsf","tmbmsrv","usam","windefend","wrsa","wsctrl","360tray","360sd","qqpctray"};
  static readonly string[] AntivirusHints=new[]{"\\Windows Defender\\","\\Microsoft Security Client\\","\\Kaspersky Lab\\","\\Bitdefender\\","\\Avast Software\\","\\AVG\\","\\ESET\\","\\Malwarebytes\\","\\McAfee\\","\\Norton\\","\\Sophos\\","\\Trend Micro\\","\\Webroot\\","\\360Safe\\","\\360SD\\","\\QQPCMgr\\","\\Huorong\\","\\Kingsoft Antivirus\\","\\DrWeb\\","\\F-Secure\\","\\G DATA\\","\\Panda Security\\","\\Comodo\\"};
  public static string SharedAction(bool pending) {return pending?"restore":"close";}
  public static bool HighRisk(string name,bool service,bool critical) {return service||critical||RiskNames.Contains(Path.GetFileNameWithoutExtension(name??""));}
  static readonly HashSet<string> AntivirusNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"msmpeng","securityhealthservice","securityhealthsystray","avp","avpui","avgui","avgnt","avguard","avastui","avastsvc","aswengsrv","avastavwrapper","bdagent","bdservicehost","bdss","bitdefender","ekrn","egui","eguiproxy","mbam","mbamservice","mbamtray","mfevtps","mcshield","mcuicnt","mfeann","nortonsecurity","norton360","nswscsvc","nissrv","savservice","savtray","sophoshealth","sophosui","sophosfilescanner","tmccsf","tmbmsrv","pccntmon","coreserviceshell","usam","windefend","wrsa","wsctrl","360tray","360sd","360safe","zhudongfangyu","360rp","qqpctray","qqpcrtp","hipsdaemon","hipstray","wsctrlsvc","kxescore","kavstart","ksafetray","avcenter","avshadow","fshoster","fsulhoster","psanhost","psuaservice","avkservice","gdscan","dwengine","spideragent","cavwp","cmdagent","cis","immunetprotect","forticlient","fortitray","sentinelagent","csfalconservice"};
  public static bool IsAntivirus(string name,string path){var process=Path.GetFileNameWithoutExtension(name??"");return AntivirusNames.Contains(process)||AntivirusHints.Any(x=>(path??"").IndexOf(x,StringComparison.OrdinalIgnoreCase)>=0);}
=======
namespace TrayLeaf {
 public static class Policy {
  static readonly HashSet<string> RiskNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {"system","idle","registry","secure system","memory compression","smss","csrss","wininit","winlogon","lsass","lsaiso","services","svchost","dwm","fontdrvhost","sihost","taskhostw","audiodg","spoolsv","msmpeng","securityhealthservice","securityhealthsystray","explorer","trustedinstaller","winlogon","wmiprvse"};
  public static string SharedAction(bool pending) {return pending?"restore":"close";}
  public static bool HighRisk(string name,bool service,bool critical) {return service||critical||RiskNames.Contains(Path.GetFileNameWithoutExtension(name??""));}
>>>>>>> origin/main
  public static string Arguments(string commandLine) {
   if(String.IsNullOrWhiteSpace(commandLine))return "";
   string s=commandLine.TrimStart(); bool quoted=false; int slashes=0;
   for(int i=0;i<s.Length;i++) { char c=s[i]; if(c=='\\'){slashes++;continue;} if(c=='"'&&slashes%2==0)quoted=!quoted; else if(Char.IsWhiteSpace(c)&&!quoted)return s.Substring(i).TrimStart(); slashes=0; }
   return "";
  }
 }
<<<<<<< HEAD
}
=======
}
>>>>>>> origin/main
