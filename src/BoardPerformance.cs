using System;using System.Linq;using System.Text;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Threading;
namespace GlassBoard {
    public partial class BoardWindow {
        string viewStructureKey,viewDataKey;bool viewRenderDirty=true,openingFrame;
        readonly DispatcherTimer openingSettled=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(220)};
        string ViewStructureKey(){var p=C.Prefs;return p.Light+"/"+p.BoardHideSearch+"/"+p.BoardHideGroups+"/"+p.BoardHideFolders+"/"+p.Dock.ShowShortcutOverlays+"/"+p.Dock.SharperIcons;}
        string ViewDataKey(){var text=new StringBuilder();var p=C.Prefs;text.Append(currentGroup).Append('/').Append(docFilter).Append('/').Append(p.BoardIconSize).Append('/').Append(p.BoardIconGap).Append('/').Append(p.Dock.ShowRunning);foreach(var g in p.Groups)text.Append('|').Append(g);foreach(var a in p.Apps){text.Append('|').Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a)).Append(':').Append(a.Name).Append(':').Append(a.Path).Append(':').Append(a.IconPath).Append(':').Append(a.Pinned).Append(':').Append(a.Group);if(a.ExtraGroups!=null)foreach(var g in a.ExtraGroups)text.Append(';').Append(g);}return text.ToString();}
        void PrepareOpeningFrame(){StopOpeningFrame();openingFrame=true;BeginAnimation(OpacityProperty,null);Opacity=1;ContentGrid.BeginAnimation(OpacityProperty,null);ContentGrid.Opacity=MotionEnabled?0:1;}
        void ScheduleOpeningFrame(){int serial=visibilitySerial;Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{
            if(!IsVisible||serial!=visibilitySerial)return;
            UpdateLayout();sizeRefresh.Stop();ApplySizeRefresh();sizeRefresh.Stop();AnimateOpen();
            openingSettled.Stop();openingSettled.Tick-=OpeningSettled;openingSettled.Tick+=OpeningSettled;openingSettled.Start();
        }));}
        void OpeningSettled(object sender,EventArgs args){openingSettled.Stop();openingFrame=false;if(!IsVisible)return;QueueSizeRefresh();QueueRunningMarkers(visibilitySerial,Tiles.Children.OfType<Border>().ToArray(),0);}
        void QueueRunningMarkers(int serial,Border[] tiles,int at){if(!IsVisible||serial!=visibilitySerial||at>=tiles.Length)return;Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>{if(!IsVisible||serial!=visibilitySerial)return;var entry=tiles[at].Tag as Entry;var grid=tiles[at].Child as Grid;if(entry!=null&&grid!=null)foreach(var marker in grid.Children.OfType<Border>().Where(b=>b.Tag as string=="running"))marker.Visibility=C.Prefs.Dock.ShowRunning&&RunningWindows.IsRunning(entry)?Visibility.Visible:Visibility.Collapsed;QueueRunningMarkers(serial,tiles,at+1);}));}
        void StopOpeningFrame(){openingSettled.Stop();openingFrame=false;ContentGrid.CacheMode=null;ContentGrid.BeginAnimation(OpacityProperty,null);ContentGrid.Opacity=1;}
        internal void CheckWarmOpen(){Open();var search=Search;var grid=Tiles;Hide();Open();if(!object.ReferenceEquals(search,Search)||!object.ReferenceEquals(grid,Tiles))throw new Exception("重复展开仍重建面板宿主");Hide();}
    }
}
