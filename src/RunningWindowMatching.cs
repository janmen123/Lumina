using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Lumina {
    public static partial class RunningWindows {
        internal static bool IsFileManager(Entry entry) {
            if(entry==null||entry.IsFolder)return false;
            if(!string.IsNullOrEmpty(entry.LaunchArguments))return false;
            var target=(entry.LaunchTarget??"").Trim();
            if(target.Equals("文件资源管理器",StringComparison.OrdinalIgnoreCase))return true;
            if(string.Equals(Path.GetFileName(target),"explorer.exe",StringComparison.OrdinalIgnoreCase))return true;
            return string.Equals(Path.GetFileName(entry.Path??""),"File Explorer.lnk",StringComparison.OrdinalIgnoreCase);
        }
        static List<RunningWindow> MatchWindows(Entry entry,IEnumerable<string> paths) {
            return MatchWindows(entry,paths,windows);
        }
        internal static List<RunningWindow> MatchWindows(Entry entry,IEnumerable<string> paths,IEnumerable<RunningWindow> snapshot) {
            var available=snapshot.ToList();
            if(IsFileManager(entry))return available.Where(IsFolderWindow).ToList();
            var candidates=paths.Where(p=>!string.IsNullOrWhiteSpace(p)).ToArray();
            var exact=available.Where(w=>candidates.Contains(w.Path,StringComparer.OrdinalIgnoreCase)||WindowActivationRules.HostedMatch(entry,w.Path,w.AppId)).ToList();
            if(exact.Count>0)return exact;
            // A known executable is authoritative; title matching must not override its identity.
            if(candidates.Length>0)return available.Where(w=>WindowActivationRules.FallbackMatch(candidates,w.Path,w.ProcessName)).ToList();
            string key=(entry.Name??"").Trim();
            if(key.Equals("DSH",StringComparison.OrdinalIgnoreCase))key="DeepSeek Harness";
            if(key.Contains("抖音"))key="douyin";
            if(key.Length<2)return new List<RunningWindow>();
            return available.Where(w=>!string.IsNullOrEmpty(w.Path)&&!string.IsNullOrEmpty(w.Title)&&w.Title.IndexOf(key,StringComparison.OrdinalIgnoreCase)>=0&&Path.GetFileNameWithoutExtension(w.Path).IndexOf(key,StringComparison.OrdinalIgnoreCase)>=0).ToList();
        }
        internal static bool WindowMatch(Entry entry,RunningWindow window) {
            if(entry==null||window==null||entry.IsFolder)return false;
            if(IsFileManager(entry))return IsFolderWindow(window);
            var paths=WindowActivationRules.Executables(entry);
            if(paths.Any(p=>string.Equals(p,window.Path,StringComparison.OrdinalIgnoreCase)))return true;
            if(WindowActivationRules.HostedMatch(entry,window.Path,window.AppId))return true;
            return WindowActivationRules.FallbackMatch(paths,window.Path,window.ProcessName);
        }
    }
}
