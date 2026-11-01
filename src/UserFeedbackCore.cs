using System;using System.Collections.Generic;using System.Linq;using System.IO;using System.Runtime.Serialization;
namespace Lumina {
    public static class RemovalRules {
        public static string Identity(Entry entry){if(entry==null)return null;string target=WindowActivationRules.NormalizePath(entry.LaunchTarget);if(string.IsNullOrWhiteSpace(target))return "path|"+(WindowActivationRules.NormalizePath(entry.Path)??"");return "target|"+target+"|"+(entry.LaunchArguments??"").Trim();}
        public static bool Blocked(Preferences prefs,Entry entry){return (prefs.Removed??new List<string>()).Any(p=>string.Equals(WindowActivationRules.NormalizePath(p),WindowActivationRules.NormalizePath(entry.Path),StringComparison.OrdinalIgnoreCase))||(prefs.RemovedIdentities??new List<string>()).Contains(Identity(entry),StringComparer.OrdinalIgnoreCase);}
        public static void Remember(Preferences prefs,Entry entry){if(prefs.RemovedIdentities==null)prefs.RemovedIdentities=new List<string>();string key=Identity(entry);if(key!=null&&!prefs.RemovedIdentities.Contains(key,StringComparer.OrdinalIgnoreCase))prefs.RemovedIdentities.Add(key);}
        public static void Forget(Preferences prefs,Entry entry){if(prefs.RemovedIdentities!=null)prefs.RemovedIdentities.RemoveAll(key=>string.Equals(key,Identity(entry),StringComparison.OrdinalIgnoreCase));prefs.Removed.RemoveAll(p=>string.Equals(WindowActivationRules.NormalizePath(p),WindowActivationRules.NormalizePath(entry.Path),StringComparison.OrdinalIgnoreCase));}
        // A saved, present entry is authoritative. Tombstones only block automatic discovery
        // or prune a recovered older snapshot, never a valid current layout on upgrade.
        public static void ReconcileActive(Preferences prefs){if(prefs.Removed==null)prefs.Removed=new List<string>();if(prefs.RemovedIdentities==null)prefs.RemovedIdentities=new List<string>();foreach(var entry in prefs.Apps??new List<Entry>())Forget(prefs,entry);}
        public static bool Same(Entry one,Entry two){return !string.IsNullOrWhiteSpace(one.LaunchTarget)&&!string.IsNullOrWhiteSpace(two.LaunchTarget)&&string.Equals(Identity(one),Identity(two),StringComparison.OrdinalIgnoreCase);}
    }
    [DataContract] public class RemovalJournal {
        [DataMember(EmitDefaultValue=false)] public long SaveRevision;
        [DataMember] public List<string> Paths=new List<string>();
        [DataMember] public List<string> Identities=new List<string>();
        [DataMember] public List<string> ActivePaths=new List<string>();
        public void Merge(Preferences prefs){prefs.Removed=(prefs.Removed??new List<string>()).Concat(Paths??new List<string>()).Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();prefs.RemovedIdentities=(prefs.RemovedIdentities??new List<string>()).Concat(Identities??new List<string>()).Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();}
    }
    public static class DocumentRules {
        static readonly HashSet<string> extensions=new HashSet<string>(new[]{".doc",".docx",".docm",".dot",".dotx",".wps",".wpt",".xls",".xlsx",".xlsm",".xlsb",".xlt",".xltx",".et",".ett",".csv",".ppt",".pptx",".pptm",".dps",".dpt",".pdf",".txt",".md",".rtf",".odt",".ods",".odp",".jpg",".jpeg",".png",".webp",".svg",".mp4",".mp3",".zip",".rar",".7z"},StringComparer.OrdinalIgnoreCase);
        public static bool Supported(string path){return extensions.Contains(Path.GetExtension(path??""));}
    }
    public static class GameModeRules {
        public static bool Suppressed(bool manual,bool automatic,bool fullscreen,bool desktop){return manual||(automatic&&fullscreen&&!desktop);}
    }
}
