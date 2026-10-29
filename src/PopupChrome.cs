using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GlassBoard {
    // Shared popup material. Sample only the app's own visual once on opening;
    // never capture the desktop, poll pixels, or blur menu text.
    public static class PopupChrome {
        public static readonly DependencyProperty MaterialProperty=DependencyProperty.RegisterAttached("Material",typeof(int),typeof(PopupChrome),new PropertyMetadata(-1,MaterialChanged));
        public static int GetMaterial(DependencyObject o){return (int)o.GetValue(MaterialProperty);}
        public static void SetMaterial(DependencyObject o,int v){o.SetValue(MaterialProperty,v);}
        static readonly Dictionary<bool,Style> scrollStyles=new Dictionary<bool,Style>();
        public static Brush Tint(bool light){
            var brush=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(light?"#EEFCFDFF":"#ED293747"),(Color)ColorConverter.ConvertFromString(light?"#E8EFF4FA":"#E8212C3A"),90);brush.Freeze();return brush;
        }
        static void MaterialChanged(DependencyObject d,DependencyPropertyChangedEventArgs e){
            var surface=d as Border;if(surface==null)return;
            surface.Background=Tint((int)e.NewValue==1);
            if((int)e.OldValue<0)surface.Loaded+=SurfaceLoaded;
        }
        static Window Owner(FrameworkElement surface){
            var owner=surface.TemplatedParent as FrameworkElement;
            var combo=owner as ComboBox;if(combo!=null)return Window.GetWindow(combo);
            while(owner is MenuItem)owner=ItemsControl.ItemsControlFromItemContainer(owner);
            var menu=owner as ContextMenu;
            return menu!=null&&menu.PlacementTarget!=null?Window.GetWindow(menu.PlacementTarget):null;
        }
        static void SurfaceLoaded(object sender,RoutedEventArgs e){
            var surface=(Border)sender;bool light=GetMaterial(surface)==1;surface.Background=Tint(light);
            if(SystemParameters.MenuAnimation&&!SystemParameters.HighContrast)surface.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.65,1,TimeSpan.FromMilliseconds(100)){FillBehavior=FillBehavior.Stop});
            surface.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{
                if(!surface.IsLoaded)return;
                var owner=Owner(surface);var content=owner==null?null:owner.Content as FrameworkElement;
                double w=surface.ActualWidth,h=surface.ActualHeight;
                if(content==null||!content.IsVisible||w<1||h<1||w>900||h>800)return;
                try{
                    var point=content.PointFromScreen(surface.PointToScreen(new Point()));
                    var region=new Rect(point.X,point.Y,w,h);
                    // Outside the owner (e.g. a Dock menu), keep the readable tinted fallback.
                    if(!new Rect(0,0,content.ActualWidth,content.ActualHeight).Contains(region))return;
                    var sample=new Border{Width=w/2,Height=h/2,Background=new VisualBrush(content){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=region,Stretch=Stretch.Fill},Effect=new BlurEffect{Radius=7,RenderingBias=RenderingBias.Performance}};
                    sample.Measure(new Size(w/2,h/2));sample.Arrange(new Rect(0,0,w/2,h/2));
                    var bitmap=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(w/2)),Math.Max(1,(int)Math.Ceiling(h/2)),96,96,PixelFormats.Pbgra32);bitmap.Render(sample);bitmap.Freeze();
                    var drawing=new DrawingGroup();using(var dc=drawing.Open()){dc.DrawImage(bitmap,new Rect(0,0,w,h));dc.DrawRectangle(Tint(light),null,new Rect(0,0,w,h));}drawing.Freeze();
                    var material=new DrawingBrush(drawing){Stretch=Stretch.Fill};material.Freeze();surface.Background=material;
                }catch(InvalidOperationException){/* Popup closed or lost its presentation source. */}
            }));
        }
        public static Style ScrollStyle(bool light){
            Style cached;if(scrollStyles.TryGetValue(light,out cached))return cached;
            string common=@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'>
<Grid Background='Transparent'><Track x:Name='PART_Track' Orientation='@ORIENTATION' IsDirectionReversed='@REVERSED' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{Binding Value,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}' ViewportSize='{TemplateBinding ViewportSize}'>
<Track.DecreaseRepeatButton><RepeatButton Focusable='False' Command='{x:Static ScrollBar.@DECREASE}'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
<Track.Thumb><Thumb MinHeight='16' MinWidth='16'><Thumb.Template><ControlTemplate TargetType='Thumb'><Border CornerRadius='3' Margin='@MARGIN' Background='@COLOR'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
<Track.IncreaseRepeatButton><RepeatButton Focusable='False' Command='{x:Static ScrollBar.@INCREASE}'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
</Track></Grid></ControlTemplate>";
            Func<bool,ControlTemplate> template=horizontal=>(ControlTemplate)XamlReader.Parse(common.Replace("@ORIENTATION",horizontal?"Horizontal":"Vertical").Replace("@REVERSED",horizontal?"False":"True").Replace("@DECREASE",horizontal?"PageLeftCommand":"PageUpCommand").Replace("@INCREASE",horizontal?"PageRightCommand":"PageDownCommand").Replace("@MARGIN",horizontal?"0,2":"2,0").Replace("@COLOR",light?"#9965758A":"#99C5D7EB").Replace("MinHeight='16' MinWidth='16'",horizontal?"MinWidth='20'":"MinHeight='20'"));
            var style=new Style(typeof(ScrollBar));style.Setters.Add(new Setter(FrameworkElement.WidthProperty,8.0));style.Setters.Add(new Setter(UIElement.OpacityProperty,.3));style.Setters.Add(new Setter(UIElement.FocusableProperty,false));style.Setters.Add(new Setter(Control.TemplateProperty,template(false)));
            var hover=new DataTrigger{Binding=new System.Windows.Data.Binding("IsMouseOver"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(ScrollViewer),1)},Value=true};hover.Setters.Add(new Setter(UIElement.OpacityProperty,1.0));style.Triggers.Add(hover);
            var focused=new Trigger{Property=UIElement.IsKeyboardFocusWithinProperty,Value=true};focused.Setters.Add(new Setter(UIElement.OpacityProperty,1.0));style.Triggers.Add(focused);
            var horizontalTrigger=new Trigger{Property=ScrollBar.OrientationProperty,Value=Orientation.Horizontal};horizontalTrigger.Setters.Add(new Setter(FrameworkElement.WidthProperty,double.NaN));horizontalTrigger.Setters.Add(new Setter(FrameworkElement.HeightProperty,8.0));horizontalTrigger.Setters.Add(new Setter(Control.TemplateProperty,template(true)));style.Triggers.Add(horizontalTrigger);
            style.Seal();scrollStyles[light]=style;return style;
        }
    }
}
