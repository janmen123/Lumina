using System;using System.Linq;using System.Windows;using System.Windows.Controls;using System.Windows.Controls.Primitives;using System.Windows.Input;using System.Windows.Media;using System.Windows.Media.Animation;
namespace GlassBoard {
    public partial class DockWindow {
        int revealSerial,layoutSerial;
        Rect DockPlacementArea(bool pointer=false){return DesktopWindowPolicy.WorkArea(this,pointer,C.Prefs.Dock.UseScreenBounds);}
        void StopDockLayoutMotion(){double width=Width,left=Left;layoutSerial++;BeginAnimation(WidthProperty,null);BeginAnimation(LeftProperty,null);if(!double.IsNaN(width))Width=width;if(!double.IsNaN(left))Left=left;}
        void AnimateDockLayout(double oldLeft,double oldWidth){
            // Layered HWND size animation forces a new layout and surface on every frame.
            // Keep the settled geometry and let icon transitions carry the visual feedback.
            layoutSerial++;BeginAnimation(WidthProperty,null);BeginAnimation(LeftProperty,null);
        }
        public void AddDockControls(ContextMenu menu){
            AddTaskDockMenu(menu);menu.Items.Add(new Separator());
            var movable=new MenuItem{Header="允许自由拖动 Dock",IsCheckable=true};
            var spring=new MenuItem{Header="弹簧弹出效果",IsCheckable=true,ToolTip="自动隐藏后弹出时生效，遵循展开 / 收起动画开关"};
            var automatic=new MenuItem{Header="Dock 自动隐藏",IsCheckable=true};
            menu.Opened+=(s,e)=>{movable.IsChecked=!C.Prefs.Placement.DockLocked;spring.IsChecked=C.Prefs.Dock.SpringReveal;automatic.IsChecked=C.Prefs.Dock.AutoHide;};
            movable.Click+=(s,e)=>{C.Prefs.Placement.DockLocked=!movable.IsChecked;C.Save();Refresh(false);};
            spring.Click+=(s,e)=>{C.Prefs.Dock.SpringReveal=spring.IsChecked;C.Save();};
            automatic.Click+=(s,e)=>{C.Prefs.Dock.AutoHide=automatic.IsChecked;C.Save();Refresh(false);};
            menu.Items.Add(movable);menu.Items.Add(spring);menu.Items.Add(automatic);var settingsIcon=new MenuItem{Header="显示 Dock 设置图标",IsCheckable=true};menu.Opened+=(s,e)=>settingsIcon.IsChecked=C.Prefs.Dock.ShowSettings;settingsIcon.Click+=(s,e)=>{C.Prefs.Dock.ShowSettings=settingsIcon.IsChecked;C.Save();Refresh(false);};menu.Items.Add(settingsIcon);
        }
        void InitializeBackgroundMove(){Root.Background=Brushes.Transparent;Root.PreviewMouseLeftButtonDown+=(s,e)=>{
            if(e.Handled||e.ButtonState!=MouseButtonState.Pressed||C.Prefs.Placement.DockLocked||DockInputBusy)return;
            DependencyObject element=e.OriginalSource as DependencyObject;
            while(element!=null&&element!=Root){if(element is ButtonBase||element is Thumb||element is TextBoxBase)return;element=element is Visual?VisualTreeHelper.GetParent(element):LogicalTreeHelper.GetParent(element);}
            if(element!=Root)return;e.Handled=true;
            BeginDockMove();try{DragMove();}catch(InvalidOperationException){}finally{FinishDockMove();}
        };}
        void BeginDockMove(){if(C.Prefs.Placement.DockLocked)return;StopDockLayoutMotion();dragging=true;double actual=Top;revealSerial++;BeginAnimation(TopProperty,null);Top=actual;renderedHidden=false;hidePolicy=new AutoHidePolicy();ResetDockHover();}
        void FinishDockMove(){if(!dragging)return;try{var area=DockPlacementArea();dockWorkingArea=area;dockScreenArea=DesktopWindowPolicy.WorkArea(this,false,true);Left=FeatureRules.Position(Left,area.Left,area.Width,Width);Top=DockGeometryRules.Top(Top,area,Height);shownTop=Top;C.Prefs.Placement.DockLeft=Left;C.Prefs.Placement.DockCenter=Left+Width/2;C.Prefs.Placement.DockTop=Top;hidePolicy=new AutoHidePolicy();C.Save();Backdrop(C.Prefs.Light);}finally{dragging=false;}}
        void AnimateDockVisibility(bool hide,double target){
            double actual=Top;int serial=++revealSerial;BeginAnimation(TopProperty,null);Top=target;
            if(!MotionEnabled)return;
            if(!hide&&C.Prefs.Dock.SpringReveal){
                var points=DockRevealRules.Stops(actual,target,MotionLevel,dockWorkingArea.Top);
                var animation=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromMilliseconds(420),FillBehavior=FillBehavior.Stop};
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(points[0],KeyTime.FromTimeSpan(TimeSpan.Zero)));
                animation.KeyFrames.Add(new SplineDoubleKeyFrame(points[1],KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230)),new KeySpline(.18,.72,.28,1)));
                animation.KeyFrames.Add(new SplineDoubleKeyFrame(points[2],KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(330)),new KeySpline(.35,0,.65,1)));
                animation.KeyFrames.Add(new SplineDoubleKeyFrame(points[3],KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420)),new KeySpline(.35,0,.5,1)));
                animation.Completed+=(s,e)=>{if(serial==revealSerial)BeginAnimation(TopProperty,null);};BeginAnimation(TopProperty,animation,HandoffBehavior.SnapshotAndReplace);
            }else{
                var animation=new DoubleAnimation(actual,target,TimeSpan.FromMilliseconds(hide?160:190)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
                animation.Completed+=(s,e)=>{if(serial==revealSerial)BeginAnimation(TopProperty,null);};BeginAnimation(TopProperty,animation,HandoffBehavior.SnapshotAndReplace);
            }
        }
    }
}
