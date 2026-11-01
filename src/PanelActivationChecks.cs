using System;
using System.Linq;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Lumina {
    public partial class DockWindow {
        internal void CheckPanelActivation() {
            foreach(var key in C.Prefs.Hotkeys)key.Enabled=false;
            var handle=new WindowInteropHelper(this).EnsureHandle();
            C.Prefs.GameMode=true;
            C.ApplyGameMode();
            bool handled=false;
            Hook(handle,Native.ShowPanelMessage,IntPtr.Zero,IntPtr.Zero,ref handled);
            Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
            if(!handled||!C.Board.IsVisible)throw new Exception("显式唤回消息没有打开面板");
            if(!C.Prefs.GameMode||C.Prefs.Hotkeys.Any(k=>k.Enabled))
                throw new Exception("面板唤回改写了游戏模式或快捷键偏好");
            C.Board.Hide();
        }
    }

    internal static class PanelActivationChecks {
        internal static void Run() {
            var controller=new Controller(false);
            try { controller.Dock.CheckPanelActivation(); }
            finally {
                controller.ShuttingDown=true;
                controller.Dock.ReleaseHotkey();
                controller.Board.Close();
                controller.Dock.Close();
            }
        }
    }
}
