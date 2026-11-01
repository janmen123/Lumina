using System;using System.Collections.Generic;using System.Linq;
namespace Lumina {
    public static class DockLayoutRules {
        public const string Apps="glassboard://dock/apps",Computer="glassboard://dock/computer",Power="glassboard://dock/power",Network="glassboard://dock/network",Settings="glassboard://dock/settings",RecycleBin="glassboard://dock/recyclebin";
        public static readonly string[] SystemKeys={Apps,Computer,Power,Network,Settings,RecycleBin};
        public static bool SystemItem(string key){return SystemKeys.Contains(key,StringComparer.OrdinalIgnoreCase);}
        public static bool Visible(DockOptions o,string key){return key==RecycleBin?o.ShowRecycleBin:key==Computer?o.ShowComputer:key==Power?o.ShowPower:key==Network?o.ShowNetwork:key!=Settings||o.ShowSettings;}
        static List<string> Merge(IEnumerable<string> saved,List<string> apps){
            var allowed=new HashSet<string>(SystemKeys.Concat(apps),StringComparer.OrdinalIgnoreCase);var result=(saved??new string[0]).Where(k=>k!=null&&allowed.Contains(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if(result.Count==0)return new[]{Apps,Computer}.Concat(apps).Concat(new[]{Power,Network,Settings,RecycleBin}).ToList();
            foreach(string key in SystemKeys)if(!result.Contains(key,StringComparer.OrdinalIgnoreCase))result.Add(key);
            foreach(string key in apps)if(!result.Contains(key,StringComparer.OrdinalIgnoreCase)){int index=result.FindLastIndex(k=>!SystemItem(k));if(index<0)index=result.IndexOf(Computer);result.Insert(index<0?result.Count:index+1,key);}
            return result;
        }
        public static List<string> Common(Preferences p,bool visible=false){var keys=Merge(p.Dock.ItemOrder,DockOrderRules.Ordered(p).Select(a=>a.Path).ToList());return visible?keys.Where(k=>Visible(p.Dock,k)).ToList():keys;}
        public static List<string> Running(Preferences p,IEnumerable<string> tasks,bool visible=false){var apps=tasks.ToList();IEnumerable<string> initial=p.Dock.RunningOrder;if(initial==null||!initial.Any()){var projected=new List<string>();int index=0;foreach(string key in Common(p)){if(SystemItem(key))projected.Add(key);else if(index<apps.Count)projected.Add(apps[index++]);}if(index<apps.Count){int at=projected.FindLastIndex(k=>!SystemItem(k));projected.InsertRange(at<0?Math.Min(2,projected.Count):at+1,apps.Skip(index));}initial=projected;}var keys=Merge(initial,apps);return visible?keys.Where(k=>Visible(p.Dock,k)).ToList():keys;}
        public static void SyncApps(Preferences p){if(p.Dock==null)return;var apps=DockOrderRules.Ordered(p).Select(a=>a.Path).ToList();var keys=Common(p);int at=0;for(int i=0;i<keys.Count;i++)if(!SystemItem(keys[i]))keys[i]=apps[at++];p.Dock.ItemOrder=keys;}
        public static bool Move(Preferences p,string source,string target,bool before,IEnumerable<string> tasks=null){var order=tasks==null?Common(p):Running(p,tasks);int from=order.FindIndex(k=>string.Equals(k,source,StringComparison.OrdinalIgnoreCase));int to=target==null?order.Count:order.FindIndex(k=>string.Equals(k,target,StringComparison.OrdinalIgnoreCase));if(from<0||to<0||string.Equals(source,target,StringComparison.OrdinalIgnoreCase))return false;string key=order[from];var old=order.ToArray();order.RemoveAt(from);if(from<to)to--;if(target!=null&&!before)to++;order.Insert(Math.Max(0,Math.Min(order.Count,to)),key);if(order.SequenceEqual(old,StringComparer.OrdinalIgnoreCase))return false;if(tasks==null){p.Dock.ItemOrder=order;p.DockOrder=order.Where(k=>!SystemItem(k)).ToList();}else p.Dock.RunningOrder=order;return true;}
        public static bool Step(Preferences p,string source,int step,IEnumerable<string> tasks=null){var order=(tasks==null?Common(p,true):Running(p,tasks,true));int from=order.FindIndex(k=>string.Equals(k,source,StringComparison.OrdinalIgnoreCase));int to=from+step;return from>=0&&to>=0&&to<order.Count&&Math.Abs(step)==1&&Move(p,source,order[to],step<0,tasks);}
        public static DockDropTarget OnIcon(Preferences p,string source,string target,IEnumerable<string> tasks=null){var order=tasks==null?Common(p):Running(p,tasks);int from=order.FindIndex(k=>string.Equals(k,source,StringComparison.OrdinalIgnoreCase)),to=order.FindIndex(k=>string.Equals(k,target,StringComparison.OrdinalIgnoreCase));return from<0||to<0||from==to?null:new DockDropTarget{Path=target,Before=from>to};}
    }
}
