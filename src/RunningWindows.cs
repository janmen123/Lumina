using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;using System.Windows.Media;using System.Windows.Media.Imaging;using System.Windows.Interop;

namespace GlassBoard {
    public sealed class RunningWindow {public IntPtr Handle;public uint ProcessId;public string Path,Title,FolderPath;}
    public static class RunningWindows {
        delegate bool Callback(IntPtr window,IntPtr value);
        [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback,IntPtr value);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint id);
        [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window,int command);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr GetLastActivePopup(IntPtr window);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int length);
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint id);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder path,ref int length);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
        [StructLayout(LayoutKind.Sequential)] struct FLASHWINFO{public uint Size;public IntPtr Window;public uint Flags,Count,Timeout;}
        [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO info);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder text,int length);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
        [DllImport("user32.dll",EntryPoint="GetClassLongPtrW")] static extern IntPtr ClassIcon64(IntPtr window,int index);
        [DllImport("user32.dll",EntryPoint="GetClassLongW")] static extern uint ClassIcon32(IntPtr window,int index);
        public static ImageSource Icon(RunningWindow window){
            try{IntPtr handle;SendMessageTimeout(window.Handle,0x7F,new IntPtr(1),IntPtr.Zero,2,80,out handle);
                if(handle==IntPtr.Zero)SendMessageTimeout(window.Handle,0x7F,new IntPtr(2),IntPtr.Zero,2,80,out handle);
                if(handle==IntPtr.Zero)handle=IntPtr.Size==8?ClassIcon64(window.Handle,-14):new IntPtr(unchecked((int)ClassIcon32(window.Handle,-14)));
                if(handle!=IntPtr.Zero){var image=Imaging.CreateBitmapSourceFromHIcon(handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image;}
                if(!string.IsNullOrEmpty(window.Path)){var image=Native.Icon(window.Path);if(image!=null)return image;}
            }catch{}
            var fallback=Imaging.CreateBitmapSourceFromHIcon(System.Drawing.SystemIcons.Application.Handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());fallback.Freeze();return fallback;
        }
        sealed class TargetInfo {public DateTime Stamp;public string Target,Arguments;}
        static DateTime foldersAt=DateTime.MinValue;static Dictionary<long,string> folders=new Dictionary<long,string>();
        static readonly List<RunningWindow> windows=new List<RunningWindow>();
        static readonly BoundedCache<string,TargetInfo> targets=new BoundedCache<string,TargetInfo>(192,StringComparer.OrdinalIgnoreCase);
        static readonly BoundedCache<string,long> last=new BoundedCache<string,long>(192,StringComparer.OrdinalIgnoreCase);
        public static string ProcessPath(uint id){
            var process=OpenProcess(0x1000,false,id);if(process==IntPtr.Zero)return null;
            try{int length=512;var path=new StringBuilder(length);if(!QueryFullProcessImageName(process,0,path,ref length)){if(Marshal.GetLastWin32Error()!=122)return null;length=32768;path=new StringBuilder(length);if(!QueryFullProcessImageName(process,0,path,ref length))return null;}return WindowActivationRules.NormalizePath(path.ToString());}finally{CloseHandle(process);}
        }
        public static void Scan(){
            windows.Clear();if((DateTime.UtcNow-foldersAt).TotalSeconds>=25){foldersAt=DateTime.UtcNow;folders=ExplorerWindows.Read();}var byPid=new Dictionary<uint,string>();int own=Process.GetCurrentProcess().Id;
            EnumWindows((handle,value)=>{
                if(!IsWindowVisible(handle))return true;int style=GetWindowLong(handle,-20);if((style&0x80)!=0||((style&0x40000)==0&&GetWindow(handle,4)!=IntPtr.Zero&&IsWindowVisible(GetWindow(handle,4))))return true;
                int cloaked;try{if(DwmGetWindowAttribute(handle,14,out cloaked,4)==0&&cloaked!=0)return true;}catch{}
                uint id;GetWindowThreadProcessId(handle,out id);if(id==own)return true;string path;
                if(!byPid.TryGetValue(id,out path)){try{path=ProcessPath(id);}catch{path=null;}byPid[id]=path;}
                var text=new StringBuilder(1024);GetWindowText(handle,text,text.Capacity);if(string.IsNullOrWhiteSpace(text.ToString()))return true;
                var cls=new StringBuilder(128);GetClassName(handle,cls,cls.Capacity);if(new[]{"Progman","WorkerW","Shell_TrayWnd","Shell_SecondaryTrayWnd"}.Contains(cls.ToString()))return true;
                string folder;folders.TryGetValue(handle.ToInt64(),out folder);windows.Add(new RunningWindow{Handle=handle,ProcessId=id,Path=path,Title=text.ToString(),FolderPath=folder});return true;
            },IntPtr.Zero);
        }
        static void Resolve(Entry entry){
            if(entry==null||entry.IsFolder)return;string path=entry.Path;if(string.IsNullOrEmpty(path))return;
            if(!Path.GetExtension(path).Equals(".lnk",StringComparison.OrdinalIgnoreCase)){if(string.IsNullOrWhiteSpace(entry.LaunchTarget))LaunchInfoService.Enrich(entry);return;}
            DateTime stamp;try{stamp=File.GetLastWriteTimeUtc(path);}catch{stamp=DateTime.MinValue;}TargetInfo cached;
            if(targets.TryGet(path,out cached)&&cached.Stamp==stamp){entry.LaunchTarget=cached.Target;entry.LaunchArguments=cached.Arguments;return;}
            LaunchInfoService.Enrich(entry);targets.Put(path,new TargetInfo{Stamp=stamp,Target=entry.LaunchTarget,Arguments=entry.LaunchArguments});
        }
        public static List<RunningWindow> For(Entry entry){if(entry==null)return new List<RunningWindow>();if(entry.IsFolder||Directory.Exists(entry.Path)){string folder=EntryPathRules.Normalize(entry.Path);return windows.Where(w=>!string.IsNullOrEmpty(w.FolderPath)&&string.Equals(w.FolderPath,folder,StringComparison.OrdinalIgnoreCase)).ToList();}Resolve(entry);var paths=WindowActivationRules.Executables(entry);return windows.Where(w=>paths.Contains(w.Path,StringComparer.OrdinalIgnoreCase)).ToList();}
        public static List<RunningWindow> Snapshot(){return windows.ToList();}
        public static bool IsRunning(Entry entry){return For(entry).Count>0;}
        public static int Count(Entry entry){return entry==null?0:For(entry).Count;}
        [DllImport("user32.dll",SetLastError=true)]static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
        public static bool RequestClose(RunningWindow window){if(window==null||!IsWindow(window.Handle))return false;uint pid;GetWindowThreadProcessId(window.Handle,out pid);if(pid!=window.ProcessId)return false;return PostMessage(window.Handle,0x10,IntPtr.Zero,IntPtr.Zero);}
        [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
        [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")]static extern bool AttachThreadInput(uint attach,uint target,bool value);
        [DllImport("user32.dll")]static extern bool BringWindowToTop(IntPtr window);
        public static bool Minimized(IntPtr handle){return handle!=IntPtr.Zero&&IsWindow(handle)&&IsIconic(handle);}
        public static IntPtr Foreground(){return GetForegroundWindow();}
        // Windows reports no foreground window while the user clicks the Dock:
        // the click must not activate the Dock, so the previous app is cleared.
        // Remember the last real foreground app so a Dock click still knows which
        // window the user was looking at.
        static IntPtr remembered;
        public static void RememberForeground(){
            var handle=GetForegroundWindow();
            if(handle==IntPtr.Zero||IsOwnWindow(handle)||IsShellSurface(handle))return;
            remembered=handle;
        }
        static bool IsOwnWindow(IntPtr handle){uint id;GetWindowThreadProcessId(handle,out id);return id==(uint)Process.GetCurrentProcess().Id;}
        // The taskbar, Start menu and the ForegroundStaging helper are not the app
        // the user is working in; they must not replace the remembered app.
        static bool IsShellSurface(IntPtr handle){
            if(handle==IntPtr.Zero)return true;
            var text=new StringBuilder(128);GetClassName(handle,text,text.Capacity);
            var name=text.ToString();
            return name=="Shell_TrayWnd"||name=="Shell_SecondaryTrayWnd"||name=="Progman"||name=="WorkerW"
                ||name=="Windows.UI.Core.CoreWindow"||name=="MultitaskingViewFrame"||name=="ForegroundStaging"
                ||name.Length==0;
        }
        static IntPtr EffectiveForeground(){
            var handle=GetForegroundWindow();
            if(handle!=IntPtr.Zero&&!IsOwnWindow(handle)&&!IsShellSurface(handle))return handle;
            if(remembered!=IntPtr.Zero&&IsWindow(remembered)&&!IsShellSurface(remembered))return remembered;
            return IntPtr.Zero;
        }
        // The launcher panel itself is the foreground window while a tile is clicked,
        // so remember which window the user was looking at before the panel opened.
        public static IntPtr CaptureForegroundHint(){
            var foreground=EffectiveForeground();
            if(foreground==IntPtr.Zero||IsOwnWindow(foreground))return IntPtr.Zero;
            return foreground;
        }
        static void ForceForeground(IntPtr handle){
            if(handle==IntPtr.Zero||GetForegroundWindow()==handle)return;
            uint owner;uint targetThread=GetWindowThreadProcessId(handle,out owner);
            var foreground=GetForegroundWindow();uint foregroundOwner;
            uint foregroundThread=foreground==IntPtr.Zero?0:GetWindowThreadProcessId(foreground,out foregroundOwner);
            uint own=0;bool attachForeground=false,attachTarget=false;
            try{own=GetCurrentThreadId();attachForeground=foregroundThread!=0&&foregroundThread!=own&&AttachThreadInput(own,foregroundThread,true);attachTarget=targetThread!=0&&targetThread!=own&&AttachThreadInput(own,targetThread,true);}
            catch(Exception ex){Store.Log(ex);}
            try{Native.SetForegroundWindow(handle);BringWindowToTop(handle);}
            finally{try{if(attachTarget)AttachThreadInput(own,targetThread,false);if(attachForeground)AttachThreadInput(own,foregroundThread,false);}catch(Exception ex){Store.Log(ex);}}
        }
        public static void Focus(IntPtr handle){
            if(handle==IntPtr.Zero||!IsWindow(handle))return;
            if(IsIconic(handle))ShowWindowAsync(handle,9);
            ForceForeground(handle);
        }
        public static bool ActivateHandle(IntPtr handle){
            if(!IsWindow(handle))return false;if(IsIconic(handle))ShowWindowAsync(handle,9);
            var popup=GetLastActivePopup(handle);var target=popup!=IntPtr.Zero&&IsWindowVisible(popup)?popup:handle;
            ForceForeground(target);
            if(GetForegroundWindow()!=target){var flash=new FLASHWINFO{Size=(uint)Marshal.SizeOf(typeof(FLASHWINFO)),Window=target,Flags=3,Count=2};FlashWindowEx(ref flash);Store.Trace("WINDOW_SWITCH foreground request blocked handle="+target);}
            // A recognized existing window must never fall through to launching
            // another instance merely because Windows refused foreground focus.
            return true;
        }
        internal static bool Valid(RunningWindow window){uint pid;return window!=null&&IsWindow(window.Handle)&&GetWindowThreadProcessId(window.Handle,out pid)!=0&&pid==window.ProcessId;}
        internal static bool OwnsForeground(IntPtr handle,IntPtr foreground){
            // Owned dialogs belong to the same top-level window. Do not group all
            // windows from a process: another document may be open in that process.
            for(int i=0;i<32&&foreground!=IntPtr.Zero;i++){
                if(foreground==handle)return true;
                var owner=GetWindow(foreground,4);if(owner==foreground)break;foreground=owner;
            }
            return false;
        }
        internal static RunningWindow ForegroundMatch(IEnumerable<RunningWindow> candidates){
            var foreground=EffectiveForeground();return candidates.FirstOrDefault(w=>Valid(w)&&!IsIconic(w.Handle)&&OwnsForeground(w.Handle,foreground));
        }
        // hint: the window that had foreground just before the launcher panel opened.
        // It is the only reliable way to know which app a click on the panel refers to,
        // because the panel takes the foreground as soon as it is shown.
        internal static bool ToggleWindow(RunningWindow window,IntPtr hint){
            if(!Valid(window))return false;
            bool front=OwnsForeground(window.Handle,EffectiveForeground())||(hint!=IntPtr.Zero&&hint==window.Handle);
            var fg=EffectiveForeground();Store.Trace("WINDOW decide handle="+window.Handle+" hint="+hint+" fg="+fg+" fgpid="+OwnerOf(fg)+" fgtitle="+TitleOf(fg)+" front="+front+" iconic="+IsIconic(window.Handle));
            if(WindowActivationRules.MinimizeDock(front,IsIconic(window.Handle))){
                ShowWindow(window.Handle,6);
                if(!IsIconic(window.Handle)){Store.Trace("WINDOW_MINIMIZE rejected handle="+window.Handle);return true;}
                Store.Trace("WINDOW minimize handle="+window.Handle);
                return true; // A rejected minimize must not launch a second instance.
            }
            if(IsIconic(window.Handle))ShowWindow(window.Handle,9);
            ActivateHandle(window.Handle);Store.Trace("WINDOW activate handle="+window.Handle);return true;
        }
        internal static bool ToggleWindow(RunningWindow window){return ToggleWindow(window,IntPtr.Zero);}
        // One click, one window: the window the user was looking at, otherwise the
        // foreground window, otherwise the one used last, otherwise the topmost one.
        // Several windows of one program must never be minimized together.
        public static bool Toggle(Entry entry,IntPtr hint){
            Scan();var matches=For(entry).Where(Valid).ToList();
            if(matches.Count==0){Store.Trace("WINDOW none entry="+entry.Name+" path="+entry.Path+" target="+entry.LaunchTarget);return false;}
            long previous;last.TryGet(entry.Path,out previous);var foreground=EffectiveForeground();
            RunningWindow selected=null;
            if(hint!=IntPtr.Zero)selected=matches.FirstOrDefault(w=>w.Handle==hint);
            if(selected==null)selected=ForegroundMatch(matches);
            if(selected==null&&previous!=0)selected=matches.FirstOrDefault(w=>w.Handle.ToInt64()==previous);
            if(selected==null)selected=matches.FirstOrDefault(w=>!IsIconic(w.Handle))??matches[0];
            IntPtr selectedHint=hint!=IntPtr.Zero&&hint==selected.Handle?hint:IntPtr.Zero;
            Store.Trace("WINDOW pick entry="+entry.Name+" hint="+hint+" matches="+matches.Count+" selected="+selected.Handle+" fg="+foreground+" fgpid="+OwnerOf(foreground)+" fgtitle="+TitleOf(foreground));
            bool ok=ToggleWindow(selected,selectedHint);
            if(ok)last.Put(entry.Path,selected.Handle.ToInt64());
            return ok;
        }
        public static bool ToggleDock(Entry entry){return Toggle(entry,IntPtr.Zero);}
        public static bool ToggleDock(Entry entry,IntPtr hint){return Toggle(entry,hint);}
        static uint OwnerOf(IntPtr handle){uint id;if(handle==IntPtr.Zero)return 0;GetWindowThreadProcessId(handle,out id);return id;}
        static string TitleOf(IntPtr handle){if(handle==IntPtr.Zero)return "";var text=new StringBuilder(200);GetWindowText(handle,text,text.Capacity);return text.ToString();}
        public static void MinimizeForCheck(IntPtr handle){if(IsWindow(handle))ShowWindow(handle,6);}
        public static void RestoreForCheck(IntPtr handle){if(IsWindow(handle))ShowWindow(handle,9);}
        public static bool Activate(Entry entry){return Toggle(entry,IntPtr.Zero);}
    }
}
