using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Lumina {
    // 判断前台窗口是不是游戏。只按安装目录认：游戏平台的库目录有稳定特征，
    // 而窗口形状（全屏、无标题栏）在"窗口化"模式下不可靠。
    public static class GameDetection {
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint id);

        // 只列"库目录里一定是游戏"的特征；平台客户端本身（steam.exe、EpicGamesLauncher.exe、
        // Battle.net.exe）不匹配，否则打开商店就把 Dock 藏了。
        static readonly string[] Markers={
            "\\steamapps\\common\\",
            "\\epic games\\",
            "\\gog games\\",
            "\\gog galaxy\\games\\",
            "\\wegameapps\\",
            "\\riot games\\",
            "\\xboxgames\\",
            "\\ubisoft game launcher\\games\\"
        };

        // 平台客户端的可执行文件也在库目录附近，这些路径不算游戏。
        static readonly string[] Clients={
            "\\epic games\\launcher\\",
            "\\riot games\\riot client\\",
            "\\steam\\bin\\",
            "\\wegame\\",
            "\\wallpaper_engine\\",
            "\\steamworks shared\\"
        };

        public static bool IsGamePath(string path) {
            if(string.IsNullOrEmpty(path))return false;
            string value=path.Replace('/','\\').ToLowerInvariant();
            if(Clients.Any(c=>value.Contains(c)))return false;
            return Markers.Any(m=>value.Contains(m));
        }

        static IntPtr cachedWindow;
        static uint cachedProcess;
        static bool cachedResult;
        static DateTime cachedAt=DateTime.MinValue;

        public static bool ForegroundIsGame() {
            IntPtr foreground=GetForegroundWindow();
            if(foreground==IntPtr.Zero)return false;
            uint id;
            if(GetWindowThreadProcessId(foreground,out id)==0||id==0)return false;
            DateTime now=DateTime.UtcNow;
            if(foreground==cachedWindow&&id==cachedProcess&&(now-cachedAt).TotalMilliseconds<1500)return cachedResult;
            bool result=false;
            try {
                result=IsGamePath(RunningWindows.ProcessPath(id));
            }
            catch {
                result=false;
            }
            cachedWindow=foreground;
            cachedProcess=id;
            cachedResult=result;
            cachedAt=now;
            return result;
        }
    }
}
