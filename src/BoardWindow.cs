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
    public partial class BoardWindow : GlassWindow {
        public bool ModalBusy;
        internal IntPtr foregroundHint;
        Controller C;TextBox Search;UniformGrid Tiles;TextBlock Pager,Status,Empty;Grid layout;int page;int capacity=35;int columns=7;Point dragStart;Entry dragEntry;bool editing;
        readonly HashSet<string> selected=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        WrapPanel bulkBar;RowDefinition bulkRow;TextBlock selectionCount;Button selectAll,clearSelection,deleteSelected,undoRemoval,editSelection,moveSelected;
        ContextMenu iconMenu;ComboBox groups,moveGroup;string currentGroup="全部";bool syncingGroups,iconSizeSync;Slider boardIconSlider;TextBlock iconSizeText;Button addExisting;Thumb moveGrip,resizeGrip;int visibilitySerial;WrapPanel folderBar,docBar;string docFilter="全部",docSignature="";bool foldersVisible {get{return !C.Prefs.BoardHideGroups&&!C.Prefs.BoardHideFolders;}}
        readonly DispatcherTimer idleRelease=new DispatcherTimer{Interval=TimeSpan.FromSeconds(20)};bool preparingView;string pendingStatus;
        public string CurrentGroup {get{return currentGroup;}}
        public void ClearSearch(){if(Search!=null)Search.Clear();}
        public BoardWindow(Controller c):base(42) {
            C=c;Title="Lumina 应用启动台";AllowDrop=true;InitDragExperience();
            IsVisibleChanged+=(s,e)=>{if(IsVisible)idleRelease.Stop();else if(layout!=null)idleRelease.Start();};idleRelease.Tick+=(s,e)=>{if(IsVisible){idleRelease.Stop();return;}if(ReleaseHiddenView())idleRelease.Stop();};Closed+=(s,e)=>{idleRelease.Stop();sizeRefresh.Stop();};
            PreviewKeyDown+=(s,e)=> {
                if(Search==null||groupDragRunning)return;
                if(e.Key==Key.Escape){EndDragPreview();CollapseSmooth();e.Handled=true;}
                else if(!Search.IsKeyboardFocusWithin&&(Keyboard.Modifiers&ModifierKeys.Control)!=0&&(e.Key==Key.Z||e.Key==Key.Y)){if(e.Key==Key.Y||(Keyboard.Modifiers&ModifierKeys.Shift)!=0)C.RedoBoard();else C.UndoBoard();e.Handled=true;}
                else if(editing&&!Search.IsKeyboardFocusWithin&&e.Key==Key.A&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){SelectFiltered();e.Handled=true;}
                else if(editing&&!Search.IsKeyboardFocusWithin&&e.Key==Key.Delete){C.RemoveMany(selected.ToArray());e.Handled=true;}
                else if((e.Key==Key.Left||e.Key==Key.Right)&&!(Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)&&Keyboard.Modifiers==ModifierKeys.None){ChangePage(e.Key==Key.Right?1:-1);e.Handled=true;}else if(e.Key==Key.PageDown){ChangePage(1);e.Handled=true;}else if(e.Key==Key.PageUp){ChangePage(-1);e.Handled=true;}
                else if(e.Key==Key.Enter && Search.IsKeyboardFocusWithin&&!editing){var a=Filtered().FirstOrDefault();if(a!=null)C.Launch(a);e.Handled=true;}
            };
            Drop+=ExternalDrop;
            PreviewMouseRightButtonUp+=BoardBlankMenu;
            Deactivated+=(s,e)=> { Dispatcher.BeginInvoke(new Action(()=> {if(C.Prefs.BoardDismissOnDeactivate&&IsVisible&&!IsActive&&!ModalBusy&&!activeDrag&&!IsContextMenuOpen()&&Mouse.LeftButton!=MouseButtonState.Pressed)CollapseSmooth();}));};
            moveGrip=new Thumb{Width=60,Height=8,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,8,0,0),Cursor=Cursors.SizeAll,ToolTip="拖动面板位置"};Root.Children.Add(moveGrip);
            resizeGrip=new Thumb{Width=20,Height=20,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,5,5),Cursor=Cursors.SizeNWSE,ToolTip="拖动调整面板大小"};Root.Children.Add(resizeGrip);
            moveGrip.Template=GripTemplate();resizeGrip.Template=GripTemplate(true);Closing+=(s,e)=>{if(!C.ShuttingDown){e.Cancel=true;CollapseSmooth();}};
            moveGrip.DragStarted+=(s,e)=>ModalBusy=true;moveGrip.DragDelta+=(s,e)=>{if(C.Prefs.Placement.PanelLocked)return;var area=SystemParameters.WorkArea;Left=FeatureRules.Position(Left+e.HorizontalChange,area.Left,area.Width,Width);Top=FeatureRules.Position(Top+e.VerticalChange,area.Top,area.Height,Height);};
            InitResizeRefresh();
            resizeGrip.DragStarted+=(s,e)=>{ModalBusy=true;resizingPanel=true;sizeRefresh.Stop();};resizeGrip.DragDelta+=(s,e)=>{if(C.Prefs.Placement.PanelLocked)return;var area=SystemParameters.WorkArea;Width=Math.Max(Math.Min(860,area.Width-32),Math.Min(area.Width-32,Width+e.HorizontalChange));Height=Math.Max(Math.Min(560,area.Height-32),Math.Min(area.Height-32,Height+e.VerticalChange));Left=FeatureRules.Position(Left,area.Left,area.Width,Width);Top=FeatureRules.Position(Top,area.Top,area.Height,Height);};
            moveGrip.DragCompleted+=(s,e)=>{try{SavePlacement();Backdrop(C.Prefs.Light);}finally{ModalBusy=false;}};resizeGrip.DragCompleted+=(s,e)=>{try{resizingPanel=false;SavePlacement();QueueSizeRefresh();Backdrop(C.Prefs.Light);}finally{ModalBusy=false;}};
        }
        void SavePlacement(){var p=C.Prefs.Placement;p.PanelLeft=Left;p.PanelTop=Top;p.PanelWidth=Width;p.PanelHeight=Height;C.Save();}
        void BuildForSize(){
            string query=C.Prefs.BoardHideSearch||Search==null?"":Search.Text;
            if(C.Prefs.BoardHideGroups)currentGroup="全部";
            string key=ViewStructureKey();bool changed=layout==null||viewStructureKey!=key;
            if(changed){columns=BoardIconRules.Columns(Width-104,C.Prefs.BoardIconSize,C.Prefs.BoardIconGap);int rows=BoardIconRules.Rows(Height-(C.Prefs.BoardHideGroups?180:258),C.Prefs.BoardIconSize,C.Prefs.BoardIconGap);capacity=columns*rows;Build(rows,query);viewStructureKey=key;}
            else{SyncGroups();if(viewRenderDirty||viewDataKey!=ViewDataKey())Render(false);}
        }
        public void RefreshIcons(){if(boardIconSlider!=null){iconSizeSync=true;try{boardIconSlider.Value=C.Prefs.BoardIconSize;iconSizeText.Text="图标 "+Math.Round(C.Prefs.BoardIconSize)+" px";}finally{iconSizeSync=false;}}iconsDirty=true;QueueSizeRefresh();}
        void IconSettings(){var menu=MenuTheme.Create(C.Prefs.Light);iconMenu=menu;menu.Closed+=(s,e)=>{if(iconMenu==menu)iconMenu=null;};Action<string,double,double,double,Action<double>> add=(name,min,max,value,change)=>{var box=new StackPanel{Width=230};var label=new TextBlock{Text=name+"："+Math.Round(value)+" px",Foreground=TextBrush,Margin=new Thickness(0,0,0,8)};box.Children.Add(label);var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=1,IsSnapToTickEnabled=true,Style=Resources[typeof(Slider)] as Style};box.Children.Add(slider);slider.ValueChanged+=(s,e)=>{label.Text=name+"："+Math.Round(slider.Value)+" px";change(slider.Value);RefreshIcons();C.Save();};menu.Items.Add(new MenuItem{Header=box,StaysOpenOnClick=true});};add("图标大小",28,80,C.Prefs.BoardIconSize,v=>C.Prefs.BoardIconSize=v);add("图标间距",0,20,C.Prefs.BoardIconGap,v=>C.Prefs.BoardIconGap=v);var reset=new MenuItem{Header="恢复默认"};reset.Click+=(s,e)=>{C.Prefs.BoardIconSize=48;C.Prefs.BoardIconGap=5;RefreshIcons();C.Save();};menu.Items.Add(reset);menu.PlacementTarget=this;menu.IsOpen=true;}

        public void CollapseSmooth(){StopOpeningFrame();EndDragPreview();if(!IsVisible)return;int serial=++visibilitySerial;if(!MotionEnabled){Hide();return;}var animation=new DoubleAnimation(0,TimeSpan.FromMilliseconds(110));animation.Completed+=(s,e)=>{if(serial==visibilitySerial)Hide();};ContentGrid.BeginAnimation(OpacityProperty,animation);}
        internal bool ReleaseHiddenView(){if(IsVisible||preparingView||ModalBusy||activeDrag||IsMouseCaptureWithin)return false;sizeRefresh.Stop();StopOpeningFrame();viewStructureKey=viewDataKey=null;viewRenderDirty=true;if(iconMenu!=null)iconMenu.IsOpen=false;iconMenu=null;EndDragPreview();ContentGrid.Children.Clear();ReleaseBackdrop();Search=null;Tiles=null;Pager=Status=Empty=null;layout=null;bulkBar=folderBar=null;bulkRow=null;selectionCount=iconSizeText=null;selectAll=clearSelection=deleteSelected=undoRemoval=editSelection=moveSelected=addExisting=null;groups=moveGroup=null;boardIconSlider=null;dragEntry=null;return true;}
        internal bool HasView{get{return layout!=null;}}
        bool IsContextMenuOpen() {return (iconMenu!=null&&iconMenu.IsOpen)||(Tiles!=null && Tiles.Children.OfType<FrameworkElement>().Any(x=>x.ContextMenu!=null&&x.ContextMenu.IsOpen));}
        public void Open() {
            foregroundHint=RunningWindows.CaptureForegroundHint();idleRelease.Stop();EndDragPreview();preparingView=true;try{
            visibilitySerial++;
            var area=SystemParameters.WorkArea;
            double wantedW=C.Prefs.PanelSize==0?980:1280,wantedH=C.Prefs.PanelSize==0?620:730;
            if(C.Prefs.PanelSize==2){wantedW=area.Width-32;wantedH=area.Height-C.Dock.Height-40;}
            var placement=C.Prefs.Placement;Width=Math.Min(placement.PanelWidth??wantedW,area.Width-32);Height=Math.Min(placement.PanelHeight??wantedH,area.Height-32);
            Height=Math.Max(Math.Min(400,area.Height-130),Height);
            Width=Math.Max(Math.Min(860,area.Width-32),Width);Height=Math.Max(Math.Min(560,area.Height-32),Height);
            Left=FeatureRules.Position(placement.PanelLeft??(area.Left+(area.Width-Width)/2),area.Left,area.Width,Width);Top=FeatureRules.Position(placement.PanelTop??(area.Top+(area.Height-C.Dock.Height-Height)/2),area.Top,area.Height,Height);
            moveGrip.Visibility=resizeGrip.Visibility=placement.PanelLocked?Visibility.Collapsed:Visibility.Visible;
            viewRenderDirty|=editing||selected.Count>0;editing=false;selected.Clear();Topmost=C.Prefs.BoardAlwaysOnTop;RefreshLayer();LightTheme=C.Prefs.Light;MotionEnabled=C.Prefs.Dock.OpenMotion;MotionLevel=C.Prefs.Dock.MotionLevel;OpenStyle=C.Prefs.Dock.OpenAnimationStyle;ConfigureGlass(C.Prefs.Dock,C.Prefs.BoardMaterial);GlassPercent=C.Prefs.OpacityPercent??72;Polish.Apply(this,LightTheme);BuildForSize();Backdrop(C.Prefs.Light);PrepareOpeningFrame();Show();Activate();Native.SetForegroundWindow(new WindowInteropHelper(this).Handle);ScheduleOpeningFrame();if(!C.Prefs.BoardHideSearch)Search.Focus();else Focus();
            }finally{preparingView=false;}
        }
        void Build(int rows,string query="") {
            ContentGrid.Children.Clear();
            layout=new Grid{Margin=new Thickness(38,22,38,20)};
            layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(55)});
            layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            bulkRow=new RowDefinition{Height=new GridLength(0)};layout.RowDefinitions.Add(bulkRow);
            layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
            layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(45)});
            layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(32)});ContentGrid.Children.Add(layout);
            var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(116)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(80)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(44)});layout.Children.Add(header);
            var searchHost=new Border {CornerRadius=new CornerRadius(18),BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromArgb(90,255,255,255)),Background=new SolidColorBrush(Color.FromArgb(25,255,255,255)),Margin=new Thickness(10,0,18,9)};
            var searchGrid=new Grid();searchHost.Child=searchGrid;
            var hint=new TextBlock{Text="搜索应用、文件夹…",Foreground=MutedBrush,Margin=new Thickness(18,0,0,0),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};searchGrid.Children.Add(hint);
            Search=new TextBox{Text=query,FontSize=15,Foreground=TextBrush,Background=Brushes.Transparent,BorderThickness=new Thickness(0),CaretBrush=TextBrush,VerticalContentAlignment=VerticalAlignment.Center,Padding=new Thickness(18,0,18,0)};
            hint.Visibility=string.IsNullOrEmpty(query)?Visibility.Visible:Visibility.Hidden;
            Search.TextChanged+=(s,e)=>{hint.Visibility=Search.Text.Length==0?Visibility.Visible:Visibility.Hidden;page=0;Render(true);};
            searchGrid.Children.Add(Search);header.Children.Add(searchHost);searchHost.Visibility=C.Prefs.BoardHideSearch?Visibility.Collapsed:Visibility.Visible;
            if(C.Prefs.BoardHideSearch)header.Children.Add(new TextBlock{Text=C.Prefs.BoardHideGroups?"全部应用":currentGroup,Foreground=TextBrush,FontSize=18,Margin=new Thickness(12,0,0,8),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false});
            var add=Button("+ 批量添加",(s,e)=>C.Choose(),106);Grid.SetColumn(add,1);header.Children.Add(add);
            editSelection=Button(editing?"完成":"多选",null,68);editSelection.ToolTip="跨页勾选，一次移除多个入口";editSelection.Click+=(s,e)=>{SetSelectionMode(!editing);Search.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));Render();};Grid.SetColumn(editSelection,2);header.Children.Add(editSelection);
            var settings=Button("⚙",(s,e)=>C.Settings(),30);settings.Content=Polish.Icon("settings",TextBrush,18);settings.ToolTip="外观、组件与快捷键";Grid.SetColumn(settings,3);header.Children.Add(settings);
            var display=Button("···",(s,e)=>MoreMenu(),32);display.ToolTip="更多与显示选项";Grid.SetColumn(display,4);header.Children.Add(display);
            var close=Button("×",(s,e)=>CollapseSmooth(),32);Grid.SetColumn(close,5);header.Children.Add(close);
            var groupBar=new WrapPanel{VerticalAlignment=VerticalAlignment.Center,Visibility=C.Prefs.BoardHideGroups?Visibility.Collapsed:Visibility.Visible};Grid.SetRow(groupBar,1);layout.Children.Add(groupBar);
            groups=new ComboBox{Width=145,Height=30,Margin=new Thickness(8,0,10,0)};groupBar.Children.Add(groups);groups.SelectionChanged+=(s,e)=>{if(syncingGroups)return;string nextGroup=groups.SelectedItem as string??"全部";if(nextGroup!=currentGroup){selected.Clear();docFilter="全部";}currentGroup=nextGroup;if(addExisting!=null)addExisting.IsEnabled=currentGroup!="全部";page=0;Render(true);};
            var density=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6,0,8,0)};iconSizeText=new TextBlock{Text="图标 "+Math.Round(C.Prefs.BoardIconSize)+" px",Foreground=TextBrush,VerticalAlignment=VerticalAlignment.Center,Width=75};density.Children.Add(iconSizeText);boardIconSlider=new Slider{Minimum=28,Maximum=80,Value=C.Prefs.BoardIconSize,Width=110,TickFrequency=2,IsSnapToTickEnabled=true,ToolTip="缩小图标，让每页容纳更多入口"};boardIconSlider.ValueChanged+=(s,e)=>{if(iconSizeSync)return;C.Prefs.BoardIconSize=boardIconSlider.Value;iconSizeText.Text="图标 "+Math.Round(boardIconSlider.Value)+" px";RefreshIcons();C.Save();};density.Children.Add(boardIconSlider);groupBar.Children.Add(density);addExisting=Button("添加已有图标",(s,e)=>C.AddExistingToGroup(currentGroup),112);groupBar.Children.Add(addExisting);groupBar.Children.Add(Button("添加文件夹",(s,e)=>C.AddFolder(),100));groupBar.Children.Add(Button("新建",(s,e)=>NewFileMenu(),60));groupBar.Children.Add(Button("更多",(s,e)=>MoreMenu(),68));
            bulkBar=new WrapPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};Grid.SetRow(bulkBar,2);layout.Children.Add(bulkBar);
            selectAll=Button("全选结果",(s,e)=>SelectFiltered(),90);selectAll.ToolTip="选中当前搜索结果的所有页；已有勾选会保留";bulkBar.Children.Add(selectAll);
            clearSelection=Button("取消全选",(s,e)=>{selected.Clear();Render();},90);bulkBar.Children.Add(clearSelection);
            deleteSelected=Button("移除已选",(s,e)=>C.RemoveMany(selected.ToArray()),112);deleteSelected.Foreground=new SolidColorBrush(Color.FromRgb(255,155,155));bulkBar.Children.Add(deleteSelected);
            undoRemoval=Button("撤销",(s,e)=>C.UndoBoard(),72);groupBar.Children.Add(undoRemoval);
            selectionCount=new TextBlock{Foreground=MutedBrush,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,0,0)};bulkBar.Children.Add(selectionCount);
            moveGroup=new ComboBox{Width=112,Height=30,Margin=new Thickness(12,0,4,0),VerticalAlignment=VerticalAlignment.Center};bulkBar.Children.Add(moveGroup);moveSelected=Button("移动",(s,e)=>{string group=moveGroup.SelectedItem as string;if(group==null||selected.Count==0)return;C.TransferEntries(selected.ToArray(),currentGroup,group);},100);bulkBar.Children.Add(moveSelected);SyncGroups();
            var content=new DockPanel{Margin=new Thickness(0,10,0,4)};Grid.SetRow(content,3);layout.Children.Add(content);
            folderSignature="";folderBar=new WrapPanel{Margin=new Thickness(0,0,0,8),Visibility=foldersVisible?Visibility.Visible:Visibility.Collapsed};DockPanel.SetDock(folderBar,Dock.Top);content.Children.Add(folderBar);
            docSignature="";docBar=new WrapPanel{Margin=new Thickness(0,0,0,8),Visibility=Visibility.Collapsed};DockPanel.SetDock(docBar,Dock.Top);content.Children.Add(docBar);
            var tileHost=new Grid();content.Children.Add(tileHost);
            Tiles=new UniformGrid{Columns=columns,Rows=rows,ClipToBounds=true,Background=Brushes.Transparent};tileHost.Children.Add(Tiles);Tiles.SizeChanged+=(s,e)=>QueueSizeRefresh();
            Empty=new TextBlock{Text="将软件、文档或文件夹拖到这里\n面板默认保持打开；按 Esc 收起，也可点击「批量添加」",TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Foreground=MutedBrush,FontSize=19,LineHeight=32};tileHost.Children.Add(Empty);
            var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};Grid.SetRow(footer,4);layout.Children.Add(footer);
            footer.Children.Add(Button("‹",(s,e)=>ChangePage(-1),40));Pager=new TextBlock{Width=100,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Foreground=TextBrush,FontSize=14};footer.Children.Add(Pager);footer.Children.Add(Button("›",(s,e)=>ChangePage(1),40));
            var bottom=new Grid();bottom.ColumnDefinitions.Add(new ColumnDefinition());bottom.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(230)});Grid.SetRow(bottom,5);layout.Children.Add(bottom);
            Status=new TextBlock{Text="图标可拖到 Dock 固定 · Esc 收起 · ··· 显示选项",Foreground=MutedBrush,FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};var statusColumn=new StackPanel{VerticalAlignment=VerticalAlignment.Center};statusColumn.Children.Add(Status);bottom.Children.Add(statusColumn);
            var transparencyBox=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetColumn(transparencyBox,1);bottom.Children.Add(transparencyBox);
            var amount=new TextBlock{Text="透明度 "+Math.Round(100-GlassPercent)+"%",Foreground=MutedBrush,Width=83,VerticalAlignment=VerticalAlignment.Center};transparencyBox.Children.Add(amount);
            var transparency=new Slider{Minimum=0,Maximum=100,Value=100-GlassPercent,Width=140,TickFrequency=5,IsSnapToTickEnabled=true,ToolTip="只改变玻璃背景，图标和文字保持清晰"};transparencyBox.Children.Add(transparency);
            transparency.ValueChanged+=(s,e)=>{C.Opacity(100-transparency.Value);amount.Text="透明度 "+Math.Round(transparency.Value)+"%";C.Save();};
            Render(true);
        }
        public void SyncGroups(){if(addExisting!=null)addExisting.IsEnabled=currentGroup!="全部";if(groups==null)return;syncingGroups=true;var names=new string[]{"全部","未分组"}.Concat(C.Prefs.Groups).ToList();groups.ItemsSource=names;if(!names.Contains(currentGroup)){currentGroup="全部";selected.Clear();page=0;}groups.SelectedItem=currentGroup;if(moveGroup!=null){string name=moveGroup.SelectedItem as string;moveGroup.ItemsSource=names.Skip(1).ToList();moveGroup.SelectedItem=names.Contains(name)?name:"未分组";}syncingGroups=false;if(addExisting!=null)addExisting.IsEnabled=currentGroup!="全部";}
        List<Entry> Filtered() {string q=C.Prefs.BoardHideSearch||Search==null?"":Search.Text.Trim();var list=C.Prefs.Apps.Where(a=>(C.Prefs.BoardHideGroups||currentGroup=="全部"||FeatureRules.InGroup(a,currentGroup))&&LayoutRules.Matches(a.Name,q)).ToList();if(!C.Prefs.BoardHideGroups&&docFilter!="全部"&&string.Equals(currentGroup,C.Prefs.DesktopDocsGroup,StringComparison.OrdinalIgnoreCase))list=list.Where(a=>DesktopAutoRules.DocKind(a.Path)==docFilter).ToList();if(C.Prefs.BoardSortRecent&&list.Count>1)list=list.OrderByDescending(a=>RecentRules.Stamp(C.Prefs,a.Path)).ThenBy(a=>a.Name,StringComparer.CurrentCultureIgnoreCase).ToList();return list;}
        public void SetStatus(string text) {pendingStatus=text;if(Status!=null)Status.Text=text;}
        void SetSelectionMode(bool enabled) {editing=enabled;selected.Clear();dragEntry=null;if(editSelection!=null)editSelection.Content=enabled?"完成":"多选";}
        public void FinishSelection() {SetSelectionMode(false);}
        void SelectFiltered() {foreach(var a in Filtered())selected.Add(a.Path);Render();}
        void UpdateSelectionBar() {
            selected.RemoveWhere(p=>!C.Prefs.Apps.Any(a=>string.Equals(a.Path,p,StringComparison.OrdinalIgnoreCase)));
            bool visible=editing;bulkBar.Visibility=visible?Visibility.Visible:Visibility.Collapsed;bulkRow.Height=visible?GridLength.Auto:new GridLength(0);moveGroup.IsEnabled=editing;moveSelected.Content=currentGroup=="全部"?"添加到分组":"移动";moveSelected.IsEnabled=editing&&selected.Count>0;
            selectAll.IsEnabled=editing;clearSelection.IsEnabled=editing&&selected.Count>0;deleteSelected.IsEnabled=editing&&selected.Count>0;
            deleteSelected.Content="移除已选（"+selected.Count+"）";undoRemoval.IsEnabled=C.CanUndoRemoval;undoRemoval.ToolTip="撤销："+C.UndoLabel+" · Ctrl+Z";undoRemoval.Visibility=C.CanUndoRemoval?Visibility.Visible:Visibility.Collapsed;selectionCount.Text=editing?"已选 "+selected.Count+" · 跨页保留":"可撤销最近操作";
        }
        public void Render(bool animate=false) {
            if(Tiles==null||(!IsVisible&&!preparingView)){viewRenderDirty=true;return;}viewRenderDirty=false;viewDataKey=ViewDataKey();UpdateSelectionBar();RefreshFolders();RefreshDocBar();Tiles.Children.Clear();var entries=Filtered();int total=LayoutRules.Pages(entries.Count,capacity);page=Math.Min(page,total-1);Pager.Text=(page+1)+" / "+total;Empty.Visibility=entries.Count==0?Visibility.Visible:Visibility.Collapsed;if(pendingStatus!=null)Status.Text=pendingStatus;
            int index=0;foreach(var a in entries.Skip(page*capacity).Take(capacity)) Tiles.Children.Add(Tile(a,index++,animate));
        }
        void ChangePage(int delta) {var entries=Filtered();int total=LayoutRules.Pages(entries.Count,capacity);int next=Math.Max(0,Math.Min(total-1,page+delta));if(next==page)return;page=next;Render();AnimatePage(delta);}
        FrameworkElement Tile(Entry a,int index,bool animate) {
            bool isSelected=editing&&selected.Contains(a.Path);
            var selectedBrush=new SolidColorBrush(Color.FromArgb(48,111,167,255));
            var tile=new Border{Tag=a,Background=isSelected?selectedBrush:Brushes.Transparent,BorderThickness=new Thickness(1),BorderBrush=isSelected?new SolidColorBrush(Color.FromArgb(150,146,187,255)):Brushes.Transparent,CornerRadius=new CornerRadius(18),Margin=new Thickness(C.Prefs.BoardIconGap),Padding=new Thickness(4),Cursor=Cursors.Hand,ToolTip=a.Name,ContextMenu=editing?null:new ContextMenu(),AllowDrop=!editing};
            tile.ContextMenuOpening+=(sender,eventArgs)=>{if(editing){eventArgs.Handled=true;return;}eventArgs.Handled=true;var menu=C.Menu(a,currentGroup);tile.ContextMenu=menu;iconMenu=menu;menu.Closed+=(ss,ee)=>{if(iconMenu==menu)iconMenu=null;};menu.PlacementTarget=tile;menu.IsOpen=true;};
            var container=new Grid();tile.Child=container;var box=new StackPanel{VerticalAlignment=VerticalAlignment.Center};container.Children.Add(box);
            var icon=FeatureUi.Icon(a,C.Prefs.Dock.ShowShortcutOverlays,C.Prefs.Dock.SharperIcons);
            if(icon!=null)box.Children.Add(new Image{Source=icon,Width=C.Prefs.BoardIconSize,Height=C.Prefs.BoardIconSize,Margin=new Thickness(0,0,0,8)});
            else box.Children.Add(new TextBlock{Text="▣",Foreground=TextBrush,FontSize=C.Prefs.BoardIconSize*.84,Height=C.Prefs.BoardIconSize,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,8)});
            box.Children.Add(new TextBlock{Text=a.Name,Foreground=TextBrush,FontSize=12,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=Math.Max(20,(Tiles.ActualWidth>0?Tiles.ActualWidth:Width-104)/columns-2*C.Prefs.BoardIconGap-10)});
            container.Children.Add(RunningBadge.Create());
            if(editing) {
                var check=new CheckBox{IsChecked=isSelected,IsHitTestVisible=false,Focusable=false,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(2)};container.Children.Add(check);
            }
            var scale=new ScaleTransform(1,1);var lift=new TranslateTransform();var transforms=new TransformGroup();transforms.Children.Add(scale);transforms.Children.Add(lift);var iconVisual=box.Children[0] as FrameworkElement;iconVisual.RenderTransform=transforms;iconVisual.RenderTransformOrigin=new Point(.5,.5);
            Action<bool> hover=over=>{tile.Background=isSelected?selectedBrush:over?new SolidColorBrush(Color.FromArgb(24,255,255,255)):Brushes.Transparent;if(!C.Prefs.Dock.HoverMotion)return;var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(over&&InteractionRules.ZoomHover(C.Prefs.Dock.HoverAnimationStyle)?1+.05*MotionLevel:1,TimeSpan.FromMilliseconds(170)){EasingFunction=ease});scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(over&&InteractionRules.ZoomHover(C.Prefs.Dock.HoverAnimationStyle)?1+.05*MotionLevel:1,TimeSpan.FromMilliseconds(170)){EasingFunction=ease});lift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(over&&InteractionRules.LiftHover(C.Prefs.Dock.HoverAnimationStyle)?-3*MotionLevel:0,TimeSpan.FromMilliseconds(170)){EasingFunction=ease});};
            tile.MouseEnter+=(s,e)=>hover(true);tile.MouseLeave+=(s,e)=>hover(false);
            tile.MouseLeftButtonDown+=(s,e)=>{dragStart=e.GetPosition(this);dragEntry=a;tile.CaptureMouse();};
            tile.MouseMove+=(s,e)=> {if(e.LeftButton!=MouseButtonState.Pressed||dragEntry!=a)return;var p=e.GetPosition(this);if(Math.Abs(p.X-dragStart.X)+Math.Abs(p.Y-dragStart.Y)>10) {dragEntry=null;tile.ReleaseMouseCapture();StartEntryDrag(tile,a);}};
            tile.MouseLeftButtonUp+=(s,e)=> {tile.ReleaseMouseCapture();if(dragEntry==a){dragEntry=null;if(editing){if(!selected.Remove(a.Path))selected.Add(a.Path);Render();e.Handled=true;}else C.Launch(a);}};
            tile.Drop+=(s,e)=> {
                if(!e.Data.GetDataPresent("Lumina.Entry"))return;
                string path=(string)e.Data.GetData("Lumina.Entry");C.ReorderEntry(path,a);e.Handled=true;
            };return tile;
        }
        string[] DraggedEntries(IDataObject data){if(data.GetDataPresent("Lumina.Entries"))return (string[])data.GetData("Lumina.Entries");if(data.GetDataPresent("Lumina.Entry"))return new[]{(string)data.GetData("Lumina.Entry")};return new string[0];}
        void OpenGroup(string name){FinishSelection();currentGroup=name;docFilter="全部";page=0;ClearSearch();SyncGroups();Render(true);}
        void RefreshFolders(){if(folderBar==null||groupDragRunning)return;if(activeDrag&&folderBar.Children.Count>0){if(folderBar.Children.OfType<Border>().Select(b=>b.Tag as string).SequenceEqual(new[]{"全部","未分组"}.Concat(C.Prefs.Groups)))return;EndDragPreview();}string signature=currentGroup+"/"+LightTheme+"/"+string.Join("|",new[]{"全部","未分组"}.Concat(C.Prefs.Groups).Select(g=>g+":"+C.Prefs.Apps.Count(a=>FeatureRules.InGroup(a,g))));if(folderSignature==signature&&folderBar.Children.Count>0)return;folderSignature=signature;folderBar.Children.Clear();foreach(string item in new[]{"全部","未分组"}.Concat(C.Prefs.Groups)){string name=item;int count=name=="全部"?C.Prefs.Apps.Count:C.Prefs.Apps.Count(a=>FeatureRules.InGroup(a,name));var normal=name==currentGroup?new SolidColorBrush(Color.FromArgb(55,130,180,255)):new SolidColorBrush(Color.FromArgb(22,255,255,255));var box=new Border{Tag=name,CornerRadius=new CornerRadius(12),Background=normal,Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,0,6,6),Cursor=Cursors.Hand,AllowDrop=true,ToolTip=name=="全部"?"查看全部入口":"点击打开；将一个或多个入口拖到这里分组"};var label=new StackPanel{Orientation=Orientation.Horizontal};var folderIcon=Polish.Icon(name=="全部"?"grid":"folder",TextBrush,18);folderIcon.Margin=new Thickness(0,0,6,0);label.Children.Add(folderIcon);label.Children.Add(new TextBlock{Text=name+"  "+count,Foreground=TextBrush,FontSize=12,VerticalAlignment=VerticalAlignment.Center});box.Child=label;AttachGroupReorder(box,name);box.DragEnter+=(sender,e)=>{if(DraggedEntries(e.Data).Length>0||e.Data.GetDataPresent(DataFormats.FileDrop)){BeginGroupHover(name);box.Background=new SolidColorBrush(Color.FromArgb(90,100,160,240));e.Effects=GroupDragEffect(e.Data,name);e.Handled=true;}};box.DragOver+=(sender,e)=>{if(DraggedEntries(e.Data).Length>0||e.Data.GetDataPresent(DataFormats.FileDrop))BeginGroupHover(name);e.Effects=GroupDragEffect(e.Data,name);e.Handled=true;};box.DragLeave+=(sender,e)=>{StopGroupHover(name);box.Background=normal;};box.Drop+=(sender,e)=>{e.Handled=true;if(name=="全部")return;var paths=DraggedEntries(e.Data);if(paths.Length>0){string from=e.Data.GetDataPresent("Lumina.SourceGroup")?(string)e.Data.GetData("Lumina.SourceGroup"):currentGroup;C.TransferEntries(paths,from,name);}else if(e.Data.GetDataPresent(DataFormats.FileDrop)){C.Add((string[])e.Data.GetData(DataFormats.FileDrop),true,name);OpenGroup(name);SetStatus("已添加到「"+name+"」");}};folderBar.Children.Add(box);}}
        void RefreshDocBar(){
            if(docBar==null)return;
            bool visible=!C.Prefs.BoardHideGroups&&string.Equals(currentGroup,C.Prefs.DesktopDocsGroup,StringComparison.OrdinalIgnoreCase);
            docBar.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
            if(!visible)return;
            if(docFilter!="全部"&&Array.IndexOf(DesktopAutoRules.DocKinds,docFilter)<0)docFilter="全部";
            string signature=currentGroup+"/"+docFilter;
            foreach(string probe in new[]{"全部"}.Concat(DesktopAutoRules.DocKinds))signature+="|"+probe+":"+DocCount(probe);
            if(docSignature==signature&&docBar.Children.Count>0)return;
            docSignature=signature;docBar.Children.Clear();
            foreach(string kind in new[]{"全部"}.Concat(DesktopAutoRules.DocKinds)){
                string name=kind;int count=DocCount(name);bool active=docFilter==name;
                var box=new Border{Tag=name,CornerRadius=new CornerRadius(12),Background=active?new SolidColorBrush(Color.FromArgb(55,130,180,255)):new SolidColorBrush(Color.FromArgb(22,255,255,255)),Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,0,6,6),Cursor=Cursors.Hand,ToolTip=name=="全部"?"显示该分组的全部文档":"只看"+name+"类型的文档"};
                box.Child=new TextBlock{Text=name+"  "+count,Foreground=TextBrush,FontSize=12,VerticalAlignment=VerticalAlignment.Center};
                box.MouseLeftButtonUp+=(s,e)=>{docFilter=name;page=0;Render(true);e.Handled=true;};
                docBar.Children.Add(box);
            }
        }
        int DocCount(string kind){string group=C.Prefs.DesktopDocsGroup;return C.Prefs.Apps.Count(a=>FeatureRules.InGroup(a,group)&&(kind=="全部"||DesktopAutoRules.DocKind(a.Path)==kind));}
        void ExternalDrop(object s,DragEventArgs e) {if(DraggedEntries(e.Data).Length>0){e.Handled=true;e.Effects=DragDropEffects.None;return;}if(e.Data.GetDataPresent(DataFormats.FileDrop)) {C.Add((string[])e.Data.GetData(DataFormats.FileDrop),true,currentGroup);ClearSearch();e.Handled=true;}}
        public void UpdateRunning(){if(Tiles==null||!IsVisible)return;foreach(var tile in Tiles.Children.OfType<Border>()){var entry=tile.Tag as Entry;var grid=tile.Child as Grid;if(entry==null||grid==null)continue;int count=C.Prefs.Dock.ShowRunning?RunningWindows.Count(entry):0;foreach(var marker in grid.Children.OfType<Border>().Where(b=>b.Tag as string=="running"))RunningBadge.Apply(marker,count);}}
    }
}
