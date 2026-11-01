using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;


namespace Lumina {
    public static class DesktopVisibility {
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int cmd);
        [DllImport("user32.dll",SetLastError=true)] static extern bool ShowWindowAsync(IntPtr h,int cmd);
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr h,int index);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder text,int count);
        delegate bool EnumCallback(IntPtr h,IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback,IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent,EnumCallback callback,IntPtr data);
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr h,uint message,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
        static readonly string IconRecoveryFile=System.IO.Path.Combine(Store.Folder,"desktop-icons-recovery.txt");
        static bool? originalIcons;
        static bool NativeIconsHidden(){using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced")){return key!=null&&Convert.ToInt32(key.GetValue("HideIcons",0))!=0;}}
        static void NativeSetIcons(bool hide){
            if(NativeIconsHidden()==hide)return;
            var view=DesktopLists().FirstOrDefault(h=>Class(h)=="SHELLDLL_DefView");
            if(view==IntPtr.Zero)throw new InvalidOperationException("未找到 Windows 桌面视图");
            IntPtr result;if(SendMessageTimeout(view,0x111,new IntPtr(0x7402),IntPtr.Zero,2,3000,out result)==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"切换桌面图标失败");
            if(NativeIconsHidden()!=hide)throw new InvalidOperationException("Windows 未确认桌面图标状态已更改");
        }
        static readonly string RecoveryFile=System.IO.Path.Combine(Store.Folder,"shell-recovery.json");
        static Dictionary<long,ShellState> original=new Dictionary<long,ShellState>();
        static bool? taskbarHidden,iconsHidden;
        static readonly DispatcherTimer maintenance=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(750)};
        static DesktopVisibility() {maintenance.Tick+=(s,e)=>Maintain();}
        static string Class(IntPtr h) {var b=new StringBuilder(256);GetClassName(h,b,256);return b.ToString();}
        static List<IntPtr> Taskbars() {
            var result=new List<IntPtr>();var main=FindWindow("Shell_TrayWnd",null);if(main!=IntPtr.Zero)result.Add(main);
            EnumWindows((h,d)=>{string cls=Class(h);if((cls=="Shell_TrayWnd"||cls=="Shell_SecondaryTrayWnd")&&!result.Contains(h))result.Add(h);return true;},IntPtr.Zero);return result;
        }
        static List<IntPtr> DesktopLists() {
            var result=new HashSet<IntPtr>();Action<IntPtr> addView=view=>{
                result.Add(view);EnumChildWindows(view,(child,data)=>{if(Class(child)=="SysListView32")result.Add(child);return true;},IntPtr.Zero);
            };
            EnumWindows((h,d)=> {
                string cls=Class(h);var view=FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null);if(view!=IntPtr.Zero)addView(view);
                if(cls=="Progman"||cls=="WorkerW")EnumChildWindows(h,(child,data)=>{if(Class(child)=="SHELLDLL_DefView")addView(child);return true;},IntPtr.Zero);
                return true;
            },IntPtr.Zero);return result.OrderBy(h=>Class(h)=="SHELLDLL_DefView"?0:1).ToList();
        }
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
        static bool IsDeskGoSurface(IntPtr h){
            if(Class(h)!="TXMiniSkin")return false;var title=new StringBuilder(128);GetWindowText(h,title,128);if(title.ToString()!="桌面整理")return false;
            uint pid;GetWindowThreadProcessId(h,out pid);try{using(var p=Process.GetProcessById((int)pid)){return p.ProcessName.Equals("DesktopMgr64",StringComparison.OrdinalIgnoreCase);}}catch{return false;}
        }
        static List<IntPtr> DeskGoSurfaces(){var result=new HashSet<IntPtr>();EnumWindows((h,d)=>{if(IsDeskGoSurface(h))result.Add(h);EnumChildWindows(h,(child,data)=>{if(IsDeskGoSurface(child))result.Add(child);return true;},IntPtr.Zero);return true;},IntPtr.Zero);return result.ToList();}
        static List<IntPtr> hiddenDeskGo=new List<IntPtr>();
        public static void SetDeskGoHidden(bool hide){
            if(hide){hiddenDeskGo=DeskGoSurfaces().Where(IsWindowVisible).ToList();Remember(hiddenDeskGo);foreach(var h in hiddenDeskGo)ShowWindow(h,0);}
            else{foreach(var h in hiddenDeskGo)if(IsWindow(h)&&IsDeskGoSurface(h))ShowWindow(h,8);hiddenDeskGo.Clear();}
        }
        static bool MatchesSaved(ShellState state) {
            var h=new IntPtr(state.Handle);if(!IsWindow(h)||Class(h)!=state.ClassName)return false;
            if(state.ClassName=="TXMiniSkin")return IsDeskGoSurface(h);
            if(state.ClassName=="Shell_TrayWnd"||state.ClassName=="Shell_SecondaryTrayWnd")return Taskbars().Contains(h);
            return (state.ClassName=="SysListView32"||state.ClassName=="SHELLDLL_DefView")&&DesktopLists().Contains(h);
        }
        static void Remember(IEnumerable<IntPtr> handles) {
            bool changed=false;
            foreach(var h in handles)if(!original.ContainsKey(h.ToInt64())) {original[h.ToInt64()]=new ShellState{Handle=h.ToInt64(),ClassName=Class(h),Visible=(GetWindowLong(h,-16)&0x10000000)!=0};changed=true;}
            if(!changed)return;
            Directory.CreateDirectory(Store.Folder);
            string temp=RecoveryFile+".tmp";
            using(var s=File.Create(temp))new DataContractJsonSerializer(typeof(List<ShellState>)).WriteObject(s,original.Values.ToList());
            if(File.Exists(RecoveryFile))File.Replace(temp,RecoveryFile,null);else File.Move(temp,RecoveryFile);
        }
        public static bool? IsHidden(bool taskbar) {
            if(!taskbar){try{return NativeIconsHidden();}catch(Exception ex){Store.Log(ex);return null;}}
            var targets=Taskbars();if(targets.Count==0)return null;
            var lists=targets.Where(h=>Class(h)=="SysListView32").ToList();if(!taskbar&&lists.Count>0)targets=lists;
            return !targets.Any(IsWindowVisible);
        }
        public static bool DesiredHidden(bool taskbar) {return taskbar?(taskbarHidden??IsHidden(true)??false):(IsHidden(false)??false);}
        public static string SetHidden(bool taskbar,bool hide) {
            if(!taskbar){
                bool before=NativeIconsHidden();
                if(before!=hide&&originalIcons==null){Directory.CreateDirectory(Store.Folder);File.WriteAllText(IconRecoveryFile,before?"1":"0");originalIcons=before;}
                NativeSetIcons(hide);iconsHidden=hide;
                return "已"+(hide?"隐藏":"显示")+"桌面图标";
            }
            var handles=Taskbars();string noun="任务栏";
            if(handles.Count==0)return "未找到"+noun+"，请确认 Windows Explorer 已运行。";
            // Journal raw WS_VISIBLE before hiding; ancestors can make a visible child appear hidden.
            int failed=0;Remember(handles);foreach(var h in handles)if(!ShowWindowAsync(h,hide?0:8)){failed++;Store.Log(new Win32Exception(Marshal.GetLastWin32Error(),"无法切换 "+Class(h)));}
            if(taskbar)taskbarHidden=hide;else iconsHidden=hide;
            if(taskbarHidden==true||iconsHidden==true)maintenance.Start();else maintenance.Stop();
            return failed>0?noun+"部分窗口切换失败，详情已记录在 error.log。":"已请求"+(hide?"隐藏":"显示")+noun;
        }
        public static string ToggleTaskbar() {return SetHidden(true,!DesiredHidden(true));}
        public static string ToggleIcons() {return SetHidden(false,!DesiredHidden(false));}
        static void Maintain() {
            try {
                foreach(bool taskbar in new bool[]{true})if((taskbar?taskbarHidden:iconsHidden)==true) {
                    var handles=taskbar?Taskbars():DesktopLists();if(handles.Count==0)continue;Remember(handles);
                    foreach(var h in handles)if(IsWindowVisible(h))ShowWindowAsync(h,0);
                }
            }catch(Exception ex){Store.Log(ex);maintenance.Stop();}
        }
        static void RestoreStates(IEnumerable<ShellState> states) {
            var steps = states.Where(MatchesSaved).Select(state => (Action)(() => {
                var handle = new IntPtr(state.Handle);
                ShowWindow(handle, state.Visible ? 8 : 0);
                bool visible = (GetWindowLong(handle, -16) & 0x10000000) != 0;
                if (visible != state.Visible) throw new IOException("Windows 尚未确认界面恢复：" + state.ClassName);
            }));
            RecoveryActions.RunAll(steps);
        }

        public static void Recover() {
            RecoveryActions.RunAll(new Action[] {
                () => {
                    if (!File.Exists(IconRecoveryFile)) return;
                    string saved = File.ReadAllText(IconRecoveryFile).Trim();
                    if (saved != "0" && saved != "1") throw new InvalidDataException("桌面图标恢复记录无效");
                    NativeSetIcons(saved == "1");
                    File.Delete(IconRecoveryFile);
                },
                () => {
                    if (!File.Exists(RecoveryFile)) return;
                    using (var stream = File.OpenRead(RecoveryFile)) {
                        var states = (List<ShellState>)new DataContractJsonSerializer(typeof(List<ShellState>)).ReadObject(stream);
                        if (states == null) throw new InvalidDataException("界面恢复记录无效");
                        RestoreStates(states);
                    }
                    File.Delete(RecoveryFile);
                }
            });
        }

        public static void RestoreOriginal() {
            maintenance.Stop();
            taskbarHidden = null;
            iconsHidden = null;
            RecoveryActions.RunAll(new Action[] {
                () => {
                    if (!originalIcons.HasValue) return;
                    NativeSetIcons(originalIcons.Value);
                    if (File.Exists(IconRecoveryFile)) File.Delete(IconRecoveryFile);
                    originalIcons = null;
                },
                () => {
                    RestoreStates(original.Values);
                    if (File.Exists(RecoveryFile)) File.Delete(RecoveryFile);
                    original.Clear();
                    hiddenDeskGo.Clear();
                }
            });
        }
        public static void ShowAll() {
            maintenance.Stop();taskbarHidden=false;iconsHidden=false;
            RecoveryActions.RunAll(new Action[] {
                () => {
                    NativeSetIcons(false);
                    if(File.Exists(IconRecoveryFile))File.Delete(IconRecoveryFile);
                    originalIcons=null;
                },
                () => {
                    var states=Taskbars().Concat(DesktopLists()).Select(h=>new ShellState {
                        Handle=h.ToInt64(),ClassName=Class(h),Visible=true
                    }).ToList();
                    RestoreStates(states);
                }
            });
            original.Clear();if(File.Exists(RecoveryFile))File.Delete(RecoveryFile);
        }
    }
}
