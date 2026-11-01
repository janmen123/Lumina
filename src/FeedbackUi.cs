using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
namespace Lumina {
    public static class FileShellActions {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct SHELLEXECUTEINFO {
            public int Size;
            public uint Mask;
            public IntPtr Window;
            [MarshalAs(UnmanagedType.LPWStr)]public string Verb,File,Parameters,Directory;
            public int Show;
            public IntPtr Instance,IdList;
            [MarshalAs(UnmanagedType.LPWStr)]public string Class;
            public IntPtr ClassKey;
            public uint Hotkey;
            public IntPtr Icon,Process;
        }
        [DllImport("shell32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO info);
        public static void Properties(string path) {
            var info=new SHELLEXECUTEINFO {
                Size=Marshal.SizeOf(typeof(SHELLEXECUTEINFO)),Mask=0x0000000C,Verb="properties",File=path,Show=1
            }
            ;
            if(!ShellExecuteEx(ref info))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        public static void Location(Entry entry) {
            LaunchInfoService.Enrich(entry);
            string path=!string.IsNullOrWhiteSpace(entry.LaunchTarget)&&File.Exists(entry.LaunchTarget)?entry.LaunchTarget:entry.Path;
            if(Directory.Exists(path))Process.Start(new ProcessStartInfo("explorer.exe","\""+path+"\"") {
                UseShellExecute=true
            }
            );
            else if(File.Exists(path))Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+path+"\"") {
                UseShellExecute=true
            }
            );
            else throw new FileNotFoundException("文件已移动或删除",path);
        }
    }
    public static class FullscreenState {
        [StructLayout(LayoutKind.Sequential)]struct RECT {
            public int Left,Top,Right,Bottom;
        }
        [StructLayout(LayoutKind.Sequential)]struct MONITORINFO {
            public int Size;
            public RECT Monitor,Work;
            public uint Flags;
        }
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out RECT rect);
        [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFO info);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder text,int length);
        [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint id);
        [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")]static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowLong(IntPtr window,int index);
        public static bool Active() {
            return Active(IntPtr.Zero);
        }
        public static bool Active(IntPtr onMonitor) {
            IntPtr window;
            RECT rect;
            MONITORINFO monitor;
            if(!Measure(onMonitor,out window,out rect,out monitor))return false;
            return WindowReliabilityRules.Fullscreen(IsWindowVisible(window),IsIconic(window),IsZoomed(window),(GetWindowLong(window,-16)&0x00C00000)!=0,Covers(rect,monitor));
        }
        // 前景窗口的底边压进了 Dock 那条带（bandTop 是物理像素下这条带的顶边）：
        // 最大化的应用、贴边的长窗口都算。任务栏隐藏与否只影响工作区，不影响这条带，
        // 所以这里不拿工作区当基准，否则任务栏一收起判据就失效。
        public static bool Immersive(IntPtr onMonitor,double bandTop) {
            IntPtr window;
            RECT rect;
            MONITORINFO monitor;
            if(!Measure(onMonitor,out window,out rect,out monitor))return false;
            bool reachesBand=rect.Bottom>=bandTop;
            bool wide=(rect.Right-rect.Left)*2>=(monitor.Monitor.Right-monitor.Monitor.Left);
            return WindowReliabilityRules.Immersive(IsWindowVisible(window),IsIconic(window),reachesBand,wide,IsZoomed(window)||Covers(rect,monitor));
        }
        static bool Covers(RECT rect,MONITORINFO monitor) {
            return rect.Left<=monitor.Monitor.Left&&rect.Top<=monitor.Monitor.Top&&rect.Right>=monitor.Monitor.Right&&rect.Bottom>=monitor.Monitor.Bottom;
        }
        // 只接受"别人的、不是桌面/任务栏的、同一显示器上的"前景窗口。
        static bool Measure(IntPtr onMonitor,out IntPtr window,out RECT rect,out MONITORINFO monitor) {
            rect=new RECT();
            monitor=new MONITORINFO{Size=Marshal.SizeOf(typeof(MONITORINFO))};
            window=GetForegroundWindow();
            if(window==IntPtr.Zero||(onMonitor!=IntPtr.Zero&&MonitorFromWindow(window,2)!=MonitorFromWindow(onMonitor,2)))return false;
            uint pid;
            GetWindowThreadProcessId(window,out pid);
            if(pid==(uint)Process.GetCurrentProcess().Id)return false;
            var cls=new StringBuilder(256);
            GetClassName(window,cls,256);
            if(new[] {
                "Progman","WorkerW","Shell_TrayWnd","Shell_SecondaryTrayWnd"
            }
            .Contains(cls.ToString()))return false;
            // 桌面挂件和悬浮层（腾讯桌面整理、壁纸引擎等）是工具窗口、没有标题栏，
            // 也不该被当成"铺满屏幕的应用窗口"，否则 Dock 会在桌面上自己收起来。
            if((GetWindowLong(window,-20)&0x00000080)!=0)return false;
            if((GetWindowLong(window,-16)&0x00C00000)==0&&!IsZoomed(window))return false;
            return GetWindowRect(window,out rect)&&GetMonitorInfo(MonitorFromWindow(window,2),ref monitor);
        }
    }
    public partial class Controller {
        bool removalMigrated,glassSuppressed;
        public bool GlassSuppressed {
            get {
                return glassSuppressed;
            }
        }
        void MigrateRemovalIdentities() {
            if(removalMigrated)return;
            removalMigrated=true;
            foreach(var entry in Prefs.Apps.Where(a=>string.IsNullOrWhiteSpace(a.LaunchTarget)))try {
                LaunchInfoService.Enrich(entry);
            }
            catch {
            }
            foreach(string path in Prefs.Removed.ToList())if(!Prefs.Apps.Any(a=>string.Equals(a.Path,path,StringComparison.OrdinalIgnoreCase))&&(File.Exists(path)||Directory.Exists(path)))try {
                var removed=LaunchInfoService.Read(path);
                if(!Prefs.Apps.Any(a=>RemovalRules.Same(a,removed)))RemovalRules.Remember(Prefs,removed);
            }
            catch {
            }
            RemovalRules.ReconcileActive(Prefs);
            Save();
        }
        public void ChooseDockFiles() {
            bool busy=Board.ModalBusy;
            Board.ModalBusy=true;
            try {
                var pick=new Microsoft.Win32.OpenFileDialog {
                    Title="添加软件或文件到 Dock",Multiselect=true,Filter="所有文件|*.*"
                }
                ;
                if(pick.ShowDialog(OwnerWindow)!=true)return;
                Add(pick.FileNames,true,"全部");
                var before=BeginBoardEdit();
                foreach(string path in pick.FileNames) {
                    var entry=Prefs.Apps.FirstOrDefault(a=>string.Equals(a.Path,EntryPathRules.Normalize(path),StringComparison.OrdinalIgnoreCase));
                    if(entry!=null&&!entry.Pinned&&Prefs.Apps.Count(a=>a.Pinned)<20)entry.Pinned=true;
                }
                EndBoardEdit(before,"添加到 Dock");
                Refresh();
            }
            finally {
                Board.ModalBusy=busy;
            }
        }
        public void SetGameMode(bool enabled) {
            Prefs.GameMode=enabled;
            Save();
            ApplyGameMode();
            RefreshTrayMenu();
        }
        public void CheckGameMode() {
            if(!Prefs.AutoGameMode&&!Prefs.GameMode&&!glassSuppressed)return;
            ApplyGameMode();
        }
        public void ApplyGameMode() {
            bool game=GameDetection.ForegroundIsGame();
            bool automatic=GameModeRules.AutoActive(Prefs.AutoGameMode,FullscreenState.Active(),game);
            bool suppress=GameModeRules.Suppressed(Prefs.GameMode,Prefs.AutoGameMode,automatic,false);
            Dock.ScanPaused(suppress);
            if(suppress==glassSuppressed)return;
            Store.Trace("GAME suppress="+suppress+" auto="+Prefs.AutoGameMode+" game="+game+" fullscreen="+FullscreenState.Active());
            glassSuppressed=suppress;
            ControlCenterVisibility(!suppress&&!CleanDesktopActive);
            if(suppress) {
                RunningWindows.CancelKeepVisible();
                Board.Hide();
                Dock.Hide();
                if(widgets!=null)widgets.SetVisible(false);
            }
            else {
                Dock.Refresh(false);
                Dock.Show();
                if(widgets!=null)widgets.SetVisible(true);
            }
            if(Tray!=null)RefreshTrayMenu();
        }
        void BuildGameSettings(StackPanel panel) {
            panel.Children.Add(new TextBlock {
                Text="游戏模式",FontSize=18,Margin=new Thickness(0,22,0,10)
            }
            );
            var manual=new CheckBox {
                Content="游戏模式：隐藏 Dock、大面板和小组件",IsChecked=Prefs.GameMode,Margin=new Thickness(0,6,0,8)
            }
            ;
            panel.Children.Add(manual);
            manual.Checked+=(s,e)=>SetGameMode(true);
            manual.Unchecked+=(s,e)=>SetGameMode(false);
            var automatic=new CheckBox {
                Content="检测全屏或游戏时自动进入（默认关闭）",IsChecked=Prefs.AutoGameMode,Margin=new Thickness(0,6,0,8)
            }
            ;
            panel.Children.Add(automatic);
            automatic.Checked+=(s,e)=> {
                Prefs.AutoGameMode=true;
                Save();
                ApplyGameMode();
            }
            ;
            automatic.Unchecked+=(s,e)=> {
                Prefs.AutoGameMode=false;
                Save();
                ApplyGameMode();
            }
            ;
            panel.Children.Add(new TextBlock {
                Text="游戏模式期间玻璃窗口完全隐藏，避免触碰底边误唤醒。右键系统托盘可退出；自动模式离开全屏后恢复 Dock 和组件。组件配置不关闭或清空。",TextWrapping=TextWrapping.Wrap
            }
            );
        }
    }
}
