using System;using System.Collections.Generic;using System.Linq;
namespace GlassBoard {
    // No native calls: these rules are shared by the UI and portable regression checks.
    public sealed class DockWheelState {
        public bool Running {get;private set;} long last=long.MinValue;int accumulated;
        public bool Scroll(int delta,long milliseconds){
            if(delta==0)return false;
            if(last!=long.MinValue&&milliseconds-last<280){accumulated=0;return false;}
            if(Math.Sign(accumulated)!=Math.Sign(delta))accumulated=0;
            accumulated+=delta;if(Math.Abs(accumulated)<120)return false;
            bool next=accumulated<0;accumulated=0;if(next==Running)return false;
            Running=next;last=milliseconds;return true;
        }
        public void Set(bool running){Running=running;accumulated=0;}
    }
    public sealed class DockPinResult {public int Added,Skipped;public List<string> Paths=new List<string>();}
    public static class DockPinRules {
        public static DockPinResult Apply(Preferences prefs,IEnumerable<Entry> candidates,DockDropTarget target){
            var result=new DockPinResult();var moved=new List<Entry>();
            foreach(var candidate in candidates){
                if(candidate==null||string.IsNullOrWhiteSpace(candidate.Path))continue;
                var entry=prefs.Apps.FirstOrDefault(a=>a.Pinned&&(string.Equals(a.Path,candidate.Path,StringComparison.OrdinalIgnoreCase)||RemovalRules.Same(a,candidate)))
                    ??prefs.Apps.FirstOrDefault(a=>string.Equals(a.Path,candidate.Path,StringComparison.OrdinalIgnoreCase))??candidate;
                if(moved.Contains(entry))continue;
                if(!entry.Pinned&&prefs.Apps.Count(a=>a.Pinned)>=20){result.Skipped++;continue;}
                if(!prefs.Apps.Contains(entry))prefs.Apps.Add(entry);
                if(!entry.Pinned){entry.Pinned=true;result.Added++;}
                RemovalRules.Forget(prefs,entry);moved.Add(entry);result.Paths.Add(entry.Path);
            }
            if(moved.Count==0)return result;
            var order=DockOrderRules.Ordered(prefs);var anchor=target==null?null:order.FirstOrDefault(a=>string.Equals(a.Path,target.Path,StringComparison.OrdinalIgnoreCase));
            // Dropping an existing icon on itself is a no-op.
            if(moved.Count==1&&anchor==moved[0])return result;
            int index=anchor==null?order.Count:order.IndexOf(anchor)+(target.Before?0:1);
            int removedBefore=order.Take(index).Count(a=>moved.Contains(a));order.RemoveAll(a=>moved.Contains(a));
            index=Math.Max(0,Math.Min(order.Count,index-removedBefore));order.InsertRange(index,moved);
            prefs.DockOrder=order.Select(a=>a.Path).ToList();return result;
        }
    }
}
