using System;using System.Linq;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
namespace Lumina {
    public static class OrbSections {public static string Normalize(string value){return value=="tray"?"tray":"control";}}
    public sealed partial class ControlCenterWindow {
        string activeSection="control";Button controlSection,traySection,startSection;
        void BuildSectionNavigation(DockPanel root){
            var row=new System.Windows.Controls.Primitives.UniformGrid{Rows=1,Margin=new Thickness(0,0,0,14)};
            controlSection=ActionButton("控制中心",()=>SelectSection("control"));traySection=ActionButton("系统托盘",()=>{SelectSection("tray");OpenNativeTray();});startSection=ActionButton("开始菜单",()=>{Collapse(true);NativeStartMenu.Open();});
            foreach(var button in new[]{controlSection,traySection,startSection}){button.Width=double.NaN;button.Height=44;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(3,0,3,0);row.Children.Add(button);}
            DockPanel.SetDock(row,Dock.Top);root.Children.Add(row);RefreshSections();MarkSection("control");
        }
        public void RefreshSections(){if(controlSection==null)return;var o=C.Prefs.ControlCenter;controlSection.Visibility=o.ShowControl?Visibility.Visible:Visibility.Collapsed;traySection.Visibility=o.ShowTray?Visibility.Visible:Visibility.Collapsed;startSection.Visibility=o.ShowStart?Visibility.Visible:Visibility.Collapsed;}
        void SetSectionSize(bool tray){if(!IsLoaded)return;var area=DesktopWindowPolicy.WorkArea(orb);Height=Math.Min(tray?460:550,area.Height-12);double above=orb.Top-Height+8;Top=above>=area.Top?above:Math.Min(area.Bottom-Height,orb.Top+orb.Height-8);}
        void SelectSection(string section){
            if(stopped)return;trayViewRevision++;section=OrbSections.Normalize(section);volumeDelay.Stop();brightnessDelay.Stop();var o=C.Prefs.ControlCenter;
            if(section=="tray"&&!o.ShowTray)section="control";if(section=="control"&&!o.ShowControl)section="tray";
            if(!o.ShowControl&&!o.ShowTray){page="empty";body.Children.Clear();Label(o.ShowStart?"点击上方开始菜单，打开 Windows 原生菜单。":"栏目已关闭。右键小球 → 小球设置，可重新开启。");return;}
            if(section=="tray")ShowTray();else{BuildHome();if(IsVisible)RefreshState();}
        }
        void MarkSection(string section){activeSection=OrbSections.Normalize(section);if(controlSection==null||traySection==null)return;
            controlSection.Background=Polish.Color(activeSection=="control"?"#66FFFFFF":"#24FFFFFF");traySection.Background=Polish.Color(activeSection=="tray"?"#66FFFFFF":"#24FFFFFF");
            controlSection.FontWeight=activeSection=="control"?FontWeights.SemiBold:FontWeights.Normal;traySection.FontWeight=activeSection=="tray"?FontWeights.SemiBold:FontWeights.Normal;
            controlSection.ToolTip="Wi-Fi、蓝牙、音量、亮度与电源";traySection.ToolTip="打开 Windows 原生托盘，使用原软件的图标菜单";
        }
        internal void CheckPeerSections(){
            if(controlSection==null||traySection==null||controlSection.Parent!=traySection.Parent)throw new Exception("小球入口不是平级");
            SelectSection("control");if(page!="home"||activeSection!="control")throw new Exception("控制中心入口异常");
            if(body.Children.OfType<Button>().Any(b=>Convert.ToString(b.Content).Contains("系统托盘")))throw new Exception("托盘仍嵌套于控制中心");
            SelectSection("tray");if(page!="tray"||activeSection!="tray"||body.Children.OfType<Button>().Any(b=>Convert.ToString(b.Content).Contains("返回控制中心")))throw new Exception("托盘仍依赖返回控制中心");
        }
    }
}
