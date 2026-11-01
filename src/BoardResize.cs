using System;using System.Windows;using System.Windows.Threading;
namespace Lumina {
    public partial class BoardWindow {
        readonly DispatcherTimer sizeRefresh=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(100)};
        bool resizingPanel,refreshingSize,iconsDirty=true;
        void InitResizeRefresh(){sizeRefresh.Tick+=(s,e)=>{sizeRefresh.Stop();ApplySizeRefresh();};}
        void QueueSizeRefresh(){if(openingFrame||Tiles==null||resizingPanel||refreshingSize||(!IsVisible&&!preparingView))return;if(!sizeRefresh.IsEnabled)sizeRefresh.Start();}
        void ApplySizeRefresh(){
            if(Tiles==null||resizingPanel||refreshingSize||(!IsVisible&&!preparingView))return;
            refreshingSize=true;
            try{
                int cols=BoardIconRules.Columns(Tiles.ActualWidth>0?Tiles.ActualWidth:Width-104,C.Prefs.BoardIconSize,C.Prefs.BoardIconGap);
                int rows=BoardIconRules.Rows(Tiles.ActualHeight>0?Tiles.ActualHeight:Height-(C.Prefs.BoardHideGroups?180:258),C.Prefs.BoardIconSize,C.Prefs.BoardIconGap);
                // Never mutate the visual tree from inside SizeChanged. A drag stretches the
                // existing grid; releasing it applies one settled layout without rebuilding UI.
                if(!iconsDirty&&columns==cols&&Tiles.Rows==rows)return;iconsDirty=false;columns=cols;capacity=cols*rows;Tiles.Columns=cols;Tiles.Rows=rows;Render(false);
            }catch(Exception ex){Store.Log(ex);SetStatus("面板刷新未完成，请重新打开面板。");}
            finally{refreshingSize=false;}
        }
        internal void CheckResizeRefresh(){
            Open();string query=Search.Text;var search=Search;var tiles=Tiles;
            foreach(double width in new[]{900.0,1200.0,920.0,1260.0}){Width=width;Height=620;UpdateLayout();QueueSizeRefresh();ApplySizeRefresh();UpdateLayout();}
            sizeRefresh.Stop();if(!object.ReferenceEquals(search,Search)||!object.ReferenceEquals(tiles,Tiles)||Search.Text!=query)throw new Exception("缩放不应重建搜索框或图标宿主");
            Hide();
        }
    }
}
