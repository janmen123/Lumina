using System;
using System.Collections.Generic;

namespace Lumina {
    // Keeps a small most-recently-used list so the board can offer a "recent first"
    // order without touching the user's manual arrangement.
    public static class RecentRules {
        public static void Touch(Preferences prefs,string path) {
            if(prefs==null||string.IsNullOrWhiteSpace(path))return;
            if(prefs.RecentUse==null)prefs.RecentUse=new List<string>();
            string key=path.Trim();
            prefs.RecentUse.RemoveAll(item=>PathOf(item).Equals(key,StringComparison.OrdinalIgnoreCase));
            prefs.RecentUse.Insert(0,DateTime.UtcNow.Ticks.ToString()+":"+key);
            if(prefs.RecentUse.Count>200)prefs.RecentUse.RemoveRange(200,prefs.RecentUse.Count-200);
        }
        public static long Stamp(Preferences prefs,string path) {
            if(prefs==null||prefs.RecentUse==null||string.IsNullOrWhiteSpace(path))return 0;
            string key=path.Trim();
            foreach(string item in prefs.RecentUse) {
                if(!PathOf(item).Equals(key,StringComparison.OrdinalIgnoreCase))continue;
                int split=item.IndexOf(':');
                long ticks;
                return split>0&&long.TryParse(item.Substring(0,split),out ticks)?ticks:0;
            }
            return 0;
        }
        static string PathOf(string item) {
            if(string.IsNullOrEmpty(item))return "";
            int split=item.IndexOf(':');
            return split>=0&&split+1<item.Length?item.Substring(split+1):item;
        }
    }
}
