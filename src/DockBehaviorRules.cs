using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace GlassBoard {
    public static class DockOrderRules {
        public static List<Entry> Ordered(Preferences prefs){
            var pinned=prefs.Apps.Where(a=>a!=null&&a.Pinned&&!string.IsNullOrWhiteSpace(a.Path)).ToList();
            var byPath=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);foreach(var entry in pinned)if(!byPath.ContainsKey(entry.Path))byPath[entry.Path]=entry;
            var result=new List<Entry>();foreach(var path in prefs.DockOrder??new List<string>()){Entry entry;if(path!=null&&byPath.TryGetValue(path,out entry)){result.Add(entry);byPath.Remove(path);}}
            foreach(var entry in pinned)if(byPath.Remove(entry.Path))result.Add(entry);return result;
        }
        public static void Normalize(Preferences prefs){prefs.DockOrder=Ordered(prefs).Select(a=>a.Path).ToList();DockLayoutRules.SyncApps(prefs);}
        public static bool Move(Preferences prefs,string source,string target,bool before){
            var ordered=Ordered(prefs);var moved=ordered.FirstOrDefault(a=>string.Equals(a.Path,source,StringComparison.OrdinalIgnoreCase));
            if(moved==null||string.Equals(source,target,StringComparison.OrdinalIgnoreCase))return false;
            var destination=string.IsNullOrEmpty(target)?null:ordered.FirstOrDefault(a=>string.Equals(a.Path,target,StringComparison.OrdinalIgnoreCase));if(!string.IsNullOrEmpty(target)&&destination==null)return false;
            var original=ordered.Select(a=>a.Path).ToArray();ordered.Remove(moved);int index=destination==null?ordered.Count:ordered.IndexOf(destination)+(before?0:1);ordered.Insert(index,moved);
            var next=ordered.Select(a=>a.Path).ToList();if(next.SequenceEqual(original,StringComparer.OrdinalIgnoreCase))return false;prefs.DockOrder=next;DockLayoutRules.SyncApps(prefs);return true;
        }
        public static bool Step(Preferences prefs,string source,int step){var ordered=Ordered(prefs);int index=ordered.FindIndex(a=>string.Equals(a.Path,source,StringComparison.OrdinalIgnoreCase)),next=index+step;if(index<0||next<0||next>=ordered.Count||Math.Abs(step)!=1)return false;return Move(prefs,source,ordered[next].Path,step<0);}
    }
    public sealed class DockDropTarget {public string Path;public bool Before;}
    public static class DockDropRules {
        public static DockDropTarget OnIcon(Preferences prefs,string source,string target){var order=DockOrderRules.Ordered(prefs).Select(a=>a.Path).ToList();int from=order.FindIndex(p=>string.Equals(p,source,StringComparison.OrdinalIgnoreCase)),to=order.FindIndex(p=>string.Equals(p,target,StringComparison.OrdinalIgnoreCase));if(from<0||to<0||from==to)return null;return new DockDropTarget{Path=target,Before=from>to};}
        public static DockDropTarget OnGap(string source,string[] visible,double[] centers,double x){if(visible==null||centers==null||visible.Length!=centers.Length||double.IsNaN(x)||double.IsInfinity(x))return null;for(int i=0;i<visible.Length;i++)if(!string.Equals(visible[i],source,StringComparison.OrdinalIgnoreCase)&&x<centers[i])return new DockDropTarget{Path=visible[i],Before=true};return new DockDropTarget{Path=null,Before=false};}
    }
    public sealed class DockPointerResult {public bool Click,Dragged;public DockDropTarget Target;}
    public sealed class DockPointerGesture {
        public readonly string Source;readonly double startX,startY,thresholdX,thresholdY;
        public bool Active{get;private set;}public bool Dragging{get;private set;}
        public DockPointerGesture(string source,double x,double y,double minimumX,double minimumY){Source=source;startX=x;startY=y;thresholdX=Math.Max(1,minimumX);thresholdY=Math.Max(1,minimumY);Active=true;}
        public void Move(double x,double y){if(!Active||double.IsNaN(x)||double.IsNaN(y)||double.IsInfinity(x)||double.IsInfinity(y))return;if(Math.Abs(x-startX)>=thresholdX||Math.Abs(y-startY)>=thresholdY)Dragging=true;}
        public DockPointerResult Release(double x,double y,bool inside,DockDropTarget target,bool allowClick=true){if(!Active)return new DockPointerResult();if(double.IsNaN(x)||double.IsNaN(y)||double.IsInfinity(x)||double.IsInfinity(y)){Active=false;return new DockPointerResult{Dragged=Dragging};}Move(x,y);Active=false;return new DockPointerResult{Click=!Dragging&&allowClick,Dragged=Dragging,Target=Dragging&&inside?target:null};}
        public void Cancel(){Active=false;}
    }
    public static class WindowActivationRules {
        internal static long SelectDock(IEnumerable<long> windows,long foreground,long previous){
            var handles=windows.Where(h=>h!=0).Distinct().ToList();
            return handles.Contains(foreground)?foreground:handles.Contains(previous)?previous:handles.FirstOrDefault();
        }
        internal static bool MinimizeDock(bool isForeground,bool isMinimized){return isForeground&&!isMinimized;}
        public static string NormalizePath(string path){if(string.IsNullOrWhiteSpace(path))return null;string value=path.Trim().Trim('"').Replace('/','\\');return value.StartsWith(@"\\?\",StringComparison.Ordinal)?value.Substring(4):value;}
        public static string[] Executables(Entry entry){if(entry==null||entry.IsFolder)return new string[0];var manual=NormalizePath(entry.WindowTarget);if(manual!=null&&Path.GetExtension(manual).Equals(".exe",StringComparison.OrdinalIgnoreCase))return new[]{manual};return new[]{entry.LaunchTarget,entry.Path}.Select(NormalizePath).Where(p=>p!=null&&Path.GetExtension(p).Equals(".exe",StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
        public static bool HostedMatch(Entry entry,string path,string appId){
            if(entry==null)return false;
            // A manual association is authoritative.
            if(!string.IsNullOrWhiteSpace(entry.WindowTarget))return false;
            if(!string.IsNullOrWhiteSpace(appId)&&string.Equals(entry.LaunchTarget,appId,StringComparison.OrdinalIgnoreCase))return true;
            var target=NormalizePath(entry.LaunchTarget);path=NormalizePath(path);
            if(target==null||path==null||!Path.GetFileName(target).Equals("steam.exe",StringComparison.OrdinalIgnoreCase))return false;
            // Only Steam's own helper under this installation, never games or another Steam install.
            return string.Equals(path,Path.Combine(Path.GetDirectoryName(target),@"bin\cef\cef.win64\steamwebhelper.exe"),StringComparison.OrdinalIgnoreCase)
                ||string.Equals(path,Path.Combine(Path.GetDirectoryName(target),@"bin\cef\cef.win7\steamwebhelper.exe"),StringComparison.OrdinalIgnoreCase);
        }
        public static long Select(IEnumerable<long> windows,long foreground,long previous,bool ownForeground){
            var handles=windows.Where(h=>h!=0).Distinct().OrderBy(h=>h).ToList();if(handles.Count==0)return 0;
            int index=handles.IndexOf(foreground);if(index>=0)return handles[(index+1)%handles.Count];
            int last=handles.IndexOf(previous);if(last>=0)return handles[ownForeground?(last+1)%handles.Count:last];return handles[0];
        }
    }
}
namespace GlassBoard {
    public static class DockRevealRules {
        public static double[] Stops(double current,double target,double intensity,double top){double bounce=System.Math.Min(12,System.Math.Abs(current-target)*.07)*System.Math.Max(.5,System.Math.Min(1.5,intensity));return new[]{current,System.Math.Max(top,target-bounce),target+bounce*.22,target};}
    }
}
