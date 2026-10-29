using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace GlassBoard {
    // Hover preview for Dock icons: up to four frosted bubbles in a row, each holding a
    // live DWM thumbnail of one window. Hovering a bubble grows it and nudges the
    // neighbours aside; clicking it brings that window to the front.
    public sealed class ThumbnailPopup : IDisposable {
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle,int command);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr handle);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
        [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(IntPtr dest,IntPtr src,out IntPtr thumb);
        [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(IntPtr thumb);
        [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(IntPtr thumb,ref ThumbnailProperties properties);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr hwnd,ref WindowCompositionData data);
        [StructLayout(LayoutKind.Sequential)] struct RECT {public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)] struct POINT {public int X;public int Y;}
        [StructLayout(LayoutKind.Sequential)] struct ThumbnailProperties {
            public int Flags; public RECT Destination; public RECT Source;
            public byte Opacity; public bool Visible; public bool SourceClientAreaOnly;
        }
        [StructLayout(LayoutKind.Sequential)] struct AccentPolicy {public int State;public int Flags;public int GradientColor;public int AnimationId;}
        [StructLayout(LayoutKind.Sequential)] struct WindowCompositionData {public int Attribute;public IntPtr Data;public int SizeOfData;}

        const int AcrylicBlurBehind=4;
        const int WcaAccentPolicy=19;
        const uint SwpNoActivate=0x0010;
        const int RectFlag=0x1,OpacityFlag=0x4,VisibleFlag=0x8,SourceClientFlag=0x10;

        const int MaxCards=4;
        const double CardWidth=208,Overlap=22,Radius=18,TitleHeight=24,MinImage=120,MaxImage=160,HoverScale=1.38;
        const double NeighbourPush=1.28,Inset=7;

        public event Action<IntPtr> Clicked;
        public event Action Hidden;
        public event Action PointerOut;

        readonly List<Card> cards=new List<Card>();
        readonly Point anchor;
        readonly double scale;
        int hovered=-1;
        bool disposed,animating,inside;
        DateTime animStart;

        sealed class Card {
            public IntPtr Target;
            public HwndSource Source;
            public IntPtr Thumb;
            public Border Root;
            public double RestX,RestY,RestW,RestH;
            public double X,Y,W,H;
            public double StartX,StartY,StartW,StartH;
            public double TargetX,TargetY,TargetW,TargetH;
            public int ThumbW=-1;
        }

        public bool PointerInside {get {return inside;}}
        public bool IsUsable {get {return cards.Count>0;}}
        public IntPtr Handle {get {return cards.Count==0?IntPtr.Zero:cards[0].Source.Handle;}}

        public ThumbnailPopup(IList<RunningWindow> windows,Point anchorDip,double dpiScale) {
            if(windows==null||windows.Count==0)return;
            anchor=anchorDip;
            scale=dpiScale<=0?1:dpiScale;
            int count=Math.Min(MaxCards,windows.Count);
            var sizes=new List<double>();
            for(int i=0;i<count;i++) {
                double aspect=1.6;
                RECT rect;
                if(GetWindowRect(windows[i].Handle,out rect)&&rect.Right>rect.Left&&rect.Bottom>rect.Top) {
                    aspect=Math.Max(.6,Math.Min(2.4,(double)(rect.Bottom-rect.Top)/(rect.Right-rect.Left)));
                }
                double image=CardWidth*aspect;
                image=Math.Max(MinImage,Math.Min(MaxImage,image));
                sizes.Add(image+TitleHeight);
            }
            double rowWidth=(count-1)*(CardWidth-Overlap)+CardWidth;
            Rect work=SystemParameters.WorkArea;
            double originX=Math.Max(work.Left+6,Math.Min(work.Right-rowWidth-6,anchor.X-rowWidth/2));
            double bottom=Math.Max(work.Top+sizes[0]+6,anchor.Y-1);
            for(int i=0;i<count;i++) {
                double x=originX+i*(CardWidth-Overlap);
                double h=sizes[i];
                Card card=Create(windows[i],x,bottom-h,CardWidth,h);
                if(card==null)continue;
                card.RestX=x;card.RestY=bottom-h;card.RestW=CardWidth;card.RestH=h;
                card.X=x;card.Y=bottom-h;card.W=CardWidth;card.H=h;
                card.StartX=card.TargetX=x;card.StartY=card.TargetY=card.Y;
                card.StartW=card.TargetW=CardWidth;card.StartH=card.TargetH=h;
                cards.Add(card);
                Apply(card);
            }
            if(cards.Count>0)CompositionTarget.Rendering+=OnFrame;
        }

        Card Create(RunningWindow window,double x,double y,double w,double h) {
            var parameters=new HwndSourceParameters("GlassBoardCard") {
                Width=(int)Math.Round(w*scale),Height=(int)Math.Round(h*scale),
                WindowStyle=unchecked((int)0x80000000),
                ExtendedWindowStyle=0x00080000|0x08000000|0x00000008,
                PositionX=(int)Math.Round(x*scale),PositionY=(int)Math.Round(y*scale)
            };
            var source=new HwndSource(parameters);
            if(source.Handle==IntPtr.Zero)return null;
            var glass=source.Handle;
            int corner=2;
            try {DwmSetWindowAttribute(glass,33,ref corner,4);} catch {}
            try {ApplyGlass(glass);} catch {}
            var title=new TextBlock {
                Text=string.IsNullOrEmpty(window.Title)?"窗口":window.Title,
                Foreground=new SolidColorBrush(Color.FromArgb(0xF0,0xFF,0xFF,0xFF)),
                FontSize=11,VerticalAlignment=VerticalAlignment.Center,
                Margin=new Thickness(11,0,11,0),TextTrimming=TextTrimming.CharacterEllipsis
            };
            var titleBar=new Border {Height=TitleHeight,VerticalAlignment=VerticalAlignment.Bottom,Background=new SolidColorBrush(Color.FromArgb(0x30,0x00,0x00,0x00)),Child=title};
            var root=new Border {
                CornerRadius=new CornerRadius(Radius),
                Background=new SolidColorBrush(Color.FromArgb(0x22,0xFF,0xFF,0xFF)),
                BorderBrush=new SolidColorBrush(Color.FromArgb(0x58,0xFF,0xFF,0xFF)),
                BorderThickness=new Thickness(1),
                Cursor=System.Windows.Input.Cursors.Hand
            };
            var host=new Grid {Background=Brushes.Transparent};
            host.Children.Add(root);
            root.Child=titleBar;
            IntPtr thumb=IntPtr.Zero;
            if(DwmRegisterThumbnail(glass,window.Handle,out thumb)!=0)thumb=IntPtr.Zero;
            var card=new Card {Target=window.Handle,Source=source,Thumb=thumb,Root=root,RestH=h};
            root.MouseLeftButtonUp+=(s,e)=>{
                e.Handled=true;
                var handler=Clicked;
                if(handler!=null)handler(card.Target);
            };
            source.RootVisual=host;
            ShowWindow(glass,5);
            return card;
        }

        // A light frosted veil; alpha stays low so the wallpaper keeps showing through.
        static void ApplyGlass(IntPtr hwnd) {
            var policy=new AccentPolicy {State=AcrylicBlurBehind,Flags=2,GradientColor=unchecked((int)0x4C1E1A17)};
            IntPtr buffer=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(AccentPolicy)));
            try {
                Marshal.StructureToPtr(policy,buffer,false);
                var data=new WindowCompositionData {Attribute=WcaAccentPolicy,Data=buffer,SizeOfData=Marshal.SizeOf(typeof(AccentPolicy))};
                SetWindowCompositionAttribute(hwnd,ref data);
            } finally {Marshal.FreeHGlobal(buffer);}
        }

        void OnFrame(object sender,EventArgs e) {
            if(disposed)return;
            POINT cursor;
            bool hasCursor=GetCursorPos(out cursor);
            int nextHover=-1;
            if(hasCursor) {
                for(int i=0;i<cards.Count;i++) {
                    Card card=cards[i];
                    int left=(int)Math.Round(card.X*scale),top=(int)Math.Round(card.Y*scale);
                    int right=left+(int)Math.Round(card.W*scale),bottom=top+(int)Math.Round(card.H*scale);
                    if(cursor.X>=left&&cursor.X<=right&&cursor.Y>=top&&cursor.Y<=bottom){nextHover=i;break;}
                }
            }
            if(nextHover!=hovered)SetHover(nextHover);
            bool wasInside=inside;
            inside=hasCursor&&(nextHover>=0);
            if(wasInside&&!inside) {
                var handler=PointerOut;
                if(handler!=null)handler();
            }
            if(!animating)return;
            double t=(DateTime.UtcNow-animStart).TotalMilliseconds/200.0;
            if(t<0)t=0;if(t>1)t=1;
            double ease=1-Math.Pow(1-t,3);
            for(int i=0;i<cards.Count;i++) {
                Card card=cards[i];
                card.X=card.StartX+(card.TargetX-card.StartX)*ease;
                card.Y=card.StartY+(card.TargetY-card.StartY)*ease;
                card.W=card.StartW+(card.TargetW-card.StartW)*ease;
                card.H=card.StartH+(card.TargetH-card.StartH)*ease;
                Apply(card);
            }
            if(t>=1)animating=false;
        }

        void SetHover(int index) {
            hovered=index;
            double hoverCenter=0;
            if(index>=0)hoverCenter=cards[index].RestX+cards[index].RestW/2;
            for(int i=0;i<cards.Count;i++) {
                Card card=cards[i];
                card.StartX=card.X;card.StartY=card.Y;card.StartW=card.W;card.StartH=card.H;
                if(i==index) {
                    card.TargetW=card.RestW*HoverScale;
                    card.TargetH=card.RestH*HoverScale;
                    card.TargetX=card.RestX-(card.TargetW-card.RestW)/2;
                    card.TargetY=card.RestY-(card.TargetH-card.RestH);
                } else {
                    card.TargetW=card.RestW;
                    card.TargetH=card.RestH;
                    card.TargetY=card.RestY;
                    if(index>=0) {
                        double center=card.RestX+card.RestW/2;
                        double moved=hoverCenter+(center-hoverCenter)*NeighbourPush;
                        card.TargetX=moved-card.RestW/2;
                    } else card.TargetX=card.RestX;
                }
            }
            animStart=DateTime.UtcNow;
            animating=true;
            if(index>=0)SetWindowPos(cards[index].Source.Handle,IntPtr.Zero,(int)Math.Round(cards[index].X*scale),(int)Math.Round(cards[index].Y*scale),(int)Math.Round(cards[index].W*scale),(int)Math.Round(cards[index].H*scale),SwpNoActivate);
        }

        void Apply(Card card) {
            SetWindowPos(card.Source.Handle,IntPtr.Zero,(int)Math.Round(card.X*scale),(int)Math.Round(card.Y*scale),(int)Math.Round(card.W*scale),(int)Math.Round(card.H*scale),SwpNoActivate);
            if(card.Thumb==IntPtr.Zero)return;
            int width=(int)Math.Round(card.W*scale);
            if(Math.Abs(width-card.ThumbW)<2)return;
            card.ThumbW=width;
            int pad=(int)Math.Round(Inset*scale);
            int title=(int)Math.Round(TitleHeight*scale);
            var properties=new ThumbnailProperties {
                Flags=RectFlag|OpacityFlag|VisibleFlag|SourceClientFlag,
                Destination=new RECT {
                    Left=pad,Top=pad,
                    Right=(int)Math.Round(card.W*scale)-pad,
                    Bottom=(int)Math.Round(card.H*scale)-pad-title
                },
                Opacity=255,Visible=true,SourceClientAreaOnly=false
            };
            DwmUpdateThumbnailProperties(card.Thumb,ref properties);
        }

        public void Dispose() {
            if(disposed)return;
            disposed=true;
            try {CompositionTarget.Rendering-=OnFrame;} catch {}
            foreach(Card card in cards) {
                try {if(card.Thumb!=IntPtr.Zero)DwmUnregisterThumbnail(card.Thumb);} catch {}
                try {if(card.Source!=null){DestroyWindow(card.Source.Handle);card.Source.Dispose();}} catch {}
            }
            cards.Clear();
            hovered=-1;
            var handler=Hidden;
            if(handler!=null)handler();
        }
    }
}
