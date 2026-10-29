using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GlassBoard {
    // Hover cards for Dock icons: a small glass panel with stacked window cards,
    // like the phone recents switcher. Windows are captured with PrintWindow so the
    // card chrome, rounded corners and stacking order stay under our control.
    public sealed class ThumbnailPopup : IDisposable {
        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd,IntPtr hdc,uint flags);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr dc,int width,int height);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle,int command);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr handle);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr hwnd,ref WindowCompositionData data);
        [StructLayout(LayoutKind.Sequential)] struct RECT {public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)] struct AccentPolicy {public int State;public int Flags;public int GradientColor;public int AnimationId;}
        [StructLayout(LayoutKind.Sequential)] struct WindowCompositionData {public int Attribute;public IntPtr Data;public int SizeOfData;}

        const int AcrylicBlurBehind=4;
        const int WcaAccentPolicy=19;
        const double CardWidth=300,Pad=12,StaggerY=14,StaggerX=22,TitleBar=34,MinImage=132,MaxImage=230;
        const int RefreshMs=1500;

        public event Action<IntPtr> Clicked;
        public event Action Hidden;
        public event Action PointerOut;
        public bool PointerInside {get {return pointerInside;}}

        readonly HwndSource source;
        readonly Canvas canvas=new Canvas();
        readonly List<Card> cards=new List<Card>();
        readonly DispatcherTimer refresh;
        bool pointerInside;
        bool disposed;

        sealed class Card {
            public IntPtr Handle;
            public Border Root;
            public Image Image;
            public TextBlock Title;
            public double Width,X,Y;
            public int ImageHeight;
        }

        public bool IsUsable {get {return source!=null&&source.Handle!=IntPtr.Zero&&cards.Count>0;}}
        public IntPtr Handle {get {return source==null?IntPtr.Zero:source.Handle;}}

        public ThumbnailPopup(IList<RunningWindow> windows,double left,double top,double scale) {
            if(windows==null||windows.Count==0)return;
            if(windows.Count>5)windows=new List<RunningWindow>(windows).GetRange(0,5);
            if(scale<=0)scale=1;
            var captures=new List<BitmapSource>();
            var aspects=new List<double>();
            for(int i=0;i<windows.Count;i++) {
                BitmapSource image=Capture(windows[i].Handle);
                double aspect=1.6;
                RECT rect;
                if(GetWindowRect(windows[i].Handle,out rect)&&rect.Right>rect.Left&&rect.Bottom>rect.Top)aspect=Math.Max(.6,Math.Min(2.4,(double)(rect.Bottom-rect.Top)/(rect.Right-rect.Left)));
                captures.Add(image);aspects.Add(aspect);
            }
            int count=windows.Count;
            int commonHeight=ImageHeight(windows,scale);
            Size target=Measure(windows,scale);
            int width=(int)Math.Round(target.Width),height=(int)Math.Round(target.Height);
            var parameters=new HwndSourceParameters("GlassBoardThumbnails") {
                Width=width,Height=height,WindowStyle=unchecked((int)0x80000000),
                ExtendedWindowStyle=0x00080000|0x08000000|0x00000008,
                PositionX=(int)Math.Round(left),PositionY=(int)Math.Round(top)
            };
            source=new HwndSource(parameters);
            if(source.Handle==IntPtr.Zero)return;
            ApplyGlass(source.Handle);
            var host=new Grid {Background=Brushes.Transparent};
            int corner=2;try {DwmSetWindowAttribute(source.Handle,33,ref corner,4);} catch(Exception ex) {Store.Log(ex);}
            var panel=new Border {
                Background=new SolidColorBrush(Color.FromArgb(0xE0,0x12,0x17,0x20)),
                CornerRadius=new CornerRadius(16),
                BorderBrush=new SolidColorBrush(Color.FromArgb(0x30,0xFF,0xFF,0xFF)),
                BorderThickness=new Thickness(1),
                Child=canvas
            };
            host.Children.Add(panel);
            host.MouseEnter+=(s,e)=>pointerInside=true;
            host.MouseLeave+=(s,e)=>{pointerInside=false;var handler=PointerOut;if(handler!=null)handler();};
            source.RootVisual=host;
            ShowWindow(source.Handle,5);
            host.Measure(new Size(width,height));
            host.Arrange(new Rect(0,0,width,height));
            for(int index=0;index<count;index++) {
                Card card=BuildCard(windows[index],captures[index],commonHeight,index,count,scale);
                canvas.Children.Add(card.Root);
                cards.Add(card);
            }
            refresh=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(RefreshMs)};
            refresh.Tick+=(s,e)=>RefreshCaptures();
            refresh.Start();
            AnimateIn();
        }

        // Same geometry as the constructor, so the Dock can place the panel before it exists.
        public static Size Measure(IList<RunningWindow> windows,double scale) {
            if(windows==null||windows.Count==0)return new Size(0,0);
            if(scale<=0)scale=1;
            int count=Math.Min(5,windows.Count);
            int tallest=0;
            int commonHeight=ImageHeight(windows,scale);
            for(int i=0;i<count;i++) {
                int cardBottom=(int)Math.Round(Pad+i*StaggerY+commonHeight+(i==0?TitleBar:0));
                if(cardBottom>tallest)tallest=cardBottom;
            }
            return new Size(Math.Round((Pad*2+CardWidth+(count-1)*StaggerX)*scale),Math.Round((tallest+Pad)*scale));
        }

        // Every card shares one picture height, so the deck reads as an even stack.
        static int ImageHeight(IList<RunningWindow> windows,double scale) {
            int count=Math.Min(5,windows.Count);
            int tallest=0;
            for(int i=0;i<count;i++) {
                double aspect=1.6;
                RECT rect;
                if(GetWindowRect(windows[i].Handle,out rect)&&rect.Right>rect.Left&&rect.Bottom>rect.Top)aspect=Math.Max(.6,Math.Min(2.4,(double)(rect.Bottom-rect.Top)/(rect.Right-rect.Left)));
                int cellImage=(int)Math.Round(CardWidth*aspect);
                cellImage=Math.Max((int)MinImage,Math.Min((int)MaxImage,cellImage));
                if(cellImage>tallest)tallest=cellImage;
            }
            return tallest;
        }

        Card BuildCard(RunningWindow window,BitmapSource image,int imageHeight,int index,int count,double scale) {
            var visual=new Image {Width=CardWidth,Height=imageHeight,Stretch=Stretch.UniformToFill};
            if(image!=null)visual.Source=image;
            var title=new TextBlock {
                Text=string.IsNullOrEmpty(window.Title)?"窗口":window.Title,
                Foreground=new SolidColorBrush(Color.FromArgb(0xE6,0xFF,0xFF,0xFF)),
                FontSize=11.5,VerticalAlignment=VerticalAlignment.Center,
                Margin=new Thickness(12,0,12,0),TextTrimming=TextTrimming.CharacterEllipsis
            };
            var titleBar=new Border {
                Height=TitleBar,Background=new SolidColorBrush(Color.FromArgb(0xB0,0x0B,0x10,0x17)),Child=title
            };
            var body=new StackPanel();
            body.Children.Add(visual);
            // Only the front card carries a caption; the cards behind it peek as photo rims.
            if(index==0)body.Children.Add(titleBar);
            var root=new Border {
                Width=CardWidth,CornerRadius=new CornerRadius(11),
                Background=new SolidColorBrush(Color.FromArgb(0xEE,0x16,0x1C,0x26)),
                BorderBrush=new SolidColorBrush(Color.FromArgb(0x38,0xFF,0xFF,0xFF)),
                BorderThickness=new Thickness(1),
                Child=body,
                Cursor=System.Windows.Input.Cursors.Hand,
                RenderTransformOrigin=new Point(.5,.5),
                RenderTransform=new TranslateTransform(0,10),
                Opacity=0
            };
            root.Clip=new RectangleGeometry(new Rect(0,0,CardWidth,imageHeight+(index==0?TitleBar:0)),11,11);
            int depth=count-1-index;
            double x=Pad+index*StaggerX;
            double y=Pad+index*StaggerY;
            Canvas.SetLeft(root,x);
            Canvas.SetTop(root,y);
            Canvas.SetZIndex(root,depth);
            IntPtr handle=window.Handle;
            root.MouseLeftButtonUp+=(s,e)=>{e.Handled=true;var handler=Clicked;if(handler!=null)handler(handle);};
            root.MouseEnter+=(s,e)=>Raise(root,depth,true,x,y);
            root.MouseLeave+=(s,e)=>Raise(root,depth,false,x,y);
            return new Card {Handle=handle,Root=root,Image=visual,Title=title,Width=CardWidth,ImageHeight=imageHeight,X=x,Y=y};
        }

        void Raise(Border root,int depth,bool up,double x,double y) {
            var transform=root.RenderTransform as TranslateTransform;
            if(transform==null)return;
            var duration=TimeSpan.FromMilliseconds(160);
            double targetX=up?Pad-x:0;
            double targetY=up?Pad-y:0;
            transform.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(targetX,duration){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
            transform.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(targetY,duration){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
            Canvas.SetZIndex(root,up?40:depth);
            root.BorderBrush=new SolidColorBrush(Color.FromArgb(up?(byte)0x9A:(byte)0x38,0xFF,0xFF,0xFF));
        }

        void AnimateIn() {
            for(int index=0;index<cards.Count;index++) {
                Border root=cards[index].Root;
                var translate=root.RenderTransform as TranslateTransform;
                double start=(translate==null?0:translate.Y);
                var ease=new CubicEase {EasingMode=EasingMode.EaseOut};
                var fade=new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)){BeginTime=TimeSpan.FromMilliseconds(index*40),EasingFunction=ease};
                root.BeginAnimation(UIElement.OpacityProperty,fade);
                if(translate!=null)translate.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(start+8,0,TimeSpan.FromMilliseconds(200)){BeginTime=TimeSpan.FromMilliseconds(index*40),EasingFunction=ease});
            }
        }

        void RefreshCaptures() {
            if(disposed)return;
            foreach(Card card in cards) {
                try {
                    BitmapSource image=Capture(card.Handle);
                    if(image!=null)card.Image.Source=image;
                } catch(Exception ex) {Store.Log(ex);}
            }
        }

        static BitmapSource Capture(IntPtr hwnd) {
            if(hwnd==IntPtr.Zero)return null;
            RECT rect;
            if(!GetWindowRect(hwnd,out rect))return null;
            int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;
            if(width<=0||height<=0||width>12000||height>12000)return null;
            int targetWidth=(int)Math.Min(900,width);
            int targetHeight=Math.Max(1,(int)Math.Round((double)height*targetWidth/width));
            IntPtr screen=GetDC(IntPtr.Zero);
            IntPtr memory=CreateCompatibleDC(screen);
            IntPtr bitmap=CreateCompatibleBitmap(screen,targetWidth,targetHeight);
            IntPtr previous=SelectObject(memory,bitmap);
            bool captured=PrintWindow(hwnd,memory,2);
            if(!captured)captured=PrintWindow(hwnd,memory,0);
            SelectObject(memory,previous);
            BitmapSource result=null;
            try {
                if(captured)result=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
            } finally {
                DeleteObject(bitmap);DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen);
            }
            if(result!=null)result.Freeze();
            return result;
        }

        static void ApplyGlass(IntPtr hwnd) {
            try {
                var policy=new AccentPolicy {State=AcrylicBlurBehind,Flags=0,GradientColor=unchecked((int)0xCC0F141C),AnimationId=0};
                int size=Marshal.SizeOf(typeof(AccentPolicy));
                IntPtr buffer=Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(policy,buffer,false);
                var data=new WindowCompositionData {Attribute=WcaAccentPolicy,Data=buffer,SizeOfData=size};
                SetWindowCompositionAttribute(hwnd,ref data);
                Marshal.FreeHGlobal(buffer);
            } catch(Exception ex) {Store.Log(ex);}
        }



        public IntPtr HandleOf(int index) {return index>=0&&index<cards.Count?cards[index].Handle:IntPtr.Zero;}

        public void Dispose() {
            if(disposed)return;
            disposed=true;
            if(refresh!=null)refresh.Stop();
            cards.Clear();
            var handler=Hidden;
            try {if(source!=null&&source.Handle!=IntPtr.Zero)DestroyWindow(source.Handle);} catch(Exception ex) {Store.Log(ex);}
            if(handler!=null)handler();
        }
    }
}
