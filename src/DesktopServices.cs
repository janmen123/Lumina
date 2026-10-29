using System;using System.IO;using System.Text;using System.Linq;using System.Runtime.Serialization.Json;using System.Windows.Threading;using System.Threading.Tasks;using Microsoft.Win32;
namespace GlassBoard {
    public static class LaunchInfoService {
        public static Entry Read(string path){var entry=new Entry{Name=EntryPathRules.Name(path),Path=EntryPathRules.Normalize(path),IsFolder=Directory.Exists(path)};Enrich(entry);return entry;}
        public static void Enrich(Entry entry){string target=entry.Path??"",arguments="";string ext=System.IO.Path.GetExtension(target).ToLowerInvariant();
            if(Directory.Exists(target)){entry.IsFolder=true;entry.LaunchTarget=target;entry.LaunchArguments="";return;}
            if(ext==".lnk"){object shell=null,link=null;try{var type=Type.GetTypeFromProgID("WScript.Shell");shell=Activator.CreateInstance(type);link=type.InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,shell,new object[]{target});target=(string)link.GetType().InvokeMember("TargetPath",System.Reflection.BindingFlags.GetProperty,null,link,null);arguments=(string)link.GetType().InvokeMember("Arguments",System.Reflection.BindingFlags.GetProperty,null,link,null);}catch{}finally{if(link!=null&&System.Runtime.InteropServices.Marshal.IsComObject(link))System.Runtime.InteropServices.Marshal.ReleaseComObject(link);if(shell!=null&&System.Runtime.InteropServices.Marshal.IsComObject(shell))System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);}}
            else if(ext==".url")target=StartupEntryRules.ReadUrl(target);
            // Electron shortcuts can intentionally start a main app through Update.exe.
            if(StartupEntryRules.Auxiliary(target)&&arguments.IndexOf("--processStart",StringComparison.OrdinalIgnoreCase)>=0){var match=System.Text.RegularExpressions.Regex.Match(arguments,@"--processStart\s+(?:""([^""]+)""|([^\s]+))",System.Text.RegularExpressions.RegexOptions.IgnoreCase);if(match.Success){string main=match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value;if(!StartupEntryRules.Auxiliary(main))target=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(target)??"",main);}}
            entry.LaunchTarget=target;entry.LaunchArguments=arguments;entry.IsFolder=entry.IsFolder||Directory.Exists(target);
            try{if(System.IO.Path.GetExtension(target??"").Equals(".exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(target)){var info=System.Diagnostics.FileVersionInfo.GetVersionInfo(target);entry.ProductInfo=(info.ProductName??"")+" "+(info.FileDescription??"");}}catch{}
        }
        public static bool Primary(Entry entry){if(entry.IsFolder||!StartupEntryRules.Candidate(entry.Path)||StartupEntryRules.Auxiliary(entry.LaunchTarget))return false;string target=entry.LaunchTarget;if(string.IsNullOrWhiteSpace(target))return true;if(Directory.Exists(target))return false;string ext=System.IO.Path.GetExtension(target).ToLowerInvariant();return ext.Length==0||StartupEntryRules.GameUri(target)||(EntryRules.Importable(target)&&!DocumentRules.Supported(target));}
    }
}
