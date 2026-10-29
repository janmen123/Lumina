using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
namespace GlassBoard {
    internal static class BoardDragData {
        internal static DataObject Create(string[] paths,string group){
            var data=new DataObject();data.SetData("GlassBoard.Entry",paths[0]);data.SetData("GlassBoard.Entries",paths);data.SetData("GlassBoard.SourceGroup",group);
            var files=new StringCollection();
            foreach(string path in paths.Distinct(StringComparer.OrdinalIgnoreCase)){
                try{if(Path.IsPathRooted(path)&&(File.Exists(path)||Directory.Exists(path)))files.Add(Path.GetFullPath(path));}catch(ArgumentException){}catch(NotSupportedException){}
            }
            if(files.Count>0)data.SetFileDropList(files);
            return data;
        }
        // External targets receive copies. Reordering launcher entries never moves disk files.
        internal static DragDropEffects Allowed(IDataObject data){return data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.Move|DragDropEffects.Copy;}
        internal static DragDropEffects Effect(DragDropEffects allowed){return (allowed&DragDropEffects.Copy)!=0?DragDropEffects.Copy:(allowed&DragDropEffects.Move);}
    }
    public partial class BoardWindow {
        ScaleTransform dragGhostScale;
        Window dragGhostWindow;
        bool dragRenderingSubscribed;
        TimeSpan lastDragRenderingTime=TimeSpan.MinValue;
        readonly Dictionary<TranslateTransform,Point> dragShiftTargets=new Dictionary<TranslateTransform,Point>();
        int ghostX=int.MinValue,ghostY=int.MinValue;
        bool dragGhostShown;
        FrameworkElement dragShiftSource;
        int dragShiftFrom=-1,dragShiftTo=-1;
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr handle,int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr handle,int index,int value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr handle,IntPtr after,int x,int y,int width,int height,uint flags);
        internal void InitDragGhostMotion(){
            dragGhostScale=new ScaleTransform(1,1);
            dragPreview.RenderTransform=dragGhostScale;
            dragPreview.RenderTransformOrigin=new Point(.5,.5);
            dragPreview.CacheMode=new BitmapCache();
            dragPreviewIcon.Width=54;dragPreviewIcon.Height=54;
            dragPreview.Padding=new Thickness(10);
            dragPreview.CornerRadius=new CornerRadius(20);
            dragPreview.Effect=new DropShadowEffect{BlurRadius=22,ShadowDepth=9,Direction=270,Opacity=.45,Color=Colors.Black};
            dragPreviewText.FontSize=13;dragPreviewText.MaxWidth=200;
            dragGhostWindow=new Window{
                WindowStyle=WindowStyle.None,
                AllowsTransparency=true,
                Background=Brushes.Transparent,
                ShowActivated=false,
                ShowInTaskbar=false,
                Opacity=1,
                Topmost=true,
                ResizeMode=ResizeMode.NoResize,
                SizeToContent=SizeToContent.WidthAndHeight,
                WindowStartupLocation=WindowStartupLocation.Manual,
                Focusable=false,
                IsHitTestVisible=false,
                Content=new Border{Padding=new Thickness(16),Child=dragPreview}
            };
            dragGhostWindow.SourceInitialized+=(s,e)=>MakeGhostClickThrough(new WindowInteropHelper(dragGhostWindow).Handle);
        }
        static void MakeGhostClickThrough(IntPtr handle){
            int style=GetWindowLong(handle,-20);
            SetWindowLong(handle,-20,style|0x00000020|0x00000080|0x08000000);
        }
        internal void MoveDragGhost(Point p){
            if(dragGhostWindow==null||C==null)return;
            if(!dragGhostShown){
                bool motion=C.Prefs.Dock.DragMotion;
                dragPreviewIcon.Width=dragPreviewIcon.Height=motion?54:28;
                dragPreview.Padding=motion?new Thickness(10):new Thickness(12,9,12,9);
                dragPreview.CornerRadius=new CornerRadius(motion?20:14);
                dragPreview.Effect=motion?new DropShadowEffect{BlurRadius=16,ShadowDepth=5,Direction=270,Opacity=.35,Color=Colors.Black}:null;
                var position=System.Windows.Forms.Cursor.Position;
                var source=PresentationSource.FromVisual(this);
                var dip=source==null||source.CompositionTarget==null?new Point(position.X,position.Y):source.CompositionTarget.TransformFromDevice.Transform(new Point(position.X,position.Y));
                dragGhostWindow.Left=dip.X;dragGhostWindow.Top=dip.Y;
                dragGhostWindow.Show();
                dragGhostShown=true;
                ghostX=ghostY=int.MinValue;
                UpdateDragGhostPosition();
                StartDragRendering();
                if(!motion)return;
                var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
                dragGhostScale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(.84,1,TimeSpan.FromMilliseconds(140)){EasingFunction=ease});
                dragGhostScale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(.84,1,TimeSpan.FromMilliseconds(140)){EasingFunction=ease});
                return;
            }
            // Rendering owns movement; DragOver must not add another positioning pass.
        }
        void StartDragRendering(){
            if(dragRenderingSubscribed)return;
            lastDragRenderingTime=TimeSpan.MinValue;
            CompositionTarget.Rendering+=OnDragRendering;dragRenderingSubscribed=true;
        }
        void StopDragRendering(){
            if(!dragRenderingSubscribed)return;
            CompositionTarget.Rendering-=OnDragRendering;dragRenderingSubscribed=false;
            lastDragRenderingTime=TimeSpan.MinValue;
        }
        void OnDragRendering(object sender,EventArgs e){
            if(!dragGhostShown){StopDragRendering();return;}
            var frame=e as RenderingEventArgs;
            // Layout may request multiple callbacks for the same composition frame.
            if(frame!=null){if(frame.RenderingTime==lastDragRenderingTime)return;lastDragRenderingTime=frame.RenderingTime;}
            UpdateDragGhostPosition();
        }
        void UpdateDragGhostPosition(){
            if(!dragGhostShown||dragGhostWindow==null)return;
            var position=System.Windows.Forms.Cursor.Position;
            if(position.X==ghostX&&position.Y==ghostY)return;
            ghostX=position.X;ghostY=position.Y;
            // Move both screen coordinates together without two WPF layout updates.
            SetWindowPos(new WindowInteropHelper(dragGhostWindow).Handle,IntPtr.Zero,ghostX,ghostY,0,0,0x0001|0x0004|0x0010);
        }
        internal void UpdateBoardDragShifts(DragEventArgs e){
            if(Tiles==null||C==null||editing||!C.Prefs.Dock.DragMotion){ResetBoardDragShifts();return;}
            string[] paths=DraggedEntries(e.Data);
            if(paths.Length!=1){ResetBoardDragShifts();return;}
            if(dragShiftSource!=null&&!Tiles.Children.Contains(dragShiftSource))ResetBoardDragShifts();
            if(dragShiftSource==null){
                foreach(var child in Tiles.Children){
                    var element=child as FrameworkElement;
                    var entry=element==null?null:element.Tag as Entry;
                    if(entry!=null&&string.Equals(entry.Path,paths[0],StringComparison.OrdinalIgnoreCase)){dragShiftSource=element;break;}
                }
                if(dragShiftSource==null){ResetBoardDragShifts();return;}
                dragShiftFrom=Tiles.Children.IndexOf(dragShiftSource);
                if(dragShiftFrom<0){ResetBoardDragShifts();return;}
                dragShiftSource.Opacity=.22;
            }
            Point p=e.GetPosition(Tiles);
            int slot=SlotAt(p);
            if(slot<0){ResetBoardDragShifts();return;}
            if(slot==dragShiftTo)return;
            dragShiftTo=slot;
            ApplyBoardDragShifts();
        }
        int SlotAt(Point p){
            if(Tiles==null)return -1;
            int columns=Tiles.Columns,rows=Tiles.Rows;
            if(columns<=0||rows<=0)return -1;
            double width=Tiles.ActualWidth,height=Tiles.ActualHeight;
            if(width<=0||height<=0)return -1;
            if(p.X<0||p.Y<0||p.X>=width||p.Y>=height)return -1;
            int count=Tiles.Children.Count;
            if(count==0)return -1;
            int column=(int)(p.X/(width/columns));
            int row=(int)(p.Y/(height/rows));
            if(column<0)column=0;else if(column>=columns)column=columns-1;
            if(row<0)row=0;else if(row>=rows)row=rows-1;
            int slot=row*columns+column;
            return slot<count?slot:-1;
        }
        Entry BoardDropEntry(Point p){int slot=SlotAt(p);return slot<0?null:((FrameworkElement)Tiles.Children[slot]).Tag as Entry;}
        void BoardPreviewDrop(object sender,DragEventArgs e){
            // Resolve against stable grid slots BEFORE cleanup. Animated tiles are not targets.
            string[] paths=DraggedEntries(e.Data);
            Entry target=paths.Length==1&&!editing&&Tiles!=null?BoardDropEntry(e.GetPosition(Tiles)):null;
            if(paths.Length>0&&Tiles!=null){
                Point p=e.GetPosition(Tiles);
                if(p.X>=0&&p.Y>=0&&p.X<Tiles.ActualWidth&&p.Y<Tiles.ActualHeight){e.Handled=true;e.Effects=target==null?DragDropEffects.None:BoardDragData.Effect(e.AllowedEffects);}
            }
            EndDragPreview();
            if(e.Handled&&target!=null)C.ReorderEntry(paths[0],target);
            Dispatcher.BeginInvoke(new Action(()=>{if(!activeDrag&&IsVisible)Render();}));
        }
        int ColumnOf(int slot){int columns=Tiles==null?0:Tiles.Columns;return columns<=0?0:slot%columns;}
        int RowOf(int slot){int columns=Tiles==null?0:Tiles.Columns;return columns<=0?0:slot/columns;}
        void ApplyBoardDragShifts(){
            int from=dragShiftFrom,to=dragShiftTo;
            if(from<0||to<0||Tiles==null)return;
            int columns=Tiles.Columns,rows=Tiles.Rows;
            if(columns<=0||rows<=0)return;
            double cellWidth=Tiles.ActualWidth/columns;
            double cellHeight=Tiles.ActualHeight/rows;
            for(int i=0;i<Tiles.Children.Count;i++){
                var element=Tiles.Children[i] as FrameworkElement;
                if(element==null)continue;
                var translate=element.RenderTransform as TranslateTransform;
                double x=0,y=0;
                if(element!=dragShiftSource){
                    bool moves=from<to?(i>from&&i<=to):(i>=to&&i<from);
                    if(moves){
                        int targetIndex=from<to?i-1:i+1;
                        x=(ColumnOf(targetIndex)-ColumnOf(i))*cellWidth;
                        y=(RowOf(targetIndex)-RowOf(i))*cellHeight;
                    }
                }
                if(translate==null){if(x==0&&y==0)continue;translate=new TranslateTransform();element.RenderTransform=translate;}
                AnimateShift(translate,x,y);
            }
        }
        void AnimateShift(TranslateTransform translate,double x,double y){
            Point previous;
            // Compare destinations, not the current animated coordinates.
            if(dragShiftTargets.TryGetValue(translate,out previous)&&previous.X==x&&previous.Y==y)return;
            dragShiftTargets[translate]=new Point(x,y);
            double oldX=translate.X,oldY=translate.Y;
            translate.BeginAnimation(TranslateTransform.XProperty,null);translate.BeginAnimation(TranslateTransform.YProperty,null);
            translate.X=x;translate.Y=y;
            var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
            if(Math.Abs(oldX-x)>.1)translate.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(oldX,x,TimeSpan.FromMilliseconds(135)){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
            if(Math.Abs(oldY-y)>.1)translate.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(oldY,y,TimeSpan.FromMilliseconds(135)){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
        }
        internal void ResetBoardDragShifts(){
            // Include clocks that are scheduled but have not moved a pixel yet.
            foreach(var translate in dragShiftTargets.Keys.ToArray())AnimateShift(translate,0,0);
            if(dragShiftSource!=null)dragShiftSource.Opacity=1;
            dragShiftSource=null;
            dragShiftFrom=-1;
            dragShiftTo=-1;
        }
        internal void EndDragGhostMotion(){
            dragGhostShown=false;
            StopDragRendering();
            if(dragGhostWindow!=null){try{dragGhostWindow.Hide();}catch{}}
            if(dragGhostScale!=null){
                dragGhostScale.BeginAnimation(ScaleTransform.ScaleXProperty,null);
                dragGhostScale.BeginAnimation(ScaleTransform.ScaleYProperty,null);
                dragGhostScale.ScaleX=1;dragGhostScale.ScaleY=1;
            }
        }
        void CloseDragGhost(){EndDragGhostMotion();if(dragGhostWindow!=null){dragGhostWindow.Close();dragGhostWindow=null;}}
    }
}
