using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
namespace GlassBoard {
    // Keep screen-coordinate WPF windows top-level. Reparenting layered windows
    // into Explorer changes DPI/coordinates and breaks drag and drop.
    public static class DesktopPersistence {
        sealed class Entry {public Window Window;public IntPtr Handle;public HwndSource Source;public HwndSourceHook Hook;}
        static readonly List<Entry> entries=new List<Entry>();
        delegate void WinEvent(IntPtr hook,uint type,IntPtr window,int obj,int child,uint thread,uint time);
        static readonly WinEvent callback=Changed;
        static IntPtr foregroundHook,reorderHook;
        static readonly DispatcherTimer settle=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
        static bool applying;
        public static bool DesktopOnly;
        public static bool ForceDesktop(Window window){return window is IslandWindow || DesktopOnly&&(window is BoardWindow||window is DockWindow||window is WidgetWindow||window is ControlOrbWindow||window is ControlCenterWindow);}
        public static void SetDesktopOnly(bool value){DesktopOnly=value;foreach(var e in entries.ToList()){var glass=e.Window as GlassWindow;if(glass!=null)glass.RefreshLayer();Own(e);}RefreshOrder();}
        [StructLayout(LayoutKind.Sequential)]struct WINDOWPOS {public IntPtr Hwnd,After;public int X,Y,Cx,Cy;public uint Flags;}

        static DesktopPersistence(){settle.Tick+=(s,e)=>{settle.Stop();RefreshOrder();};}
        [DllImport("user32.dll")]static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr w,StringBuilder b,int n);
        [DllImport("user32.dll")]static extern bool IsIconic(IntPtr w);
        [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr w);
        [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr w,int command);
        [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr w,IntPtr after,int x,int y,int cx,int cy,uint flags);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr SetLong64(IntPtr w,int index,IntPtr value);
        [DllImport("user32.dll",EntryPoint="SetWindowLongW")]static extern int SetLong32(IntPtr w,int index,int value);
        [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,WinEvent proc,uint process,uint thread,uint flags);
        [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
        static bool Desktop(IntPtr window){if(window==IntPtr.Zero)return false;var b=new StringBuilder(80);GetClassName(window,b,80);return b.ToString()=="Progman"||b.ToString()=="WorkerW"||window==GetShellWindow();}
        static void Own(Entry e){var glass=e.Window as GlassWindow;IntPtr shell=glass!=null&&glass.WantsTopmost&&!ForceDesktop(e.Window)?IntPtr.Zero:GetShellWindow();if(IntPtr.Size==8)SetLong64(e.Handle,-8,shell);else SetLong32(e.Handle,-8,shell.ToInt32());}
        public static void Attach(Window window){
            if(entries.Any(item=>item.Window==window))return;
            var e=new Entry{Window=window,Handle=new WindowInteropHelper(window).Handle};
            if(e.Handle==IntPtr.Zero)return;e.Source=HwndSource.FromHwnd(e.Handle);
            e.Hook=delegate(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled){
                // Let Windows minimize during Show Desktop; restore only when the desktop is actually foreground.
                if(message==0x46&&l!=IntPtr.Zero&&ForceDesktop(window)&&!Desktop(GetForegroundWindow())){var position=(WINDOWPOS)Marshal.PtrToStructure(l,typeof(WINDOWPOS));position.After=new IntPtr(1);position.Flags&=~4u;Marshal.StructureToPtr(position,l,false);}
                if(message==0x5&&w.ToInt64()==1&&window.IsVisible)Schedule();
                return IntPtr.Zero;
            };
            e.Source.AddHook(e.Hook);entries.Add(e);var attachedGlass=window as GlassWindow;if(attachedGlass!=null)attachedGlass.RefreshLayer();Own(e);
            window.IsVisibleChanged+=(s,a)=>{if(window.IsVisible)Schedule();};
            window.Closed+=(s,a)=>{entries.Remove(e);if(e.Source!=null&&!e.Source.IsDisposed)e.Source.RemoveHook(e.Hook);if(entries.Count==0){settle.Stop();if(foregroundHook!=IntPtr.Zero)UnhookWinEvent(foregroundHook);if(reorderHook!=IntPtr.Zero)UnhookWinEvent(reorderHook);foregroundHook=reorderHook=IntPtr.Zero;}};
            if(foregroundHook==IntPtr.Zero)foregroundHook=SetWinEventHook(3,3,IntPtr.Zero,callback,0,0,0);
            if(reorderHook==IntPtr.Zero)reorderHook=SetWinEventHook(0x8004,0x8004,IntPtr.Zero,callback,0,0,2);
            Schedule();
        }
        static void Changed(IntPtr h,uint type,IntPtr window,int obj,int child,uint thread,uint time){if(type==3||Desktop(window))Schedule();}
        static void Schedule(){if(applying||Application.Current==null||Application.Current.Dispatcher.HasShutdownStarted)return;Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(!settle.IsEnabled)settle.Start();}));}
        static void RefreshOrder(){if(applying)return;applying=true;try{
            var foreground=GetForegroundWindow();bool desktop=Desktop(foreground);

            foreach(var e in entries.ToList()){
                if(e.Window.Visibility!=Visibility.Visible)continue;var glass=e.Window as GlassWindow;if(glass!=null)glass.RefreshLayer();Own(e);
                if((IsIconic(e.Handle)||!IsWindowVisible(e.Handle))&&desktop)ShowWindow(e.Handle,4); // SW_SHOWNOACTIVATE
                if(e.Window.Topmost||(e.Handle==foreground&&!ForceDesktop(e.Window)))continue;
                SetWindowPos(e.Handle,desktop?IntPtr.Zero:new IntPtr(1),0,0,0,0,0x13);
            }
        }catch(Exception ex){Store.Log(ex);}finally{applying=false;}}
    }
}
