using System;
using System.Runtime.InteropServices;

namespace GlassBoard {
    // Watches a single key with a low-level hook instead of polling GetAsyncKeyState
    // from a timer: the hook runs only while it is installed and costs nothing when idle.
    public sealed class KeyWatch : IDisposable {
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,Proc callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr wparam,IntPtr lparam);
        delegate IntPtr Proc(int code,IntPtr wparam,IntPtr lparam);
        const int KeyboardLowLevel=13;
        const int KeyDown=0x0100;
        readonly Proc callback;
        readonly int virtualKey;
        readonly Action action;
        IntPtr hook;
        public KeyWatch(int key,Action onPressed) {
            virtualKey=key; action=onPressed; callback=OnMessage;
            try { hook=SetWindowsHookEx(KeyboardLowLevel,callback,IntPtr.Zero,0); }
            catch(Exception ex) { Store.Log(ex); }
        }
        IntPtr OnMessage(int code,IntPtr wparam,IntPtr lparam) {
            try {
                if(code>=0&&wparam==(IntPtr)KeyDown&&Marshal.ReadInt32(lparam)==virtualKey&&action!=null)action();
            } catch(Exception ex) { Store.Log(ex); }
            return CallNextHookEx(hook,code,wparam,lparam);
        }
        public void Dispose() {
            if(hook==IntPtr.Zero)return;
            try { UnhookWindowsHookEx(hook); } catch(Exception ex) { Store.Log(ex); }
            hook=IntPtr.Zero;
        }
        public static void DisposeOf(ref KeyWatch watch) {var current=watch;watch=null;if(current!=null)current.Dispose();}
    }
}
