<<<<<<< HEAD
using System;
using System.IO;
using System.Xml;
namespace TrayLeaf {
 internal static class RuntimeConfiguration {
  const string DpiSwitch="Switch.System.Windows.DoNotScaleForDpiChanges";
  // CLR activation uses the assembly's v4 metadata. Apply this application-owned file before WPF starts.
  internal static void Load(string path){AppContext.SetSwitch(DpiSwitch,false);if(!File.Exists(path))return;
   try{var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536};
    using(var reader=XmlReader.Create(path,settings))while(reader.Read())if(reader.NodeType==XmlNodeType.Element&&reader.Name=="AppContextSwitchOverrides"){
     foreach(string part in (reader.GetAttribute("value")??"").Split(';')){var pair=part.Split('=');bool value;if(pair.Length==2&&pair[0].Trim()==DpiSwitch&&Boolean.TryParse(pair[1].Trim(),out value))AppContext.SetSwitch(DpiSwitch,value);}
    }
   }catch(XmlException){AppContext.SetSwitch(DpiSwitch,false);}catch(IOException){AppContext.SetSwitch(DpiSwitch,false);}catch(UnauthorizedAccessException){AppContext.SetSwitch(DpiSwitch,false);}
  }
 }
}
=======
using System;
using System.IO;
using System.Xml;
namespace TrayLeaf {
 internal static class RuntimeConfiguration {
  const string DpiSwitch="Switch.System.Windows.DoNotScaleForDpiChanges";
  // CLR activation uses the assembly's v4 metadata. Apply this application-owned file before WPF starts.
  internal static void Load(string path){AppContext.SetSwitch(DpiSwitch,false);if(!File.Exists(path))return;
   try{var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536};
    using(var reader=XmlReader.Create(path,settings))while(reader.Read())if(reader.NodeType==XmlNodeType.Element&&reader.Name=="AppContextSwitchOverrides"){
     foreach(string part in (reader.GetAttribute("value")??"").Split(';')){var pair=part.Split('=');bool value;if(pair.Length==2&&pair[0].Trim()==DpiSwitch&&Boolean.TryParse(pair[1].Trim(),out value))AppContext.SetSwitch(DpiSwitch,value);}
    }
   }catch(XmlException){AppContext.SetSwitch(DpiSwitch,false);}catch(IOException){AppContext.SetSwitch(DpiSwitch,false);}catch(UnauthorizedAccessException){AppContext.SetSwitch(DpiSwitch,false);}
  }
 }
}
>>>>>>> origin/main
