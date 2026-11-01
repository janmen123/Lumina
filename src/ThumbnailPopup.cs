using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Lumina {
    // Hover preview for Dock icons: up to five frosted bubbles in a row, each holding a
    // live DWM thumbnail of one window. Hovering a bubble grows it and nudges the
    // neighbours aside; clicking it brings that window to the front.
    public sealed partial class ThumbnailPopup : IDisposable {
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

        const int MaxCards=5;
        const double CardWidth=168,Overlap=-10,Radius=14,TitleHeight=22,MinImage=88,MaxImage=108,HoverScale=1.12;
        const double Inset=5;

        public event Action<IntPtr> Clicked;
        public event Action Hidden;public event Action DismissRequested;
        public event Action PointerOut;

        readonly List<Card> cards=new List<Card>();
        readonly Point anchor;
        readonly double scale;
        int hovered=-1;
        bool disposed,animating,inside;
        readonly System.Diagnostics.Stopwatch motionClock=System.Diagnostics.Stopwatch.StartNew();double animStart;Rect workArea;double rowBottom;readonly DispatcherTimer pointerTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(32)};Point lastPointer=new Point(double.NaN,double.NaN);int candidate=-2;double candidateAt;bool frameHooked;TimeSpan lastRenderTime=TimeSpan.MinValue;

        sealed class Card {
            public IntPtr Target;
            public HwndSource Source;
            public IntPtr Thumb;
            public Border Root;
            public double RestX,RestY,RestW,RestH;
            public double X,Y,W,H;
            public double StartX,StartY,StartW,StartH;
            public double TargetX,TargetY,TargetW,TargetH;
            public int ThumbW=-1,ThumbH=-1,PixelX=int.MinValue,PixelY=int.MinValue,PixelW=-1,PixelH=-1;public double PressX,PressY;public bool Dragged;public Point Press;
        }

        public bool PointerInside {get {return inside||cards.Exists(c=>c.Root.IsMouseCaptured);}}
        public bool IsUsable {get {return cards.Count>0;}}
        public IntPtr Handle {get {return cards.Count==0?IntPtr.Zero:cards[0].Source.Handle;}}

        public ThumbnailPopup(IList<RunningWindow> windows,Point anchorDip,double dpiScale) {
            if(windows==null||windows.Count==0)return;
            anchor=anchorDip;
            scale=dpiScale<=0?1:dpiScale;
            int count=Math.Min(MaxCards,windows.Count);
            var sizes=new List<double>();
            for(int i=0;i<count;i++) {
                double aspect=.625;
                RECT rect;
                if(GetWindowRect(windows[i].Handle,out rect)&&rect.Right>rect.Left&&rect.Bottom>rect.Top) {
                    aspect=Math.Max(.6,Math.Min(2.4,(double)(rect.Bottom-rect.Top)/(rect.Right-rect.Left)));
                }
                double image=CardWidth*aspect;
                image=Math.Max(MinImage,Math.Min(MaxImage,image));
                sizes.Add(image+TitleHeight);
            }
            double rowWidth=(count-1)*(CardWidth-Overlap)+CardWidth;
            var monitor=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)(anchor.X*scale),(int)(anchor.Y*scale)));var wa=monitor.WorkingArea;Rect work=new Rect(wa.Left/scale,wa.Top/scale,wa.Width/scale,wa.Height/scale);
            workArea=work;double originX=Math.Max(work.Left+6,Math.Min(work.Right-rowWidth-6,anchor.X-rowWidth/2));
            double bottom=Math.Max(work.Top+sizes[0]+6,Math.Min(work.Bottom-6,anchor.Y-1));rowBottom=bottom;
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
            if(cards.Count>0){SetHover(-1);foreach(var c in cards){c.X=c.TargetX;c.Y=c.TargetY;c.W=c.TargetW;c.H=c.TargetH;Apply(c);}animating=false;StopFrames();pointerTimer.Tick+=PollPointer;pointerTimer.Start();}
        }

        Card Create(RunningWindow window,double x,double y,double w,double h) {
            var parameters=new HwndSourceParameters("LuminaCard") {
                Width=(int)Math.Round(w*scale),Height=(int)Math.Round(h*scale),
                WindowStyle=unchecked((int)0x80000000),
                ExtendedWindowStyle=0x08000000|0x00000008,
                PositionX=(int)Math.Round(x*scale),PositionY=(int)Math.Round(y*scale)
            };
            var source=new HwndSource(parameters);
            if(source.Handle==IntPtr.Zero)return null;
            source.CompositionTarget.BackgroundColor=Colors.Transparent;var glass=source.Handle;
            int corner=2;
            try {DwmSetWindowAttribute(glass,33,ref corner,4);} catch {}
            try {ApplyGlass(glass);} catch {}
            var title=new TextBlock {
                Text=string.IsNullOrEmpty(window.Title)?"窗口":window.Title,
                Foreground=new SolidColorBrush(Color.FromArgb(0xF0,0xFF,0xFF,0xFF)),
                FontSize=11,VerticalAlignment=VerticalAlignment.Center,
                Margin=new Thickness(11,0,30,0),TextTrimming=TextTrimming.CharacterEllipsis
            };
            var titleBar=new Border {Height=TitleHeight,VerticalAlignment=VerticalAlignment.Top,Background=new SolidColorBrush(Color.FromArgb(0x10,0x00,0x00,0x00)),Child=title};
            var root=new Border {
                CornerRadius=new CornerRadius(Radius),
                Background=new SolidColorBrush(Color.FromArgb(0x22,0xFF,0xFF,0xFF)),
                BorderBrush=new SolidColorBrush(Color.FromArgb(0x58,0xFF,0xFF,0xFF)),
                BorderThickness=new Thickness(1),
                Cursor=System.Windows.Input.Cursors.Hand
            };
            var host=new Grid {Background=Brushes.Transparent};
            host.Children.Add(root);
            var content=new Grid();content.Children.Add(titleBar);root.Child=content;
            IntPtr thumb=IntPtr.Zero;
            if(DwmRegisterThumbnail(glass,window.Handle,out thumb)!=0)thumb=IntPtr.Zero;
            var card=new Card {Target=window.Handle,Source=source,Thumb=thumb,Root=root,RestH=h};
            InstallCardActions(card,window,content);root.MouseLeftButtonUp+=(s,e)=>{if(card.Dragged){card.Dragged=false;return;}
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
            var policy=new AccentPolicy {State=3,Flags=0,GradientColor=unchecked((int)0x241E1A17)};
            IntPtr buffer=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(AccentPolicy)));
            try {
                Marshal.StructureToPtr(policy,buffer,false);
                var data=new WindowCompositionData {Attribute=WcaAccentPolicy,Data=buffer,SizeOfData=Marshal.SizeOf(typeof(AccentPolicy))};
                SetWindowCompositionAttribute(hwnd,ref data);
            } finally {Marshal.FreeHGlobal(buffer);}
        }

        // A stationary pointer must not change selection as geometry animates.
        bool Contains(Card c,double x,double y,double pad){return x>=c.X-pad&&x<=c.X+c.W+pad&&y>=c.Y-pad&&y<=c.Y+c.H+pad;}
        int Hit(double x,double y){if(hovered>=0&&Contains(cards[hovered],x,y,3))return hovered;for(int i=0;i<cards.Count;i++)if(Contains(cards[i],x,y,0))return i;return -1;}
        void PollPointer(object sender,EventArgs e){if(disposed||cards.Exists(c=>c.Root.IsMouseCaptured))return;POINT p;if(GetCursorPos(out p))UpdatePointer(p.X/scale,p.Y/scale,motionClock.Elapsed.TotalMilliseconds);}
        void UpdatePointer(double x,double y,double now){
            bool moved=double.IsNaN(lastPointer.X)||Math.Abs(x-lastPointer.X)>2||Math.Abs(y-lastPointer.Y)>2;
            bool within=false;foreach(var c in cards)if(Contains(c,x,y,6)){within=true;break;}
            if(!within&&cards.Count>0)within=x>=cards[0].X-6&&x<=cards[cards.Count-1].X+cards[cards.Count-1].W+6&&y>=rowBottom-8&&y<=anchor.Y+14;
            bool was=inside;inside=within;if(was&&!inside){var handler=PointerOut;if(handler!=null)handler();}
            if(moved){lastPointer=new Point(x,y);int hit=Hit(x,y);if(hit!=candidate){candidate=hit;candidateAt=now;}}
            if(candidate!=hovered&&candidate!=-2&&now-candidateAt>=75){if(candidate>=0&&Hit(x,y)!=candidate){candidate=hovered;return;}if(candidate<0&&inside)return;SetHover(candidate);}
        }
        void HookFrames(){if(frameHooked)return;CompositionTarget.Rendering+=OnFrame;frameHooked=true;}
        void StopFrames(){if(!frameHooked)return;CompositionTarget.Rendering-=OnFrame;frameHooked=false;}
        void OnFrame(object sender,EventArgs e){
            if(disposed||!animating||cards.Exists(c=>c.Root.IsMouseCaptured)){StopFrames();return;}
            var rendering=e as RenderingEventArgs;if(rendering!=null){if(rendering.RenderingTime==lastRenderTime)return;lastRenderTime=rendering.RenderingTime;}double t=Math.Max(0,Math.Min(1,(motionClock.Elapsed.TotalMilliseconds-animStart)/160.0));double ease=t*t*(3-2*t);
            foreach(var c in cards){c.X=c.StartX+(c.TargetX-c.StartX)*ease;c.Y=c.StartY+(c.TargetY-c.StartY)*ease;c.W=c.StartW+(c.TargetW-c.StartW)*ease;c.H=c.StartH+(c.TargetH-c.StartH)*ease;Apply(c);}
            if(t>=1){animating=false;StopFrames();}
        }
        void SetHover(int index){
            hovered=index;double total=10*Math.Max(0,cards.Count-1);for(int i=0;i<cards.Count;i++)total+=cards[i].RestW*(i==index?HoverScale:1);
            double fit=Math.Min(1,(workArea.Width-24)/Math.Max(1,total));total*=fit;
            double x=Math.Max(workArea.Left+12,Math.Min(workArea.Right-total-12,anchor.X-total/2));
            for(int i=0;i<cards.Count;i++){Card c=cards[i];c.StartX=c.X;c.StartY=c.Y;c.StartW=c.W;c.StartH=c.H;c.TargetW=c.RestW*(i==index?HoverScale:1)*fit;c.TargetH=c.RestH*(i==index?HoverScale:1)*fit;c.TargetX=x;c.TargetY=Math.Max(workArea.Top+8,rowBottom-c.TargetH);x+=c.TargetW+10*fit;}
            animStart=motionClock.Elapsed.TotalMilliseconds;animating=true;HookFrames();
        }
        void Apply(Card card) {
            int px=(int)Math.Round(card.X*scale),py=(int)Math.Round(card.Y*scale),pw=(int)Math.Round(card.W*scale),ph=(int)Math.Round(card.H*scale);if(px!=card.PixelX||py!=card.PixelY||pw!=card.PixelW||ph!=card.PixelH){uint flags=SwpNoActivate|0x0004|0x0200;if(pw==card.PixelW&&ph==card.PixelH)flags|=0x0001;SetWindowPos(card.Source.Handle,IntPtr.Zero,px,py,pw,ph,flags);card.PixelX=px;card.PixelY=py;card.PixelW=pw;card.PixelH=ph;}
            if(card.Thumb==IntPtr.Zero)return;
            int width=(int)Math.Round(card.W*scale);
            int height=(int)Math.Round(card.H*scale);if(width==card.ThumbW&&height==card.ThumbH)return;card.ThumbH=height;
            card.ThumbW=width;
            int pad=(int)Math.Round(Inset*scale);
            int title=(int)Math.Round(TitleHeight*scale);
            var properties=new ThumbnailProperties {
                Flags=RectFlag|OpacityFlag|VisibleFlag|SourceClientFlag,
                Destination=new RECT {
                    Left=pad,Top=pad+title,
                    Right=(int)Math.Round(card.W*scale)-pad,
                    Bottom=(int)Math.Round(card.H*scale)-pad
                },
                Opacity=255,Visible=true,SourceClientAreaOnly=false
            };
            DwmUpdateThumbnailProperties(card.Thumb,ref properties);
        }

        public void Dispose() {
            if(disposed)return;
            disposed=true;
            try {StopFrames();pointerTimer.Stop();pointerTimer.Tick-=PollPointer;HideSnapHint();} catch {}
            foreach(Card card in cards) {
                try {if(card.Thumb!=IntPtr.Zero)DwmUnregisterThumbnail(card.Thumb);} catch {}
                try {if(card.Source!=null){card.Root.ReleaseMouseCapture();card.Source.Dispose();}} catch {}
            }
            cards.Clear();
            hovered=-1;
            var handler=Hidden;
            if(handler!=null)handler();
        }
    }
}
