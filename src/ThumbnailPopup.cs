using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace GlassBoard {
    // Live hover previews: DwmRegisterThumbnail draws the real window surface into a
    // small popup above the Dock. A plain (non layered) HwndSource is required -
    // DWM refuses to composite thumbnails into WS_EX_LAYERED windows.
    public sealed class ThumbnailPopup : IDisposable {
        [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(IntPtr destination,IntPtr source,out IntPtr thumbnail);
        [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
        [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail,ref ThumbnailProperties properties);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle,int command);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr handle);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr handle,out RECT rect);
        [StructLayout(LayoutKind.Sequential)] struct RECT {public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)] struct ThumbnailProperties {
            public uint Flags; public RECT Destination; public RECT Source; public byte Opacity; public bool Visible; public bool SourceClientAreaOnly;
        }
        const uint RectFlag=0x1, OpacityFlag=0x4, VisibleFlag=0x8, ClientOnlyFlag=0x10;
        const int MaxRows=3;
        const double PopupWidthDip=268,CellHeightDip=140,GapDip=8,PaddingDip=10;
        readonly List<IntPtr> thumbnails=new List<IntPtr>();
        readonly HwndSource source;
        public bool Available{get{return thumbnails.Count>0;}}
        public IntPtr Handle{get{return source==null?IntPtr.Zero:source.Handle;}}
        public ThumbnailPopup(IList<IntPtr> windows,double left,double top,double scale) {
            if(windows==null||windows.Count==0)return;
            int rows=Math.Min(MaxRows,windows.Count);if(scale<=0)scale=1;
            int width=(int)Math.Ceiling(PopupWidthDip*scale);
            int height=(int)Math.Ceiling((PaddingDip*2+rows*CellHeightDip+(rows-1)*GapDip)*scale);
            var parameters=new HwndSourceParameters("GlassBoardThumbnails") {
                Width=width,Height=height,WindowStyle=unchecked((int)0x80000000),
                ExtendedWindowStyle=0x00080000|0x08000000|0x00000020|0x00000008,
                PositionX=(int)Math.Round(left),PositionY=(int)Math.Round(top)
            };
            source=new HwndSource(parameters);
            source.RootVisual=new Border {
                Background=new SolidColorBrush(Color.FromArgb(238,18,22,30)),
                CornerRadius=new CornerRadius(12),
                BorderBrush=new SolidColorBrush(Color.FromArgb(60,255,255,255)),
                BorderThickness=new Thickness(1)
            };
            ShowWindow(source.Handle,5);
            RECT client;GetClientRect(source.Handle,out client);
            int pad=12,gap=8;
            int cellWidth=Math.Max(1,client.Right-client.Left-pad*2);
            int cellHeight=Math.Max(1,(client.Bottom-client.Top-pad*2-(rows-1)*gap)/rows);
            for(int index=0;index<rows;index++) {
                int cellTop=pad+index*(cellHeight+gap);
                IntPtr thumbnail;
                if(DwmRegisterThumbnail(source.Handle,windows[index],out thumbnail)!=0)continue;
                var properties=new ThumbnailProperties {
                    Flags=RectFlag|VisibleFlag|OpacityFlag|ClientOnlyFlag,
                    Destination=new RECT {Left=pad,Top=cellTop,Right=pad+cellWidth,Bottom=cellTop+cellHeight},
                    Opacity=255,Visible=true,SourceClientAreaOnly=true
                };
                DwmUpdateThumbnailProperties(thumbnail,ref properties);
                thumbnails.Add(thumbnail);
            }
        }
        public void Dispose() {
            foreach(IntPtr thumbnail in thumbnails) {try {DwmUnregisterThumbnail(thumbnail);} catch(Exception ex) {Store.Log(ex);}}
            thumbnails.Clear();
            try {if(source!=null&&source.Handle!=IntPtr.Zero)DestroyWindow(source.Handle);} catch(Exception ex) {Store.Log(ex);}
        }
    }
}
