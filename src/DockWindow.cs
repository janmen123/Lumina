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
    public partial class DockWindow : GlassWindow {
        Controller C;StackPanel Items=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
        HwndSource source;HotkeyManager hotkeys;
        int fullscreenTicks;
        readonly ScanPauseState scanPause = new ScanPauseState();
        DispatcherTimer autoTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)},runningTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(5)},foregroundTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};
        DateTime lastRunningScan=DateTime.MinValue;AutoHidePolicy hidePolicy=new AutoHidePolicy();bool renderedHidden;double shownTop;Rect dockWorkingArea,dockScreenArea;Thumb moveGrip;bool dragging;
        public double RestingTop {get{return shownTop;}}
        public void ScanPaused(bool paused){SetScanPause(ScanPauseReason.Game,paused);}
        void SetScanPause(ScanPauseReason reason,bool paused){
            scanPause.Set(reason,paused);
            if(scanPause.Paused){runningTimer.Stop();return;}
            if(!runningTimer.IsEnabled){lastRunningScan=DateTime.MinValue;runningTimer.Interval=TimeSpan.FromSeconds(5);runningTimer.Start();}
        }
        public DockWindow(Controller c):base(29) {
            C=c;Title="Lumina Dock";Height=112;ShowActivated=false;Topmost=false;ContentGrid.Children.Add(Items);InitDockPointer();InitializeBackgroundMove();InitTaskDock();
            moveGrip=new Thumb{Width=42,Height=6,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,7,0,0),Cursor=Cursors.SizeAll,ToolTip="拖动 Dock；设置中可锁定位置"};Root.Children.Add(moveGrip);
            moveGrip.Template=InvisibleDockGrip();Closing+=(s,e)=>{if(!C.ShuttingDown){e.Cancel=true;Dispatcher.BeginInvoke(new Action(()=>C.Quit()));}};
            moveGrip.DragStarted+=(s,e)=>BeginDockMove();moveGrip.DragDelta+=(s,e)=>{if(!dragging)return;var area=DockPlacementArea(true);Left=FeatureRules.Position(Left+e.HorizontalChange,area.Left,area.Width,Width);Top=DockGeometryRules.Top(Top+e.VerticalChange,area,Height);};moveGrip.DragCompleted+=(s,e)=>FinishDockMove();
            autoTimer.Tick+=(s,e)=>{RunningWindows.RememberForeground();UpdateAutoHide();};
            foregroundTimer.Tick+=(s,e)=>{RunningWindows.RememberForeground();if(C.Prefs.AutoGameMode)C.CheckGameMode();if(!C.Prefs.Dock.AutoHide&&++fullscreenTicks>=3){fullscreenTicks=0;UpdateAutoHide();}};
            foregroundTimer.Start();runningTimer.Tick+=(s,e)=>{ReleaseCancelledExternalDrag();if(boardPinDragging)return;C.CheckGameMode();if(!C.Prefs.AutoGameMode){var slow=IdleState.Seconds()>=90?TimeSpan.FromSeconds(15):TimeSpan.FromSeconds(5);if(runningTimer.Interval!=slow)runningTimer.Interval=slow;}if(C.GlassSuppressed||(!IsVisible&&!C.Board.IsVisible))return;if((!C.Prefs.Dock.ShowRunning&&!taskWheel.Running&&!C.Prefs.Dock.ShowRunningSection)||(DateTime.UtcNow-lastRunningScan).TotalSeconds<(renderedHidden?20:5))return;lastRunningScan=DateTime.UtcNow;RunningWindows.Scan();RefreshTaskSnapshot();UpdateRunning();C.Board.UpdateRunning();};runningTimer.Start();
            SourceInitialized+=(s,e)=> {
                source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);source.AddHook(Hook);
                hotkeys=new HotkeyManager(RegisterKey,id=>Native.UnregisterHotKey(source.Handle,id));
                var failed=hotkeys.Start(C.Prefs.Hotkeys);
                if(failed.Count>0)TrayWarning(string.Join("、",failed));
            };
            var menu=MenuTheme.Create(C.Prefs.Light);var addDock=new MenuItem{Header="添加软件或文件到 Dock"};addDock.Click+=(ss,ee)=>C.ChooseDockFiles();menu.Items.Add(addDock);var settings=new MenuItem{Header="外观设置"};settings.Click+=(s,e)=>C.Settings();menu.Items.Add(settings);AddDockControls(menu);
            var quit=new MenuItem{Header="退出"};quit.Click+=(s,e)=>C.Quit();menu.Items.Add(quit);ContextMenu=menu;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        }
        void TrayWarning(string shortcuts) {Dispatcher.BeginInvoke(new Action(()=> {if(C.Tray!=null)C.Tray.ShowBalloonTip(3000,"Lumina",shortcuts+" 已被占用，对应功能仍可从托盘操作。",Forms.ToolTipIcon.Info);}));}
        bool RegisterKey(HotkeySetting key) {bool ok=Native.RegisterHotKey(source.Handle,key.ActionId,key.Modifiers|0x4000,key.Key);if(!ok){int code=Marshal.GetLastWin32Error();Store.Log("RegisterHotKey failed action="+HotkeyRules.Actions[key.ActionId-1]+" key="+HotkeyRules.Label(key),new Win32Exception(code,"RegisterHotKey"));}return ok;}
        void DisplayChanged(object s,EventArgs e) {Dispatcher.BeginInvoke(new Action(()=>{C.Board.Hide();Refresh();}));}
        public bool ApplyHotkeys(List<HotkeySetting> candidate,out string error) {if(hotkeys==null){error="快捷键窗口尚未就绪。";return false;}return hotkeys.TryApply(candidate,out error);}
        public bool HotkeyActive(int id) {return hotkeys!=null&&hotkeys.Active.Any(k=>k.ActionId==id);}
        public string RetryHotkeys() {if(hotkeys==null)return "快捷键窗口尚未就绪。";hotkeys.Release();var failed=hotkeys.Start(C.Prefs.Hotkeys);return failed.Count==0?"已重新注册启用的快捷键。":"以下组合被占用，请改键或使用开关：\n"+string.Join("\n",failed);}
        public List<HotkeySetting> SuspendHotkeys(){var previous=hotkeys==null?new List<HotkeySetting>():hotkeys.Active.Select(k=>k.Copy()).ToList();if(hotkeys!=null)hotkeys.Release();return previous;}
        public void ResumeHotkeys(List<HotkeySetting> keys){if(hotkeys==null)return;hotkeys.Release();var failed=hotkeys.Start(keys);if(failed.Count>0)TrayWarning(string.Join("、",failed));}
        public void ReleaseHotkey() {RunningWindows.CancelKeepVisible();CancelDockPointer(false);autoTimer.Stop();runningTimer.Stop();foregroundTimer.Stop();taskHintTimer.Stop();if(hotkeys!=null)hotkeys.Release();Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;}
        IntPtr Hook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled) {
            // Preserve the application's foreground state until the icon action
            // decides whether to restore or minimize it; still deliver the click.
            if(msg==0x21){handled=true;return new IntPtr(3);} // MA_NOACTIVATE
            if(msg==Native.RestoreMessage){handled=true;Dispatcher.BeginInvoke(new Action(()=>C.ShellAction(3)));return IntPtr.Zero;}
            if(msg==0x312){int id=w.ToInt32();handled=true;Dispatcher.BeginInvoke(new Action(()=>{if(id==1)C.Toggle();else if(id==2)C.ShellAction(1);else if(id==3)C.ShellAction(2);else if(id==4)C.AddFromAnywhere();else if(id==5)C.Settings();else if(id==6)C.ShellAction(3);else if(id==7)C.ToggleTaskbarAutoHide();}));}return IntPtr.Zero;
        }
        public void Refresh(bool capture=true) {
            if(DockInputBusy)return;
            if(thumbnails==null||!thumbnails.PointerInside)HideThumbnails();
            double oldWidth=Width,oldLeft=Left;StopDockLayoutMotion();
            var o=C.Prefs.Dock;o.Normalize();LightTheme=C.Prefs.Light;MotionEnabled=o.OpenMotion;MotionLevel=o.MotionLevel;OpenStyle=o.OpenAnimationStyle;ConfigureGlass(o);Topmost=InteractionRules.DockTopmost(o,boardPinDragging);RefreshLayer();
            revealSerial++;BeginAnimation(TopProperty,null);renderedHidden=false;hidePolicy=new AutoHidePolicy();moveGrip.Visibility=Visibility.Visible;moveGrip.Cursor=C.Prefs.Placement.DockLocked?Cursors.Arrow:Cursors.SizeAll;moveGrip.ToolTip=C.Prefs.Placement.DockLocked?"右键 Dock → 允许自由拖动 Dock":"拖动顶部空白区域移动 Dock，松手保存";
            Items.Children.Clear();Items.Margin=DockGeometryRules.Insets(o);Surface.CornerRadius=new CornerRadius(o.Radius);
            AddSystemButton(DockLayoutRules.Apps,DockButton("▦",null,()=>C.Toggle(),"展开大号应用面板"),()=>C.Toggle());
            if(o.ShowComputer)AddSystemButton(DockLayoutRules.Computer,DockButton("computer",null,()=> {C.Board.Hide();Native.Computer();},"此电脑"),()=>{C.Board.Hide();Native.Computer();});
            
            var area=DockPlacementArea();dockWorkingArea=area;dockScreenArea=DesktopWindowPolicy.WorkArea(this,false,true);var pinned=DockOrderRules.Ordered(C.Prefs);int displayCount=taskWheel.Running?taskGroups.Count:pinned.Count;int slots=o.VisiblePinned(area.Width-20,displayCount);
            int shown=displayCount>slots?Math.Max(0,slots-1):slots;int extrasShown=0;
            if(taskWheel.Running)BuildTaskButtons(shown,slots);else {
            foreach(var a in pinned.Take(shown)) {var b=DockButton(null,FeatureUi.Icon(a,C.Prefs.Dock.ShowShortcutOverlays,C.Prefs.Dock.SharperIcons),()=>C.LaunchDock(a),a.Name,a);b.ContextMenu=C.Menu(a,"Dock");AddDockControls(b.ContextMenu);Items.Children.Add(b);}
            if(pinned.Count>shown&&slots>0) {
                var more=DockButton("···",null,()=>{},"更多固定应用");var menu=MenuTheme.Create(C.Prefs.Light);
                foreach(var a in pinned.Skip(shown)) {var item=new MenuItem{Header=a.Name};var entry=a;item.Click+=(s,e)=>{if(!DockDragSuppresses(item))C.LaunchDock(entry);};BindDockSource(item,entry,()=>menu.IsOpen=false);menu.Items.Add(item);}
                AddDockControls(menu);more.ContextMenu=menu;more.Click+=(s,e)=>{menu.PlacementTarget=more;menu.Placement=PlacementMode.Top;menu.IsOpen=true;};Items.Children.Add(more);
            }
            if(o.ShowRunningSection&&!C.GlassSuppressed){RunningWindows.Scan();ReadTaskGroups();if(taskGroups.Count>0){var pinnedWindows=new HashSet<IntPtr>(pinned.SelectMany(a=>RunningWindows.For(a)).Select(w=>w.Handle));
            var extras=taskGroups.Where(g=>!g.Windows.All(w=>pinnedWindows.Contains(w.Handle))).ToList();
            Store.Trace("DOCK groups="+taskGroups.Count+" pinned="+pinnedWindows.Count+" extraGroups="+extras.Count);
            double cell=o.IconSize+16+o.Spacing;int fixedSlots=1+(o.ShowSettings?1:0)+(o.ShowComputer?1:0)+(o.ShowPower?1:0)+(o.ShowNetwork?1:0)+(o.ShowRecycleBin?1:0);int fit=Math.Max(0,(int)Math.Floor((area.Width-20-28-o.HoverPadding()*2-(o.ShowSeparator?17:0))/cell)-fixedSlots);int room=Math.Max(0,fit-shown-1);var overflow=new List<TaskGroup>();if(extras.Count>room){int keep=room>0?room-1:0;overflow=extras.Skip(keep).ToList();extras=extras.Take(keep).ToList();}if(extras.Count>0||overflow.Count>0){extrasShown=extras.Count;Items.Children.Add(new Border{Width=1,Height=o.IconSize*.7,Margin=new Thickness(6,0,6,0),Background=new SolidColorBrush(Color.FromArgb(65,220,220,230)),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false});foreach(var group in extras){var selected=group;var button=DockButton(null,TaskIcon(group),()=>OpenTask(selected,null),"正在运行 · "+group.Name+(group.Windows.Count>1?" · "+group.Windows.Count+" 个窗口":""));button.Tag=group;BindPointerItem(button,"running:"+group.Key,group.Name,()=>OpenTask(selected,button));var grid=button.Content as Grid;if(grid!=null)grid.Children.Add(RunningBadge.Create());button.ContextMenu=TaskWindowMenu(group);Items.Children.Add(button);}if(overflow.Count>0){extrasShown++;var moreRun=DockButton("···",null,()=>{},"更多正在运行的软件");var runMenu=MenuTheme.Create(C.Prefs.Light);foreach(var group in overflow){var selected=group;var item=new MenuItem{Header=group.Name+(group.Windows.Count>1?"（"+group.Windows.Count+"）":"")};foreach(var window in group.Windows)item.Items.Add(TaskWindowItem(window));runMenu.Items.Add(item);}AddDockControls(runMenu);moreRun.ContextMenu=runMenu;moreRun.Click+=(s,e)=>{runMenu.PlacementTarget=moreRun;runMenu.Placement=PlacementMode.Top;runMenu.IsOpen=true;};Items.Children.Add(moreRun);}}}}
            if(pinned.Count==0) Items.Children.Add(DockButton("+",null,()=>C.Toggle(true),"打开面板，可拖入软件固定"));
            }
            AddQuickButtons();
            if(o.ShowSettings)AddSystemButton(DockLayoutRules.Settings,DockButton("⚙",null,()=>C.Settings(),"DIY 设置"),()=>C.Settings());
            ArrangeDockItems();
            Width=Math.Min(area.Width-20,o.WidthFor(displayCount==0?1:slots+extrasShown)+(extrasShown>0&&o.ShowSeparator?17:0));
            Height=DockGeometryRules.Height(o);
            double x=o.Alignment==0?area.Left+10:o.Alignment==2?area.Right-Width-10:area.Left+(area.Width-Width)/2;
            var placement=C.Prefs.Placement;
            if(placement.DockLeft.HasValue&&!placement.DockCenter.HasValue)placement.DockCenter=placement.DockLeft.Value+Width/2;
            Left=DockAnchorRules.Left(placement.DockLeft.HasValue?placement.DockCenter:null,Width,x+o.Offset,area.Left,area.Width);Top=DockGeometryRules.Top(placement.DockTop??DockGeometryRules.BottomTop(area,Height,C.Prefs.DockGap),area,Height);shownTop=Top;
            if(o.AutoHide)autoTimer.Start();else autoTimer.Stop();
            GlassPercent=o.OpacityPercent;
            if(capture)Backdrop(C.Prefs.Light);else {
                SetGlassOpacity(GlassPercent);
                foreach(var host in Root.Children.OfType<Border>().Where(b=>b.Tag as string=="backdrop")) {
                    var clip=new RectangleGeometry(new Rect(0,0,Math.Max(1,Width-28),Math.Max(1,Height-28)),o.Radius,o.Radius);host.Clip=clip;
                    var bg=host.Child as Border;if(bg!=null){bg.Clip=clip;bg.CornerRadius=Surface.CornerRadius;bg.Effect=new BlurEffect{Radius=MaterialBlur,RenderingBias=RenderingBias.Performance};}
                }
            }
            var accent=Accent(o.Accent);Surface.BorderBrush=GlassPercent==0?(Brush)Brushes.Transparent:new LinearGradientBrush(Color.FromArgb((byte)Math.Min(255,160*EdgeStrength),accent.R,accent.G,accent.B),Color.FromArgb((byte)Math.Min(255,30*EdgeStrength),255,255,255),90);UpdateRunning();if(!capture)AnimateDockLayout(oldLeft,oldWidth);
        }
        void UpdateAutoHide(){ReleaseCancelledExternalDrag();if(C.GlassSuppressed||source==null||!IsVisible)return;RefreshLayer();if(C.Prefs.Dock.RespectFullscreen&&FullscreenState.Active(source.Handle)&&!boardPinDragging){ForceHideForFullscreen();SetScanPause(ScanPauseReason.Fullscreen,true);return;}SetScanPause(ScanPauseReason.Fullscreen,false);IsHitTestVisible=true;var position=Forms.Cursor.Position;var pointer=source.CompositionTarget.TransformFromDevice.Transform(new Point(position.X,position.Y));double bottom=dockScreenArea.Bottom;
            bool over=!renderedHidden&&pointer.X>=Left&&pointer.X<=Left+Width&&pointer.Y>=shownTop&&pointer.Y<=shownTop+Height;
            bool edge=pointer.X>=Left-12&&pointer.X<=Left+Width+12&&pointer.Y>=bottom-C.Prefs.Dock.EdgeRange;
            bool menu=(ContextMenu!=null&&ContextMenu.IsOpen)||Items.Children.OfType<Button>().Any(b=>b.ContextMenu!=null&&b.ContextMenu.IsOpen);
            bool hide=hidePolicy.Update(DateTime.UtcNow,C.Prefs.Dock.AutoHide,over,edge,dragging||DockInputBusy||externalDockDrag||(thumbnails!=null&&thumbnails.PointerInside)||(taskMenu!=null&&taskMenu.IsOpen)||menu||C.UiBusy||C.Board.IsVisible,C.Prefs.Dock.HideDelay);
            if(hide==renderedHidden)return;renderedHidden=hide;double target=hide?bottom-20:shownTop;AnimateDockVisibility(hide,target);
        }
        void UpdateRunning(){foreach(var button in Items.Children.OfType<Button>()){var grid=button.Content as Grid;if(grid==null)continue;var entry=button.Tag as Entry;if(entry!=null){int count=C.Prefs.Dock.ShowRunning?RunningWindows.Count(entry):0;foreach(var dot in grid.Children.OfType<Border>().Where(b=>b.Tag as string=="running"))RunningBadge.Apply(dot,count);continue;}var group=button.Tag as TaskGroup;if(group!=null)foreach(var dot in grid.Children.OfType<Border>().Where(b=>b.Tag as string=="running"))RunningBadge.Apply(dot,group.Windows.Count);}}
        static Color Accent(int index) {return new Color[]{Color.FromRgb(121,185,250),Color.FromRgb(183,149,245),Color.FromRgb(105,218,183),Color.FromRgb(242,193,104),Color.FromRgb(244,150,178),Color.FromRgb(194,203,217)}[index];}
        Button DockButton(string text,ImageSource icon,Action action,string tip,Entry entry=null) {
            var o=C.Prefs.Dock;double size=o.IconSize;
            var b=Button("",(s,e)=>{if(!dockIconDragging&&!DockDragSuppresses(s as FrameworkElement))action();},size+16);b.Height=size+16+DockReflection.ExtraHeight(o)+(o.ShowLabels?18:0);b.Margin=new Thickness(o.Spacing/2,0,o.Spacing/2,0);b.ToolTip=tip;
            ToolTipService.SetIsEnabled(b,false);
            var glass=new FrameworkElementFactory(typeof(Border));glass.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromArgb(1,255,255,255)));
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);glass.AppendChild(presenter);b.Template=new ControlTemplate(typeof(Button)){VisualTree=glass};
            FrameworkElement visual;
            if(text=="computer") {
                var color=Accent(o.Accent);var monitor=new Grid{Width=44,Height=44};bool badge=o.ComputerStyle==0;
                if(badge)monitor.Children.Add(new Border{CornerRadius=new CornerRadius(12),Background=new LinearGradientBrush(Color.FromRgb((byte)(color.R*.65),(byte)(color.G*.65),(byte)(color.B*.65)),color,45),BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromArgb(90,255,255,255))});
                var stroke=badge?Brushes.White:TextBrush;
                monitor.Children.Add(new Border{Height=badge?19:25,Width=badge?29:38,CornerRadius=new CornerRadius(3),BorderThickness=new Thickness(1.6),BorderBrush=stroke,Background=badge?new SolidColorBrush(Color.FromArgb(36,255,255,255)):Brushes.Transparent,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,badge?9:5,0,0)});
                monitor.Children.Add(new Border{Width=2,Height=5,Background=stroke,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,badge?28:30,0,0)});
                monitor.Children.Add(new Border{Width=15,Height=1.6,CornerRadius=new CornerRadius(1),Background=stroke,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,badge?33:35,0,0)});
                if(badge)monitor.Children.Add(new Border{Width=19,Height=1,Background=new SolidColorBrush(Color.FromArgb(100,255,255,255)),VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,12,0,0)});
                visual=new Viewbox{Width=size,Height=size,Child=monitor,Stretch=Stretch.Uniform};
            }
            else if(icon!=null)visual=new Image{Source=icon,Width=size,Height=size,Stretch=Stretch.Uniform};
            else visual=new TextBlock{Text=text??"□",FontSize=size*.72,Width=size,Height=size,TextAlignment=TextAlignment.Center,Foreground=TextBrush,VerticalAlignment=VerticalAlignment.Center};
            FrameworkElement mirror=DockReflection.Create(visual,o);
            var box=new StackPanel{VerticalAlignment=VerticalAlignment.Center};box.Children.Add(visual);if(mirror!=null)box.Children.Add(mirror);
            if(o.ShowLabels)box.Children.Add(new TextBlock{Text=text=="computer"?"此电脑":text=="▦"?"应用":text=="⚙"?"设置":text=="···"?"更多":tip,FontSize=10,Foreground=MutedBrush,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=size+12,Margin=new Thickness(0,4,0,0)});
            var content=new Grid{IsHitTestVisible=false};content.Children.Add(box);if(entry!=null){b.Tag=entry;content.Children.Add(RunningBadge.Create());}b.Content=content;
            var scale=new ScaleTransform(1,1);var lift=new TranslateTransform();var transforms=new TransformGroup();transforms.Children.Add(scale);transforms.Children.Add(lift);b.RenderTransform=transforms;b.RenderTransformOrigin=new Point(.5,.65);
            Action<bool> animate=hover=> {if(!C.Prefs.Dock.HoverMotion)return;var ease=new CubicEase{EasingMode=EasingMode.EaseOut};double target=hover&&InteractionRules.ZoomHover(o.HoverAnimationStyle)?Math.Min(1.6,1+(o.Zoom-1)*MotionLevel):1;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(target,TimeSpan.FromMilliseconds(180)){EasingFunction=ease});scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(target,TimeSpan.FromMilliseconds(180)){EasingFunction=ease});lift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(hover&&InteractionRules.LiftHover(o.HoverAnimationStyle)?-5*MotionLevel:0,TimeSpan.FromMilliseconds(180)){EasingFunction=ease});};
            b.MouseEnter+=(s,e)=>{if(!DockInputBusy)animate(true);};b.MouseLeave+=(s,e)=>animate(false);if(entry!=null)BindDockIcon(b,entry);return b;
        }
    }
}
