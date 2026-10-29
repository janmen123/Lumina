using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GlassBoard {
    public partial class Controller {
        public void LaunchDock(Entry entry){LaunchDock(entry,IntPtr.Zero);}
        public void LaunchDock(Entry entry,IntPtr hint){
            bool handled=false;
            RecentRules.Touch(Prefs,entry==null?null:entry.Path);
            try{handled=RunningWindows.ToggleDock(entry,hint);}
            catch(Exception ex){Store.Log(ex);Store.Trace("LAUNCH toggle failed entry="+entry.Name+" error="+ex.GetType().Name+" "+ex.Message);}
            if(handled){Board.CollapseSmooth();return;}
            try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(entry.Path){UseShellExecute=true});Board.CollapseSmooth();}
            catch(Exception ex){MessageBox.Show("无法打开「"+entry.Name+"」。\n入口可能已移动或卸载。\n\n"+ex.Message,"GlassBoard");}
        }
        public bool ReorderDock(string path,string target,bool before){var state=BeginBoardEdit();if(!Dock.MoveLayout(path,target,before))return false;EndBoardEdit(state,"调整 Dock 顺序");Dock.Refresh(false);Board.Render();return true;}
        public void StepDock(string path,int step){var state=BeginBoardEdit();if(!Dock.StepLayout(path,step))return;EndBoardEdit(state,"调整 Dock 顺序");Dock.Refresh(false);Board.Render();}
        void AddDockWindowMenus(ContextMenu menu,Entry entry){
            bool folder=entry.IsFolder||System.IO.Directory.Exists(entry.Path);
            var opened=new MenuItem{Header="切换已打开的窗口"};var associate=new MenuItem{Header="关联正在运行的窗口",ToolTip="用于游戏、特殊启动器或无法自动识别的软件；关联后点击切回该软件"};menu.Items.Add(opened);if(!folder)menu.Items.Add(associate);var close=new MenuItem{Header="关闭窗口"};menu.Items.Add(close);close.Click+=(s,e)=>{foreach(var window in RunningWindows.For(entry))RunningWindows.RequestClose(window);};
            menu.Opened+=(s,e)=>{
                RunningWindows.Scan();opened.Items.Clear();associate.Items.Clear();var matches=RunningWindows.For(entry);opened.IsEnabled=close.IsEnabled=matches.Count>0;
                foreach(var window in matches){var selected=window;var item=new MenuItem{Header=WindowLabel(selected),ToolTip=selected.Title};item.Click+=(ss,ee)=>RunningWindows.ActivateHandle(selected.Handle);opened.Items.Add(item);}
                var available=RunningWindows.Snapshot();associate.IsEnabled=available.Count>0||!string.IsNullOrEmpty(entry.WindowTarget);
                foreach(var window in available){var selected=window;var item=new MenuItem{Header=WindowLabel(selected),ToolTip=selected.Path,IsCheckable=true,IsChecked=string.Equals(entry.WindowTarget,selected.Path,StringComparison.OrdinalIgnoreCase)};item.Click+=(ss,ee)=>{var state=BeginBoardEdit();entry.WindowTarget=selected.Path;EndBoardEdit(state,"关联 Dock 窗口");Refresh();};associate.Items.Add(item);}
                if(!string.IsNullOrEmpty(entry.WindowTarget)){associate.Items.Add(new Separator());var clear=new MenuItem{Header="取消手动关联"};clear.Click+=(ss,ee)=>{var state=BeginBoardEdit();entry.WindowTarget=null;EndBoardEdit(state,"取消 Dock 窗口关联");Refresh();};associate.Items.Add(clear);}
            };
        }
        static string WindowLabel(RunningWindow window){string title=string.IsNullOrWhiteSpace(window.Title)?System.IO.Path.GetFileNameWithoutExtension(window.Path):window.Title;return title.Length>72?title.Substring(0,69)+"…":title;}
    }
    public partial class DockWindow {
        static readonly DependencyProperty DragSuppressed=DependencyProperty.RegisterAttached("DockDragSuppressed",typeof(bool),typeof(DockWindow),new PropertyMetadata(false));
        Action dockPointerClick;bool dockIconDragging,dockPointerPreviousBusy;DockPointerGesture dockPointer;FrameworkElement dockPointerOwner;bool dockPointerFromMenu;
        internal IntPtr pointerHint;
        readonly DispatcherTimer dockPointerWatch=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(33)};
        Border dockPointerPreview;TextBlock dockPointerLabel;
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        bool DockInputBusy{get{return dockPointer!=null||dockIconDragging;}}
        bool DockDragSuppresses(FrameworkElement element){return element!=null&&(bool)element.GetValue(DragSuppressed);}
        void BindDockSource(FrameworkElement element,Entry entry,Action beforeDrag=null){BindPointerItem(element,entry.Path,entry.Name,()=>C.LaunchDock(entry,pointerHint),beforeDrag);}
        void BindPointerItem(FrameworkElement element,string key,string name,Action click,Action beforeDrag=null){
            element.ToolTip=name+" · 点击打开；按住拖动调整 Dock 位置";ToolTipService.SetIsEnabled(element,false);
            element.AddHandler(Mouse.PreviewMouseDownEvent,new MouseButtonEventHandler((s,e)=>{if(e.ChangedButton!=MouseButton.Left||DockInputBusy)return;Point point=e.GetPosition(this);if(StartPointerItem(element,key,name,click,point,beforeDrag))e.Handled=true;}),true);
        }
        bool StartDockPointer(FrameworkElement owner,Entry entry,Point point,Action beforeDrag){return StartPointerItem(owner,entry.Path,entry.Name,()=>C.LaunchDock(entry),point,beforeDrag);}
        bool StartPointerItem(FrameworkElement owner,string key,string name,Action click,Point point,Action beforeDrag){
            if(!IsVisible||!LayoutKeys.Contains(key,StringComparer.OrdinalIgnoreCase))return false;
            // Remember what the user was looking at before the click is handled,
            // so a Dock click can minimize/restore exactly like the taskbar.
            pointerHint=RunningWindows.CaptureForegroundHint();
            if(beforeDrag!=null)beforeDrag();
            if(!Mouse.Capture(this,CaptureMode.Element)){Store.Trace("DOCK pointer capture failed");return false;}
            StopDockLayoutMotion();dockPointer=new DockPointerGesture(key,point.X,point.Y,SystemParameters.MinimumHorizontalDragDistance,SystemParameters.MinimumVerticalDragDistance);
            dockPointerClick=click;dockPointerOwner=owner;dockPointerFromMenu=owner is MenuItem;owner.SetValue(DragSuppressed,true);dockPointerPreviousBusy=C.Board.ModalBusy;C.Board.ModalBusy=true;
            dockPointerLabel.Text=name;dockPointerWatch.Start();Store.Trace("DOCK pointer press");return true;
        }
        void MoveDockPointer(Point point){
            if(dockPointer==null)return;bool wasDragging=dockPointer.Dragging;dockPointer.Move(point.X,point.Y);if(!dockPointer.Dragging)return;
            if(!wasDragging){dockIconDragging=true;ResetDockHover();if(dockPointerOwner!=null){dockPointerOwner.BeginAnimation(OpacityProperty,null);dockPointerOwner.Opacity=.45;}Store.Trace("DOCK pointer dragging");}
            Point onRoot=TranslatePoint(point,Root);dockPointerPreview.RenderTransform=new TranslateTransform(onRoot.X+12,onRoot.Y-36);dockPointerPreview.Visibility=Visibility.Visible;
            ShowDockDrop(InsideDock(point)?ResolveDockDrop(dockPointer.Source,TranslatePoint(point,Items).X):null);
        }
        bool InsideDock(Point point){return point.X>=0&&point.Y>=0&&point.X<=ActualWidth&&point.Y<=ActualHeight;}
        void FinishDockPointer(Point point){
            if(dockPointer==null)return;MoveDockPointer(point);var gesture=dockPointer;bool inside=InsideDock(point);
            var result=gesture.Release(point.X,point.Y,inside,inside?ResolveDockDrop(gesture.Source,TranslatePoint(point,Items).X):null,dockPointerFromMenu||inside);
            var click=dockPointerClick;ClearDockPointer();bool changed=false;
            if(result.Target!=null)changed=C.ReorderDock(gesture.Source,result.Target.Path,result.Target.Before);
            if(!changed&&!result.Click)Refresh(false);
            Store.Trace("DOCK pointer release dragged="+result.Dragged+" inside="+inside+" candidate="+(result.Target!=null)+" changed="+changed);
            if(result.Click&&click!=null)click();
        }
        void ClearDockPointer(){
            var owner=dockPointerOwner;dockPointer=null;dockPointerClick=null;dockPointerOwner=null;dockIconDragging=false;dockPointerWatch.Stop();C.Board.ModalBusy=dockPointerPreviousBusy;
            try{ClearDockMarkers();dockPointerPreview.Visibility=Visibility.Collapsed;dockPointerLabel.Text="";
                if(owner!=null){owner.BeginAnimation(OpacityProperty,null);owner.Opacity=1;if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>owner.SetValue(DragSuppressed,false)));}
            }finally{if(Mouse.Captured==this)Mouse.Capture(null);}
        }
        void RouteDockPointer(Action action){try{action();}catch(Exception ex){Store.Log(ex);CancelDockPointer(false);if(IsVisible&&!C.ShuttingDown)Refresh(false);}}

        void CancelDockPointer(bool refresh=true){if(dockPointer==null)return;dockPointer.Cancel();ClearDockPointer();Store.Trace("DOCK pointer cancelled");if(refresh&&IsVisible&&!C.ShuttingDown)Refresh(false);}
        void ClearDockMarkers(){foreach(var button in Items.Children.OfType<Button>()){var grid=button.Content as Grid;if(grid!=null)foreach(var marker in grid.Children.OfType<Border>().Where(b=>b.Tag as string=="dockInsert"))marker.Visibility=Visibility.Collapsed;}}
        void ResetDockHover(){foreach(var button in Items.Children.OfType<Button>()){var group=button.RenderTransform as TransformGroup;if(group==null)continue;foreach(var scale in group.Children.OfType<ScaleTransform>()){scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);scale.ScaleX=scale.ScaleY=1;}foreach(var lift in group.Children.OfType<TranslateTransform>()){lift.BeginAnimation(TranslateTransform.YProperty,null);lift.Y=0;}}}
        void BindDockIcon(Button button,Entry entry){BindDockSource(button,entry);AddInsertMarker(button);}
        DockDropTarget ResolveDockDrop(string source,double x){
            var buttons=Items.Children.OfType<Button>().Where(b=>ItemKey(b)!=null).ToList();
            foreach(var button in buttons){double start=button.TranslatePoint(new Point(0,0),Items).X;if(x>=start&&x<=start+button.ActualWidth)return DockLayoutRules.OnIcon(C.Prefs,source,ItemKey(button),taskWheel.Running?TaskKeys:null);}
            return DockDropRules.OnGap(source,buttons.Select(ItemKey).ToArray(),buttons.Select(b=>b.TranslatePoint(new Point(b.ActualWidth/2,0),Items).X).ToArray(),x);
        }
        void ShowDockDrop(DockDropTarget drop){ClearDockMarkers();if(drop==null)return;var buttons=Items.Children.OfType<Button>().Where(b=>ItemKey(b)!=null).ToList();var target=drop.Path==null?buttons.LastOrDefault():buttons.FirstOrDefault(b=>string.Equals(ItemKey(b),drop.Path,StringComparison.OrdinalIgnoreCase));var grid=target==null?null:target.Content as Grid;if(grid==null)return;foreach(var marker in grid.Children.OfType<Border>().Where(b=>b.Tag as string=="dockInsert")){marker.HorizontalAlignment=drop.Path!=null&&drop.Before?HorizontalAlignment.Left:HorizontalAlignment.Right;marker.Visibility=Visibility.Visible;}}
        void InitDockPointer(){
            Items.Background=Brushes.Transparent;
            dockPointerLabel=new TextBlock{Foreground=Brushes.White,MaxWidth=180,TextTrimming=TextTrimming.CharacterEllipsis};
            dockPointerPreview=new Border{Child=dockPointerLabel,Padding=new Thickness(12,8,12,8),CornerRadius=new CornerRadius(12),Background=Polish.Color("#E52A3447"),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Collapsed};Root.Children.Add(dockPointerPreview);
            AddHandler(Mouse.PreviewMouseMoveEvent,new MouseEventHandler((s,e)=>{if(dockPointer==null)return;e.Handled=true;RouteDockPointer(()=>MoveDockPointer(e.GetPosition(this)));}),true);
            AddHandler(Mouse.PreviewMouseUpEvent,new MouseButtonEventHandler((s,e)=>{if(dockPointer==null||e.ChangedButton!=MouseButton.Left)return;e.Handled=true;RouteDockPointer(()=>FinishDockPointer(e.GetPosition(this)));}),true);
            AddHandler(Mouse.LostMouseCaptureEvent,new MouseEventHandler((s,e)=>{if(dockPointer!=null&&Mouse.Captured!=this)CancelDockPointer();}),true);
            PreviewKeyDown+=(s,e)=>{if(dockPointer!=null&&e.Key==Key.Escape){e.Handled=true;CancelDockPointer();}};
            dockPointerWatch.Tick+=(s,e)=>{if(dockPointer==null){dockPointerWatch.Stop();return;}if((GetAsyncKeyState(0x1B)&0x8000)!=0){CancelDockPointer();return;}if(Mouse.Captured!=this){CancelDockPointer();return;}if((GetAsyncKeyState(System.Windows.Forms.SystemInformation.MouseButtonsSwapped?0x02:0x01)&0x8000)==0)RouteDockPointer(()=>FinishDockPointer(Mouse.GetPosition(this)));};
            IsVisibleChanged+=(s,e)=>{if(!IsVisible)CancelDockPointer(false);};Closed+=(s,e)=>{CancelDockPointer(false);dockPointerWatch.Stop();};
        }
        internal void CheckPointerReorder(){
            var buttons=Items.Children.OfType<Button>().Where(b=>b.Tag is Entry).ToList();if(buttons.Count<3)throw new Exception("指针排序测试缺少可见图标");
            var first=(Entry)buttons.First().Tag;var last=(Entry)buttons.Last().Tag;Point start=buttons.Last().TranslatePoint(new Point(buttons.Last().ActualWidth/2,buttons.Last().ActualHeight/2),this),end=buttons.First().TranslatePoint(new Point(buttons.First().ActualWidth/2,buttons.First().ActualHeight/2),this);
            if(!StartDockPointer(buttons.Last(),last,start,null))throw new Exception("Dock 未捕获排序鼠标");MoveDockPointer(end);FinishDockPointer(end);UpdateLayout();if(DockOrderRules.Ordered(C.Prefs)[0].Path!=last.Path)throw new Exception("后向前松手未提交");
            buttons=Items.Children.OfType<Button>().Where(b=>b.Tag is Entry).ToList();start=buttons.First().TranslatePoint(new Point(buttons.First().ActualWidth/2,buttons.First().ActualHeight/2),this);end=buttons.Last().TranslatePoint(new Point(buttons.Last().ActualWidth/2,buttons.Last().ActualHeight/2),this);
            if(!StartDockPointer(buttons.First(),last,start,null))throw new Exception("第二次排序未捕获鼠标");MoveDockPointer(end);FinishDockPointer(end);UpdateLayout();if(DockOrderRules.Ordered(C.Prefs).Last().Path!=last.Path)throw new Exception("前向后松手未提交");
            buttons=Items.Children.OfType<Button>().Where(b=>b.Tag is Entry).ToList();string order=string.Join("|",C.Prefs.DockOrder);start=buttons.First().TranslatePoint(new Point(20,20),this);if(!StartDockPointer(buttons.First(),first,start,null))throw new Exception("取消测试未捕获鼠标");MoveDockPointer(new Point(start.X+30,start.Y));CancelDockPointer();if(string.Join("|",C.Prefs.DockOrder)!=order||Mouse.Captured==this||dockPointer!=null)throw new Exception("取消后排序或捕获未恢复");
        }
    }
}
