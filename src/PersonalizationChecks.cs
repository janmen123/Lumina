using System;using System.Linq;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
namespace GlassBoard {
    public sealed partial class WidgetWindow {
        internal void CheckAppearance(){
            foreach(string kind in WidgetRules.Kinds){var style=M.C.Prefs.Widgets.Style(kind);style.Frameless=true;style.TextColor="#33CCFF";
                if(kind!="clock")continue;
                for(int variant=0;variant<(kind=="clock"?ClockCatalog.Names.Length:kind=="music"?5:1);variant++){
                    style.ClockStyle=variant;UpdateOptions(new System.Collections.Generic.List<string>{kind});UpdateLayout();
                    if(Surface.Visibility!=Visibility.Collapsed||moveGrip.Visibility!=Visibility.Collapsed||resizeGrip.Visibility!=Visibility.Collapsed)throw new Exception("无框组件仍有外框或编辑控件");
                    var card=stack.Children.OfType<Border>().Single();if(card.Background!=null||card.BorderThickness.Left!=0||card.Padding.Left>4)throw new Exception("无框组件仍有卡片底板或过多留白");
                    if(ContextMenu==null||ContextMenu.Items.Count==0)throw new Exception("无框组件缺少恢复入口");
                    if(!ContextMenu.Items.OfType<MenuItem>().Any(item=>item.Header as string=="恢复组件边框（退出无框模式）"))throw new Exception("无框组件缺少直接恢复边框入口");
                    Root.Measure(new Size(Width-28,Height-28));Root.Arrange(new Rect(14,14,Width-28,Height-28));Root.UpdateLayout();
                    var inputBrush=widgetInputArea.Background as SolidColorBrush;if(inputBrush==null||inputBrush.Color.A!=1||widgetInputArea.Margin.Left!=-14)throw new Exception("无框组件缺少扩大的输入区域");
                    foreach(var point in new[]{new Point(1,1),new Point(widgetInputArea.ActualWidth-1,1),new Point(1,widgetInputArea.ActualHeight-1),new Point(widgetInputArea.ActualWidth/2,widgetInputArea.ActualHeight/2)})if(widgetInputArea.InputHitTest(point)==null)throw new Exception("无框组件空白区域不能右键");
                    TextBlock text=date;var color=text.Foreground as SolidColorBrush;if(color==null||color.Color.R!=0x33||color.Color.G!=0xCC)throw new Exception("文字配色未应用");
                    if(kind=="clock"&&variant==1&&(weekday==null||weekday.Opacity>=.5))throw new Exception("星期叠字层次错误");
                }
                style.Frameless=false;UpdateOptions(new System.Collections.Generic.List<string>{kind});if(Surface.Visibility!=Visibility.Visible)throw new Exception("组件外框无法恢复");
            }
        }
    }
    public static class PersonalizationChecks {
        public static void Run(){var c=new Controller(false);WidgetManager manager=null;WidgetWindow probe=null;try{c.Prefs.Dock.MotionEnabled=false;for(int i=0;i<6;i++)c.Prefs.Apps.Add(new Entry{Name="center"+i,Path="center"+i,Pinned=true});c.Prefs.Normalize();c.Dock.Refresh(false);double center=c.Dock.Left+c.Dock.Width/2;c.Prefs.Placement.DockLeft=c.Dock.Left;c.Prefs.Placement.DockCenter=center;c.Prefs.Apps[0].Pinned=false;c.Dock.Refresh(false);if(Math.Abs(c.Dock.Left+c.Dock.Width/2-center)>.01)throw new Exception("Dock 内容变动后中心偏移");manager=new WidgetManager(c);probe=new WidgetWindow(manager,0);probe.ShowInTaskbar=false;probe.Show();probe.UpdateLayout();probe.CheckAppearance();}finally{if(probe!=null)probe.Stop();if(manager!=null)manager.Dispose();c.ShuttingDown=true;c.Dock.ReleaseHotkey();c.Board.Close();c.Dock.Close();}}
    }
}
