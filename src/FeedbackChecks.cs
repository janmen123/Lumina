using System;using System.Linq;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
namespace Lumina {
    public static class FeedbackChecks {
        sealed class Probe:GlassWindow {
            public Probe():base(22){}
            public void CheckTransparency(){Width=260;Height=200;CurrentMaterial=new MaterialOptions{Gloss=1,Grain=1,Shadow=true};foreach(int kind in new[]{0,1,2,3}){CurrentMaterial.Kind=kind;SetGlassOpacity(0);if(Surface.Effect!=null||Surface.BorderBrush!=Brushes.Transparent||Surface.Background!=Brushes.Transparent||Root.Children.OfType<Border>().Any(b=>b.Tag as string=="material"&&b.Opacity!=0))throw new Exception("玻璃 100% 透明仍有底色、阴影或纹理");SetGlassOpacity(72);if(Surface.Background==Brushes.Transparent)throw new Exception("材质恢复失败");}}
        }
        public static void Run(){var probe=new Probe();try{probe.CheckTransparency();}finally{probe.Close();}
            var controller=new Controller(false);WidgetManager manager=null;try{controller.Board.CheckResizeRefresh();controller.Board.CheckBoardFeedback16();controller.Prefs.Widgets.HideControls=true;controller.Prefs.Widgets.Clock=true;manager=new WidgetManager(controller);manager.Sync();var window=manager.SnapshotWindows.FirstOrDefault();if(window==null||window.ContextMenu==null||window.ContextMenu.Items.Count==0)throw new Exception("隐藏按钮后缺少右键恢复入口");}finally{if(manager!=null)manager.Dispose();controller.ShuttingDown=true;controller.Dock.ReleaseHotkey();controller.Board.Close();controller.Dock.Close();}
        }
    }
}
