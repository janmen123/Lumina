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
    public class GlassWindow : Window {
        protected Grid Root=new Grid();
        protected Border Surface=new Border();
        protected Grid ContentGrid=new Grid();
        protected bool LightTheme;
        protected double GlassPercent=72;
        protected bool MotionEnabled=true;
        protected double MotionLevel=1;
        protected double BlurRadius=18,EdgeStrength=1;
        protected MaterialOptions CurrentMaterial=new MaterialOptions();
        protected void ConfigureGlass(DockOptions options,MaterialOptions material=null){BlurRadius=options.BlurRadius;EdgeStrength=options.EdgeStrength;CurrentMaterial=(material??options.Material??new MaterialOptions()).Copy();}
        public void SetMaterial(MaterialOptions material){CurrentMaterial=material.Copy();SetGlassOpacity(GlassPercent);RefreshBackdropStyle();}
        protected double MaterialBlur{get{return CurrentMaterial.Kind==1?BlurRadius*.25:CurrentMaterial.Kind==2?Math.Min(40,BlurRadius*1.3):BlurRadius;}}
        protected static ControlTemplate GripTemplate(bool corner=false){var factory=new FrameworkElementFactory(corner?typeof(TextBlock):typeof(Border));if(corner){factory.SetValue(TextBlock.TextProperty,"◢");factory.SetValue(TextBlock.ForegroundProperty,new SolidColorBrush(Color.FromArgb(135,180,195,215)));factory.SetValue(TextBlock.FontSizeProperty,18.0);}else{factory.SetValue(Border.CornerRadiusProperty,new CornerRadius(3));factory.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromArgb(90,180,195,215)));}return new ControlTemplate(typeof(Thumb)){VisualTree=factory};}
        protected Brush TextBrush { get { return new SolidColorBrush(LightTheme?Color.FromRgb(25,28,34):Color.FromRgb(247,247,249)); } }
        protected Brush MutedBrush { get { return new SolidColorBrush(LightTheme?Color.FromRgb(65,69,77):Color.FromRgb(184,188,198)); } }
        public GlassWindow(double radius) {
            WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=Brushes.Transparent;
            ResizeMode=ResizeMode.NoResize; ShowInTaskbar=false; Topmost=false;UseLayoutRounding=true;SnapsToDevicePixels=true;
            Content=Root; Root.Margin=new Thickness(14);
            IsVisibleChanged+=(s,e)=>{if(!IsVisible)return;var widget=this as WidgetWindow;if(this is DockWindow||widget!=null||this is ControlOrbWindow)AnimateOpen(widget!=null?Math.Min(140,60+widget.PanelId*25):this is ControlOrbWindow?40:0);};
            Surface.CornerRadius=new CornerRadius(radius); Surface.BorderThickness=new Thickness(1);
            Surface.Effect=new DropShadowEffect { BlurRadius=25,ShadowDepth=7,Opacity=.28,Color=Colors.Black };
            Root.Children.Add(Surface); Root.Children.Add(ContentGrid);
            // Keep all glass windows recordable. A capture-protected window can stop NVIDIA desktop recording.
            SourceInitialized+=(s,e)=> {if(this is DockWindow||this is BoardWindow||this is WidgetWindow||this is ControlOrbWindow||this is ControlCenterWindow||this is IslandWindow){DesktopWindowPolicy.Apply(this);DesktopPersistence.Attach(this);}try{if(!Native.SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle,0))Store.Log(new Win32Exception(Marshal.GetLastWin32Error(),"SetWindowDisplayAffinity: allow desktop capture"));}catch(Exception ex){Store.Log(ex);}};
        }
        protected Button Button(string label,RoutedEventHandler click,double width=80) {
            var b=new Button {Content=label,Width=width,Height=36,Margin=new Thickness(4),Foreground=TextBrush,Cursor=Cursors.Hand};
            var factory=new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.CornerRadiusProperty,new CornerRadius(12));
            factory.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromArgb(LightTheme?(byte)38:(byte)25,255,255,255)));
            factory.SetValue(Border.BorderBrushProperty,new SolidColorBrush(Color.FromArgb(42,255,255,255)));
            factory.SetValue(Border.BorderThicknessProperty,new Thickness(1));
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center); factory.AppendChild(presenter);
            b.Template=new ControlTemplate(typeof(Button)) {VisualTree=factory};
            b.MouseEnter+=(s,e)=> {if(MotionEnabled)b.BeginAnimation(OpacityProperty,new DoubleAnimation(.78,TimeSpan.FromMilliseconds(120)));else{b.BeginAnimation(OpacityProperty,null);b.Opacity=.78;}};
            b.MouseLeave+=(s,e)=> {if(MotionEnabled)b.BeginAnimation(OpacityProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(150)));else{b.BeginAnimation(OpacityProperty,null);b.Opacity=1;}};
            b.PreviewMouseLeftButtonDown+=(s,e)=>{if(MotionEnabled)b.BeginAnimation(OpacityProperty,new DoubleAnimation(.6,TimeSpan.FromMilliseconds(65)));else{b.BeginAnimation(OpacityProperty,null);b.Opacity=.6;}};
            b.PreviewMouseLeftButtonUp+=(s,e)=>{if(MotionEnabled)b.BeginAnimation(OpacityProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(100)));else{b.BeginAnimation(OpacityProperty,null);b.Opacity=1;}};
            // Some buttons attach a handler after creation; WPF rejects a null routed-event handler.
            if(click!=null) b.Click+=click;
            return b;
        }
        string backdropCaptureKey;DateTime backdropCaptured=DateTime.MinValue;
        public void Backdrop(bool light) {
            LightTheme=light;
            SetGlassOpacity(GlassPercent);
            Surface.BorderBrush=GlassPercent==0?(Brush)Brushes.Transparent:new LinearGradientBrush(Color.FromArgb((byte)Math.Min(255,145*EdgeStrength),255,255,255),Color.FromArgb((byte)Math.Min(255,35*EdgeStrength),255,255,255),90);
            // A visible window must not photograph itself. Reuse its hidden-time backdrop and update the styling.
            if(IsVisible){RefreshBackdropStyle();return;}
            string captureKey=Left+"/"+Top+"/"+Width+"/"+Height;
            if(backdropCaptureKey==captureKey&&(DateTime.UtcNow-backdropCaptured).TotalSeconds<2&&Root.Children.OfType<Border>().Any(b=>b.Tag as string=="backdrop")){RefreshBackdropStyle();return;}
            Root.Children.OfType<Border>().Where(b=>b.Tag as string=="backdrop").ToList().ForEach(b=>Root.Children.Remove(b));
            if(this is BoardWindow||Width<40 || Height<40||GlassPercent==0) return;
            try {
                // Capture while this window is still hidden; no capture-protection flags or continuous capture.
                Matrix m=Matrix.Identity;
                new WindowInteropHelper(this).EnsureHandle();
                var src=PresentationSource.FromVisual(this); if(src!=null) m=src.CompositionTarget.TransformToDevice;
                int x=(int)Math.Round((Left+14)*m.M11),y=(int)Math.Round((Top+14)*m.M22);
                int w=Math.Max(1,(int)Math.Ceiling((Width-28)*m.M11)),h=Math.Max(1,(int)Math.Ceiling((Height-28)*m.M22));
                using(var bmp=new Drawing.Bitmap(w,h)) {
                    using(var g=Drawing.Graphics.FromImage(bmp)) g.CopyFromScreen(x,y,0,0,new Drawing.Size(w,h));
                    using(var small=new Drawing.Bitmap(bmp,Math.Max(1,w/8),Math.Max(1,h/8))) {
                        var data=small.LockBits(new Drawing.Rectangle(0,0,small.Width,small.Height),Drawing.Imaging.ImageLockMode.ReadOnly,Drawing.Imaging.PixelFormat.Format32bppArgb);
                        byte[] pixels=new byte[small.Width*small.Height*4];
                        try{for(int row=0;row<small.Height;row++)Marshal.Copy(IntPtr.Add(data.Scan0,row*data.Stride),pixels,row*small.Width*4,small.Width*4);}finally{small.UnlockBits(data);}
                        var image=BitmapSource.Create(small.Width,small.Height,96,96,PixelFormats.Bgra32,null,pixels,small.Width*4);image.Freeze();
                        var bg=new Border {Tag="backdrop",CornerRadius=Surface.CornerRadius,Background=new ImageBrush(image){Stretch=Stretch.Fill},Opacity=light?.8:.66};
                        bg.Clip=new RectangleGeometry(new Rect(0,0,Width-28,Height-28),Surface.CornerRadius.TopLeft,Surface.CornerRadius.TopLeft);
                        bg.Effect=new BlurEffect {Radius=MaterialBlur,RenderingBias=RenderingBias.Performance};
                        // Blur stays inside a rounded clipping host, leaving the corners transparent.
                        var host=new Border {Tag="backdrop",Clip=bg.Clip,Child=bg,Opacity=GlassPercent/100};
                        Root.Children.Insert(0,host);backdropCaptureKey=captureKey;backdropCaptured=DateTime.UtcNow;
                    }
                }
            } catch(Exception ex) { Store.Log(ex); }
        }
        void RefreshBackdropStyle(){foreach(var host in Root.Children.OfType<Border>().Where(b=>b.Tag as string=="backdrop")){var clip=new RectangleGeometry(new Rect(0,0,Math.Max(1,Width-28),Math.Max(1,Height-28)),Surface.CornerRadius.TopLeft,Surface.CornerRadius.TopLeft);host.Clip=clip;var bg=host.Child as Border;if(bg!=null){bg.Clip=clip;bg.CornerRadius=Surface.CornerRadius;bg.Opacity=LightTheme?.8:.66;bg.Effect=new BlurEffect{Radius=MaterialBlur,RenderingBias=RenderingBias.Performance};}}}
        protected void ReleaseBackdrop(){Root.Children.OfType<Border>().Where(b=>b.Tag as string=="backdrop").ToList().ForEach(b=>Root.Children.Remove(b));}
        public void SetGlassOpacity(double percent) {
            GlassPercent=LayoutRules.GlassOpacity(percent);
            Surface.Background=GlassMaterials.Fill(CurrentMaterial,LightTheme,GlassPercent);
            Surface.BorderBrush=GlassPercent==0?Brushes.Transparent:Polish.Line(LightTheme);Surface.Effect=GlassPercent==0||!CurrentMaterial.Shadow?null:new DropShadowEffect{BlurRadius=25,ShadowDepth=7,Opacity=.28,Color=Colors.Black};
            foreach(var b in Root.Children.OfType<Border>().Where(b=>b.Tag as string=="backdrop"))b.Opacity=GlassPercent/100;
            var material=Root.Children.OfType<Border>().FirstOrDefault(b=>b.Tag as string=="material");if(material==null){material=new Border{Tag="material",IsHitTestVisible=false};Root.Children.Insert(Math.Min(1,Root.Children.Count),material);}material.CornerRadius=Surface.CornerRadius;var effects=new Grid();material.Child=effects;material.Opacity=GlassPercent/100;
            if(CurrentMaterial.Gloss>0)effects.Children.Add(new Border{CornerRadius=Surface.CornerRadius,Background=new LinearGradientBrush(Color.FromArgb((byte)Math.Round(CurrentMaterial.Gloss*90),255,255,255),Colors.Transparent,90)});
            if(CurrentMaterial.Grain>0)effects.Children.Add(new Border{CornerRadius=Surface.CornerRadius,Background=new ImageBrush(GlassMaterials.Grain()){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,32,32)},Opacity=CurrentMaterial.Grain});
        }
        public virtual bool WantsTopmost{get{return Topmost;}}
        public virtual bool YieldToFullscreen{get{return true;}}
        public void RefreshLayer(){var handle=new WindowInteropHelper(this).Handle;if(handle==IntPtr.Zero)return;bool wanted=!DesktopPersistence.ForceDesktop(this)&&WantsTopmost&&(!YieldToFullscreen||!FullscreenState.Active(handle));if(Topmost!=wanted)Topmost=wanted;}
        protected string OpenStyle="fade";
        protected void AnimateOpen(int delayMs=0) {
            BeginAnimation(OpacityProperty,null);Opacity=1;Root.RenderTransform=Transform.Identity;
            ContentGrid.BeginAnimation(OpacityProperty,null);ContentGrid.Opacity=1;ContentGrid.RenderTransform=Transform.Identity;
            if(!MotionEnabled){ContentGrid.CacheMode=null;return;}
            string style=InteractionRules.OpenStyle(OpenStyle);bool software=(RenderCapability.Tier>>16)==0;
            var duration=TimeSpan.FromMilliseconds(software?100:style=="fade"?140:180);var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
            int token=++openAnimationToken;ContentGrid.CacheMode=software||style=="fade"?null:new BitmapCache(1){SnapsToDevicePixels=true};
            var fade=new DoubleAnimation(.18,1,duration){FillBehavior=FillBehavior.Stop};
            fade.Completed+=(s,e)=>{if(token==openAnimationToken)ContentGrid.CacheMode=null;};ContentGrid.BeginAnimation(OpacityProperty,fade,HandoffBehavior.SnapshotAndReplace);
            if(software)return;
            if(style=="rise"||style=="slide"){
                var move=new TranslateTransform();ContentGrid.RenderTransform=move;
                move.BeginAnimation(style=="rise"?TranslateTransform.YProperty:TranslateTransform.XProperty,new DoubleAnimation(style=="rise"?8:12,0,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
            }else if(style=="zoom"){
                ContentGrid.RenderTransformOrigin=new Point(.5,.5);var scale=new ScaleTransform(1,1);ContentGrid.RenderTransform=scale;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(.975,1,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
                scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(.975,1,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
            }
        }
        int openAnimationToken;
    }
}
