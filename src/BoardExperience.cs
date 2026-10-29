using System;using System.Runtime.InteropServices;using System.Linq;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Input;using System.Windows.Threading;
namespace GlassBoard {
    public partial class BoardWindow {
        Border dragPreview;Image dragPreviewIcon;TextBlock dragPreviewText;bool activeDrag;string hoverGroup,folderSignature="";readonly DispatcherTimer hoverTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(700)};
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        readonly DispatcherTimer dragWatch=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};
        bool entryDragRunning;
        internal void ClearDragState(){EndDragPreview();}
        void InitDragExperience(){var body=new StackPanel{Orientation=Orientation.Horizontal};dragPreviewIcon=new Image{Width=28,Height=28,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,10,0)};dragPreviewText=new TextBlock{Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center,MaxWidth=230,TextTrimming=TextTrimming.CharacterEllipsis};body.Children.Add(dragPreviewIcon);body.Children.Add(dragPreviewText);dragPreview=new Border{Child=body,Padding=new Thickness(12,9,12,9),CornerRadius=new CornerRadius(14),Background=Polish.Color("#E52A3447"),BorderBrush=Polish.Color("#80B8CEEF"),BorderThickness=new Thickness(1),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Collapsed};InitDragGhostMotion();dragWatch.Tick+=(s,e)=>{RecoverExternalDrag((GetAsyncKeyState(1)&0x8000)!=0||(GetAsyncKeyState(2)&0x8000)!=0||(GetAsyncKeyState(4)&0x8000)!=0);};IsVisibleChanged+=(s,e)=>{if(!IsVisible)EndDragPreview();};PreviewMouseDown+=(s,e)=>{if(!entryDragRunning&&activeDrag){EndDragPreview();Render();}};hoverTimer.Tick+=(s,e)=>{hoverTimer.Stop();if(activeDrag&&hoverGroup!=null&&hoverGroup!=currentGroup)OpenGroup(hoverGroup);};PreviewDragOver+=(s,e)=>{var entries=DraggedEntries(e.Data);bool files=e.Data.GetDataPresent(DataFormats.FileDrop);if(entries.Length==0&&!files)return;activeDrag=true;dragWatch.Start();Point p=e.GetPosition(Root);if(dragPreviewText.Text.Length==0)if(files){var filesToAdd=(string[])e.Data.GetData(DataFormats.FileDrop);dragPreviewText.Text=filesToAdd.Length==1?System.IO.Path.GetFileName(filesToAdd[0]):"添加 "+filesToAdd.Length+" 个文件";}else dragPreviewText.Text="正在移动图标";dragPreview.Visibility=Visibility.Visible;MoveDragGhost(p);UpdateBoardDragShifts(e);if(entries.Length>0&&Tiles!=null){Point q=e.GetPosition(Tiles);if(q.X>=0&&q.Y>=0&&q.X<Tiles.ActualWidth&&q.Y<Tiles.ActualHeight){e.Effects=!editing&&entries.Length==1&&SlotAt(q)>=0?BoardDragData.Effect(e.AllowedEffects):DragDropEffects.None;e.Handled=true;}}};PreviewDragLeave+=(s,e)=>{if(Tiles!=null&&SlotAt(e.GetPosition(Tiles))<0)ResetBoardDragShifts();};PreviewDrop+=BoardPreviewDrop;Closed+=(s,e)=>{hoverTimer.Stop();dragWatch.Stop();CloseDragGhost();};}
        void RecoverExternalDrag(bool buttonsDown){if(entryDragRunning||!activeDrag||buttonsDown)return;EndDragPreview();Render();}
        internal void CheckBoardFeedback16(){Open();C.Prefs.Groups.Add("待删除测试组");C.Prefs.Apps.Add(new Entry{Name="保留入口",Path=@"C:\keep-feedback16.lnk",Group="待删除测试组"});SyncGroups();Render();activeDrag=true;dragPreview.Visibility=Visibility.Visible;FeatureRules.DeleteGroup(C.Prefs,"待删除测试组");SyncGroups();Render();if(activeDrag||dragPreview.Visibility!=Visibility.Collapsed||folderBar.Children.OfType<Border>().Any(b=>(string)b.Tag=="待删除测试组")||!C.Prefs.Apps.Any(a=>a.Name=="保留入口"&&FeatureRules.InGroup(a,"未分组")))throw new Exception("删除分组后界面或入口未同步");activeDrag=true;dragPreview.Visibility=Visibility.Visible;RecoverExternalDrag(false);if(activeDrag||dragPreview.Visibility!=Visibility.Collapsed)throw new Exception("外部拖拽取消未清理");activeDrag=true;Hide();if(activeDrag||dragWatch.IsEnabled)throw new Exception("隐藏面板后拖拽状态残留");}
        void BeginGroupHover(string name){if(hoverGroup==name)return;hoverGroup=name;hoverTimer.Stop();hoverTimer.Start();}
        void StopGroupHover(string name){if(name==null||hoverGroup==name){hoverGroup=null;hoverTimer.Stop();}}
        void EndDragPreview(){activeDrag=false;dragWatch.Stop();hoverTimer.Stop();hoverGroup=null;dragPreview.Visibility=Visibility.Collapsed;dragPreviewText.Text="";dragPreviewIcon.Source=null;folderSignature="";EndDragGhostMotion();ResetBoardDragShifts();dragShiftTargets.Clear();}
        void StartEntryDrag(FrameworkElement tile,Entry entry){var paths=editing&&selected.Contains(entry.Path)?selected.ToArray():new[]{entry.Path};var payload=BoardDragData.Create(paths,currentGroup);dragPreviewText.Text=paths.Length==1?entry.Name:entry.Name+" 等 "+paths.Length+" 个";try{dragPreviewIcon.Source=FeatureUi.Icon(entry,C.Prefs.Dock.ShowShortcutOverlays,C.Prefs.Dock.SharperIcons);}catch{dragPreviewIcon.Source=null;}bool previous=ModalBusy;ModalBusy=true;activeDrag=true;C.Dock.BeginBoardPinDrag();entryDragRunning=true;try{dragPreview.Visibility=Visibility.Visible;MoveDragGhost(new Point());DragDrop.DoDragDrop(tile,payload,BoardDragData.Allowed(payload));}finally{entryDragRunning=false;C.Dock.EndBoardPinDrag();EndDragPreview();ModalBusy=previous;Render();}}
        DragDropEffects GroupDragEffect(IDataObject data,string name){if(name=="全部")return DragDropEffects.None;if(data.GetDataPresent(DataFormats.FileDrop))return DragDropEffects.Copy;if(DraggedEntries(data).Length>0)return data.GetDataPresent("GlassBoard.SourceGroup")&&(string)data.GetData("GlassBoard.SourceGroup")=="全部"?DragDropEffects.Copy:DragDropEffects.Move;return data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;}
        public void ApplyDisplayOptions(){if(C.Prefs.BoardHideGroups)currentGroup="全部";if(C.Prefs.BoardHideSearch&&Search!=null)Search.Clear();if(IsVisible){BuildForSize();QueueSizeRefresh();}}
        ContextMenu CreateMoreMenu(){
            var menu=MenuTheme.Create(C.Prefs.Light);
            Action<ItemsControl,string,string,Action,bool> add=(parent,label,gesture,action,enabled)=>{
                var item=new MenuItem{Header=label,InputGestureText=gesture,IsEnabled=enabled};
                item.Click+=(s,e)=>{bool previous=ModalBusy;ModalBusy=true;try{menu.IsOpen=false;action();}finally{ModalBusy=previous;}};
                parent.Items.Add(item);
            };
            Action<ItemsControl,string,bool,Action,bool> toggle=(parent,label,value,action,enabled)=>{
                add(parent,label,"",action,enabled);var item=(MenuItem)parent.Items[parent.Items.Count-1];item.IsCheckable=true;item.IsChecked=value;
            };
            add(menu,"撤销","Ctrl+Z",()=>C.UndoBoard(),C.CanUndoRemoval);
            add(menu,"重做","Ctrl+Y",()=>C.RedoBoard(),C.CanRedoBoard);
            menu.Items.Add(new Separator());
            add(menu,"新建文件或文件夹","",()=>NewFileMenu(),true);
            var display=new MenuItem{Header="显示选项"};menu.Items.Add(display);
            add(display,"图标间距与默认大小","",()=>IconSettings(),true);display.Items.Add(new Separator());
            toggle(display,"搜索栏",!C.Prefs.BoardHideSearch,()=>{C.Prefs.BoardHideSearch=!C.Prefs.BoardHideSearch;C.Save();ApplyDisplayOptions();},true);
            toggle(display,"分组栏",!C.Prefs.BoardHideGroups,()=>{C.Prefs.BoardHideGroups=!C.Prefs.BoardHideGroups;C.Save();ApplyDisplayOptions();},true);
            toggle(display,"分组文件夹",foldersVisible,()=>{C.Prefs.BoardHideFolders=!C.Prefs.BoardHideFolders;C.Save();ApplyDisplayOptions();},!C.Prefs.BoardHideGroups);
            var grouping=new MenuItem{Header="分组管理"};menu.Items.Add(grouping);
            add(grouping,"管理分组","",()=>C.ManageGroups(),true);add(grouping,"自动分组","",()=>C.AutoGroupApps(),true);
            add(menu,"工作区","",()=>C.ManageWorkspaces(),true);add(menu,"外观方案","",()=>C.ManageSchemes(),true);
            menu.Items.Add(new Separator());
            var backup=new MenuItem{Header="备份与恢复"};menu.Items.Add(backup);
            add(backup,"导出完整备份","",()=>C.ExportFullBackup(),true);add(backup,"恢复完整备份","",()=>C.RestoreFullBackup(),true);
            add(menu,"支持创作者 · 森霖sama","",()=>C.Settings(5),true);
            return menu;
        }
        void MoreMenu(){EndDragPreview();var menu=CreateMoreMenu();iconMenu=menu;menu.Closed+=(s,e)=>{if(iconMenu==menu)iconMenu=null;};menu.PlacementTarget=this;menu.IsOpen=true;}
    }
}


