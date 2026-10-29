using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace GlassBoard {
    public static class DockBehaviorChecks {
        public static void Run(){
            var path=RunningWindows.ProcessPath((uint)Process.GetCurrentProcess().Id);if(string.IsNullOrEmpty(path))throw new Exception("窗口进程路径读取失败");
            var probe=new Window{Width=220,Height=160,ShowInTaskbar=false};try{probe.Show();var handle=new WindowInteropHelper(probe).Handle;probe.WindowState=WindowState.Minimized;if(!RunningWindows.ActivateHandle(handle))throw new Exception("恢复已有窗口失败");}finally{probe.Close();}
            var controller=new Controller(false);try{
                for(int i=0;i<4;i++)controller.Prefs.Apps.Add(new Entry{Name="Dock"+i,Path=path+"-"+i,Pinned=true});controller.Prefs.Normalize();controller.Dock.Refresh(false);
                foreach(var key in controller.Prefs.Hotkeys)key.Enabled=false;controller.Dock.Show();controller.Dock.UpdateLayout();
                var first=controller.Prefs.Apps[0];var last=controller.Prefs.Apps[3];if(!DockOrderRules.Move(controller.Prefs,first.Path,last.Path,false))throw new Exception("Dock 排序失败");controller.Dock.Refresh(false);
                var panel=FindPanel(controller.Dock);var buttons=panel==null?new Button[0]:panel.Children.OfType<Button>().Where(b=>b.Tag is Entry).ToArray();var expected=DockOrderRules.Ordered(controller.Prefs).Take(buttons.Length).ToArray();
                Store.Trace("CHECK dock render ok buttons="+buttons.Length);if(buttons.Length<1||!buttons.Select(b=>((Entry)b.Tag).Path).SequenceEqual(expected.Select(a=>a.Path))||buttons.Any(b=>b.Tag==null))throw new Exception("Dock 渲染顺序或排序入口错误");Store.Trace("CHECK pointer reorder start");controller.Dock.CheckPointerReorder();Store.Trace("CHECK pointer reorder ok");controller.Dock.CheckTaskDock();controller.Dock.CheckMixedPointerReorder();
            }finally{Store.Trace("CHECK dock cleanup");controller.ShuttingDown=true;controller.Dock.ReleaseHotkey();controller.Board.Close();controller.Dock.Close();}
        }
        static StackPanel FindPanel(DependencyObject parent){if(parent is StackPanel&&((StackPanel)parent).Orientation==Orientation.Horizontal&&((StackPanel)parent).Children.OfType<Button>().Any(b=>b.Tag is Entry))return (StackPanel)parent;for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++){var result=FindPanel(System.Windows.Media.VisualTreeHelper.GetChild(parent,i));if(result!=null)return result;}return null;}
    }
}
