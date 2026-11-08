using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Diagnostics;
using System.Windows;using System.Windows.Controls;using System.Windows.Controls.Primitives;using System.Windows.Input;
using System.Windows.Media;using System.Windows.Media.Animation;using System.Windows.Threading;
namespace Lumina {
    public partial class Controller {
        public DockPinResult PinDockPaths(IEnumerable<string> paths,DockDropTarget target){
            var candidates=new List<Entry>();
            foreach(var raw in paths.Distinct(StringComparer.OrdinalIgnoreCase)){
                if(string.IsNullOrWhiteSpace(raw))continue;string path=EntryPathRules.Normalize(raw);
                var entry=Prefs.Apps.FirstOrDefault(a=>string.Equals(a.Path,path,StringComparison.OrdinalIgnoreCase));
                if(entry==null){if(!File.Exists(path)&&!Directory.Exists(path))continue;entry=new Entry{Name=EntryPathRules.Name(path),Path=path,IsFolder=Directory.Exists(path)};LaunchInfoService.Enrich(entry);}
                if(string.IsNullOrEmpty(entry.LaunchTarget)&&File.Exists(entry.Path))LaunchInfoService.Enrich(entry);candidates.Add(entry);
            }
            var before=BeginBoardEdit();var result=DockPinRules.Apply(Prefs,candidates,target);DockLayoutRules.SyncApps(Prefs);if(target!=null){var placementPaths=target.Before?result.Paths:result.Paths.AsEnumerable().Reverse();foreach(string key in placementPaths)DockLayoutRules.Move(Prefs,key,target.Path,target.Before);}EndBoardEdit(before,"拖入 Dock 固定");
            Dock.Refresh(false);Board.Render();Board.SetStatus(result.Paths.Count==0?"未添加入口":("已放入 Dock "+result.Paths.Count+" 个 · Ctrl+Z 撤销")+(result.Skipped>0?"；已达 20 个上限，跳过 "+result.Skipped+" 个":""));return result;
        }
    }
    public partial class DockWindow {
        sealed class TaskGroup {public string Key,Name;public Entry Known;public List<RunningWindow> Windows;}
        readonly DockWheelState taskWheel=new DockWheelState();readonly Stopwatch taskClock=Stopwatch.StartNew();
        readonly DispatcherTimer taskHintTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1800)};
        List<TaskGroup> taskGroups=new List<TaskGroup>();string taskSignature="";bool externalDockDrag,boardPinDragging;
        TextBlock taskHint;ContextMenu taskMenu;
        void InitTaskDock(){
            AllowDrop=true;
            taskHint=new TextBlock{FontSize=10,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(10,2,0,0),IsHitTestVisible=false,Visibility=Visibility.Collapsed};Root.Children.Add(taskHint);
            taskHintTimer.Tick+=(s,e)=>{taskHintTimer.Stop();taskHint.Visibility=Visibility.Collapsed;};
            AddHandler(Mouse.PreviewMouseWheelEvent,new MouseWheelEventHandler((s,e)=>{
                if(DockInputBusy||dragging||externalDockDrag)return;e.Handled=true;
                if(taskWheel.Scroll(e.Delta,taskClock.ElapsedMilliseconds))ApplyTaskMode();
            }),true);
            PreviewDragEnter+=DockExternalOver;PreviewDragOver+=DockExternalOver;
            PreviewDragLeave+=(s,e)=>{Point p=e.GetPosition(this);if(!InsideDock(p))ClearExternalDockDrag();};
            PreviewDrop+=(s,e)=>{
                if(ApplicationDropAt(e)) {ClearExternalDockDrag();return;}if(DockInputBusy)return;var paths=DockPayload(e.Data);if(paths.Length==0){ClearExternalDockDrag();return;}
                e.Handled=true;e.Effects=DragDropEffects.None;
                try{var target=ExternalDockTarget(e.GetPosition(Items).X);ClearExternalDockDrag();var result=C.PinDockPaths(paths,target);e.Effects=result.Paths.Count>0?DragDropEffects.Copy:DragDropEffects.None;ShowTaskHint(result.Paths.Count>0?"已放入 Dock"+(result.Skipped>0?" · 已达固定上限":""):"无法固定此入口");}
                catch(Exception ex){ClearExternalDockDrag();Store.Log(ex);ShowTaskHint("添加失败，请重试");}
            };
            IsVisibleChanged+=(s,e)=>{if(!IsVisible)ClearExternalDockDrag();};Closed+=(s,e)=>{taskHintTimer.Stop();if(taskMenu!=null)taskMenu.IsOpen=false;};
        }
        void ShowTaskHint(string text){taskHint.Text=text;taskHint.Foreground=MutedBrush;taskHint.Visibility=Visibility.Visible;taskHintTimer.Stop();taskHintTimer.Start();}
        void SetTaskMode(bool running){if(DockInputBusy||externalDockDrag)return;taskWheel.Set(running);ApplyTaskMode();}
        void ApplyTaskMode(){
            if(taskMenu!=null)taskMenu.IsOpen=false;
            if(taskWheel.Running){RunningWindows.Scan();ReadTaskGroups();}
            ResetDockHover();Refresh(false);ShowTaskHint(taskWheel.Running?"正在运行 · 上滚回常用":"常用软件 · 下滚看运行");
            if(MotionEnabled)Items.BeginAnimation(OpacityProperty,new DoubleAnimation(.35,1,TimeSpan.FromMilliseconds(160)){FillBehavior=FillBehavior.Stop});
        }
        void AddTaskDockMenu(ContextMenu menu){
            var common=new MenuItem{Header="常用软件 · 滚轮向上",IsCheckable=true};var running=new MenuItem{Header="正在运行 · 滚轮向下",IsCheckable=true};
            common.Click+=(s,e)=>SetTaskMode(false);running.Click+=(s,e)=>SetTaskMode(true);
            menu.Opened+=(s,e)=>{common.IsChecked=!taskWheel.Running;running.IsChecked=taskWheel.Running;};menu.Items.Add(common);menu.Items.Add(running);
        }
        static string TaskKey(RunningWindow window){
            // Hosted apps need a window identity, and an elevated process hides its image
            // path, so its windows are grouped by the name Toolhelp still reports.
            if(RunningWindows.IsFolderWindow(window))return "folder";
            string name=Path.GetFileName(window.Path??"");
            if(!string.IsNullOrEmpty(window.Path)&&!name.Equals("ApplicationFrameHost.exe",StringComparison.OrdinalIgnoreCase))return window.Path;
            name=window.ProcessName;
            return string.IsNullOrEmpty(name)||name.Equals("ApplicationFrameHost.exe",StringComparison.OrdinalIgnoreCase)?"window:"+window.Handle.ToInt64():"name:"+name;
        }
        void ReadTaskGroups(){
            taskGroups=RunningWindows.Snapshot().GroupBy(TaskKey,StringComparer.OrdinalIgnoreCase).OrderBy(g=>g.Key,StringComparer.OrdinalIgnoreCase).Select(g=>{
                var windows=g.OrderBy(w=>w.Handle.ToInt64()).ToList();string name=Path.GetFileNameWithoutExtension(windows[0].Path??"");if(g.Key.StartsWith("folder",StringComparison.OrdinalIgnoreCase))name="文件资源管理器";
                var known=KnownEntry(windows[0]);
                return new TaskGroup{Key=g.Key,Name=known!=null?known.Name:string.IsNullOrWhiteSpace(name)||(g.Key.StartsWith("window:")||g.Key.StartsWith("folder:"))?windows[0].Title:name,Windows=windows,Known=known};
            }).ToList();taskSignature=TaskSignature();
        }
        Entry KnownEntry(RunningWindow window){
            // Exact path first, then image name: launchers such as ...in\GameViewer.exe and
            // elevated processes both need the looser rule to find their Dock entry.
            return C.Prefs.Apps.FirstOrDefault(a=>!a.IsFolder&&RunningWindows.WindowMatch(a,window));
        }
        ImageSource TaskIcon(TaskGroup group){return group.Known!=null?FeatureUi.Icon(group.Known,false,false):RunningWindows.Icon(group.Windows[0]);}
        string TaskSignature(){return string.Join("|",RunningWindows.Snapshot().OrderBy(w=>w.Handle.ToInt64()).Select(w=>w.Handle.ToInt64()+":"+w.Path+":"+w.Title));}
        void RefreshTaskSnapshot(){if(thumbnails!=null)return;
            if((!taskWheel.Running&&!C.Prefs.Dock.ShowRunningSection)||DockInputBusy||externalDockDrag||dragging||C.Board.ModalBusy||(taskMenu!=null&&taskMenu.IsOpen)||(ContextMenu!=null&&ContextMenu.IsOpen)||Items.Children.OfType<Button>().Any(b=>b.IsMouseOver||(b.ContextMenu!=null&&b.ContextMenu.IsOpen)))return;
            if(taskSignature==TaskSignature())return;ReadTaskGroups();Refresh(false);
        }
        void BuildTaskButtons(int shown,int slots){
            var taskOrder=DockLayoutRules.Running(C.Prefs,TaskKeys);var ordered=taskGroups.OrderBy(g=>taskOrder.IndexOf("running:"+g.Key)).ToList();
            foreach(var group in ordered.Take(shown)){
                var selected=group;var button=DockButton(null,TaskIcon(group),()=>OpenTask(selected,null),group.Name+(group.Windows.Count>1?" · "+group.Windows.Count+" 个窗口":""));
                button.Tag=group;BindLayoutButton(button,"running:"+group.Key,group.Name,()=>OpenTask(selected,button));var content=button.Content as Grid;
                if(content!=null)content.Children.Add(new Border{Width=group.Windows.Count>1?12:5,Height=4,CornerRadius=new CornerRadius(2),Background=Polish.Accent,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false});
                button.ContextMenu=TaskWindowMenu(group);Items.Children.Add(button);
            }
            if(taskGroups.Count>shown&&slots>0){var more=DockButton("···",null,()=>{},"更多正在运行的软件");var menu=MenuTheme.Create(C.Prefs.Light);foreach(var group in ordered.Skip(shown)){var selected=group;var item=new MenuItem{Header=group.Name+(group.Windows.Count>1?"（"+group.Windows.Count+"）":"")};foreach(var window in group.Windows)item.Items.Add(TaskWindowItem(window));menu.Items.Add(item);}more.Click+=(s,e)=>OpenTaskMenu(menu,more);more.ContextMenu=menu;Items.Children.Add(more);}
            if(taskGroups.Count==0)Items.Children.Add(DockButton("—",null,()=>SetTaskMode(false),"暂无正在运行的软件 · 点击返回常用"));
        }
        MenuItem TaskWindowItem(RunningWindow window){var selected=window;var item=new MenuItem{Header=string.IsNullOrWhiteSpace(window.Title)?"未命名窗口":window.Title,ToolTip=window.Title};item.Click+=(s,e)=>ActivateTask(selected.Handle);return item;}
        ContextMenu TaskWindowMenu(TaskGroup group){var menu=MenuTheme.Create(C.Prefs.Light);foreach(var window in group.Windows)menu.Items.Add(TaskWindowItem(window));menu.Items.Add(new Separator());foreach(var window in group.Windows){var selected=window;var close=new MenuItem{Header=group.Windows.Count==1?"关闭窗口":"关闭 · "+window.Title};close.Click+=(s,e)=>{if(!RunningWindows.RequestClose(selected))ShowTaskHint(RunningWindows.CloseFailure);};menu.Items.Add(close);}AddDockControls(menu);return menu;}
        void OpenTask(TaskGroup group,FrameworkElement owner){
            // Refresh before acting: a launcher may replace its window after an update.
            RunningWindows.Scan();
            var current=RunningWindows.Snapshot().Where(w=>string.Equals(TaskKey(w),group.Key,StringComparison.OrdinalIgnoreCase)).ToList();
            if(current.Count==0){ReadTaskGroups();Refresh(false);ShowTaskHint("此窗口已关闭");return;}
            var known=C.Prefs.Apps.FirstOrDefault(a=>RunningWindows.For(a).Any(w=>current.Any(c=>c.Handle==w.Handle)));
            if(known!=null){C.LaunchDock(known,pointerHint);return;}
            var foreground=RunningWindows.ForegroundMatch(current);
            ToggleTask(foreground??current.FirstOrDefault(w=>!RunningWindows.Minimized(w.Handle))??current[0]);
        }

        void OpenTaskMenu(ContextMenu menu,UIElement owner){taskMenu=menu;menu.Closed+=(s,e)=>{if(taskMenu==menu)taskMenu=null;};menu.PlacementTarget=owner;menu.Placement=PlacementMode.Top;menu.IsOpen=true;}
        void ActivateTask(IntPtr handle){if(RunningWindows.ActivateHandle(handle))C.Board.CollapseSmooth();else{RunningWindows.Scan();ReadTaskGroups();Refresh(false);ShowTaskHint("此窗口已关闭");}}
        void ToggleTask(RunningWindow window){if(RunningWindows.ToggleWindow(window))C.Board.CollapseSmooth();else{RunningWindows.Scan();ReadTaskGroups();Refresh(false);ShowTaskHint("此窗口已关闭");}}
        static string[] DockPayload(IDataObject data){
            if(data.GetDataPresent("Lumina.Entries"))return data.GetData("Lumina.Entries") as string[]??new string[0];
            if(data.GetDataPresent("Lumina.Entry"))return new[]{data.GetData("Lumina.Entry") as string};
            return data.GetDataPresent(DataFormats.FileDrop)?data.GetData(DataFormats.FileDrop) as string[]??new string[0]:new string[0];
        }
        DockDropTarget ExternalDockTarget(double x){var buttons=Items.Children.OfType<Button>().Where(b=>ItemKey(b)!=null).ToList();return DockDropRules.OnGap(null,buttons.Select(ItemKey).ToArray(),buttons.Select(b=>b.TranslatePoint(new Point(b.ActualWidth/2,0),Items).X).ToArray(),x);}
        void DockExternalOver(object sender,DragEventArgs e){if(ApplicationDropAt(e)){ClearDockMarkers();return;}
            if(DockInputBusy||DockPayload(e.Data).Length==0){e.Effects=DragDropEffects.None;e.Handled=true;return;}
            e.Handled=true;e.Effects=DragDropEffects.Copy;
            if(!externalDockDrag){externalDockDrag=true;if(taskWheel.Running){taskWheel.Set(false);Refresh(false);UpdateLayout();}ShowTaskHint("松手固定到 Dock");}
            ShowDockDrop(ExternalDockTarget(e.GetPosition(Items).X));
        }
        public void BeginBoardPinDrag(){boardPinDragging=true;if(IsVisible)Topmost=true;}
        public void EndBoardPinDrag(){boardPinDragging=false;ClearExternalDockDrag();if(IsVisible)Topmost=InteractionRules.DockTopmost(C.Prefs.Dock,false);}
        internal void CheckTaskDock(){
            SetTaskMode(false);UpdateLayout();
            foreach(var button in Items.Children.OfType<Button>()){
                button.ApplyTemplate();var border=VisualTreeHelper.GetChild(button,0) as Border;var brush=border==null?null:border.Background as SolidColorBrush;
                if(brush==null||brush.Color.A==0)throw new Exception("Dock 整块点击区域缺少有效像素");
                foreach(var point in new[]{new Point(button.ActualWidth/2,button.ActualHeight/2),new Point(4,4),new Point(button.ActualWidth-4,button.ActualHeight-4)}){
                    var hit=button.InputHitTest(point) as DependencyObject;while(hit!=null&&hit!=button)hit=hit is Visual?VisualTreeHelper.GetParent(hit):LogicalTreeHelper.GetParent(hit);
                    if(hit!=button)throw new Exception("Dock 图标空白区域无法命中按钮");
                }
            }
            var wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=Mouse.PreviewMouseWheelEvent};RaiseEvent(wheel);
            if(!wheel.Handled||!taskWheel.Running)throw new Exception("Dock 滚轮未切换运行软件");
            SetTaskMode(false);
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        void ReleaseCancelledExternalDrag(){if(externalDockDrag&&(GetAsyncKeyState(System.Windows.Forms.SystemInformation.MouseButtonsSwapped?0x02:0x01)&0x8000)==0)ClearExternalDockDrag();}
        void ClearExternalDockDrag(){externalDockDrag=false;ClearDockMarkers();}
    }
}
