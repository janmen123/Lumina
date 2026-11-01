using System;
using Microsoft.Win32;

namespace Lumina {
    // Reads the Windows 10/11 app theme and reports changes, so Lumina can
    // follow the system light/dark setting instead of waiting for a manual toggle.
    public static class SystemTheme {
        const string Personalize=@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        public static bool IsLight() {
            try {
                using(var key=Registry.CurrentUser.OpenSubKey(Personalize)) {
                    var value=key==null?null:key.GetValue("AppsUseLightTheme");
                    if(value==null)return true;
                    return Convert.ToInt32(value)!=0;
                }
            } catch(Exception ex) { Store.Log(ex); return true; }
        }
        public static event Action Changed;
        static bool watching;
        public static void Watch() {
            if(watching)return; watching=true;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged+=PreferenceChanged;
        }
        public static void Stop() {
            if(!watching)return; watching=false;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged-=PreferenceChanged;
        }
        static void PreferenceChanged(object sender,UserPreferenceChangedEventArgs e) {
            // Windows raises General for a theme switch; Color covers accent changes.
            if(e.Category!=UserPreferenceCategory.General&&e.Category!=UserPreferenceCategory.Color)return;
            var handler=Changed; if(handler==null)return;
            try { handler(); } catch(Exception ex) { Store.Log(ex); }
        }
    }
}
