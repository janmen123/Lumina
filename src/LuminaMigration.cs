using System;using System.Collections.Generic;
namespace Lumina {
    public partial class Controller {
        // Earlier builds stored the executable path in layout.json; keep those entries working
        // after the product was renamed to Lumina.
        void MigrateLegacyPaths(){
            bool changed=false;
            if(Prefs.Apps!=null)foreach(var entry in Prefs.Apps){if(entry==null)continue;
                string path=RenameLegacyPath(entry.Path),target=RenameLegacyPath(entry.LaunchTarget);
                if(path!=entry.Path){entry.Path=path;changed=true;}
                if(target!=entry.LaunchTarget){entry.LaunchTarget=target;changed=true;}
                if(string.Equals(entry.Name,"GlassBoard",StringComparison.OrdinalIgnoreCase)){entry.Name="Lumina";changed=true;}
            }
            if(Prefs.Dock!=null){if(RenameLegacyList(Prefs.Dock.ItemOrder))changed=true;if(RenameLegacyList(Prefs.Dock.RunningOrder))changed=true;}
            if(RenameLegacyList(Prefs.DockOrder))changed=true;
            if(changed){Save();Store.Trace("RECOVER migrated legacy GlassBoard paths");}
        }
        static bool RenameLegacyList(List<string> values){
            if(values==null)return false;bool changed=false;
            for(int index=0;index<values.Count;index++){string next=RenameLegacyPath(values[index]);if(next!=values[index]){values[index]=next;changed=true;}}
            return changed;
        }
        static string RenameLegacyPath(string value){
            if(string.IsNullOrEmpty(value))return value;
            return value.Replace("GlassBoard.exe","Lumina.exe").Replace("GlassBoard.lnk","Lumina.lnk");
        }
    }
}
