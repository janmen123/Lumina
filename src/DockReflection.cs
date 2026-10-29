using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace GlassBoard {
    internal static class DockReflection {
        internal static double Height(DockOptions o){return o.IconSize*o.ReflectionLength/100;}
        internal static double ExtraHeight(DockOptions o){return o.Reflection?Height(o)+o.ReflectionGap+o.ReflectionBlur*2:0;}
        internal static double SidePadding(DockOptions o){return o.Reflection?Math.Abs(Math.Tan(o.ReflectionAngle*Math.PI/180))*Height(o)+o.ReflectionBlur*2+8:0;}
        internal static FrameworkElement Create(FrameworkElement visual,DockOptions o){
            if(!o.Reflection||visual==null)return null;
            double fraction=o.ReflectionLength/100,height=Height(o),fade=o.ReflectionFade/100;
            // Reflect the bottom crop at 1:1 scale. Keep the top edge anchored while leaning the lower edge.
            var brush=new VisualBrush(visual){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.RelativeToBoundingBox,
                Viewbox=new Rect(0,1-fraction,1,fraction),RelativeTransform=new ScaleTransform(1,-1,.5,.5),Opacity=o.ReflectionOpacity/100};
            var mask=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(0,1)};
            mask.GradientStops.Add(new GradientStop(Colors.White,0));
            mask.GradientStops.Add(new GradientStop(Color.FromArgb(105,255,255,255),fade*.48));
            mask.GradientStops.Add(new GradientStop(Colors.Transparent,fade));mask.Freeze();
            var reflected=new Border{Width=o.IconSize,Height=height,Background=brush,OpacityMask=mask,IsHitTestVisible=false,
                VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,o.ReflectionGap,0,0),RenderTransform=new SkewTransform(o.ReflectionAngle,0)};
            if(o.ReflectionBlur>0)reflected.Effect=new BlurEffect{Radius=o.ReflectionBlur,RenderingBias=RenderingBias.Performance};
            var host=new Grid{Width=o.IconSize,Height=ExtraHeight(o),IsHitTestVisible=false};host.Children.Add(reflected);return host;
        }
        internal static void Preset(DockOptions o,int index){
            o.ReflectionOpacity=index==1?65:index==2?38:45;
            o.ReflectionLength=index==1?55:index==2?48:38;
            o.ReflectionAngle=index==2?-24:0;
            o.ReflectionBlur=index==1?0:index==2?2.5:1.5;
            o.ReflectionGap=index==1?1:2;
            o.ReflectionFade=index==1?100:index==2?75:85;
        }
    }
    public partial class Controller {
        void BuildReflectionEditor(StackPanel parent,Func<DockOptions> options,Action live){
            bool updating=false;
            var enabled=new CheckBox{Content="Dock 图标倒影",IsChecked=options().Reflection,Foreground=Brushes.White,Margin=new Thickness(0,14,0,8)};
            parent.Children.Add(enabled);
            var body=new StackPanel{Margin=new Thickness(14,0,14,12),IsEnabled=options().Reflection};
            var expander=new Expander{Header="倒影细节 · 角度、质感与预设",Foreground=Brushes.White,IsExpanded=true,Content=body,Margin=new Thickness(0,0,0,12)};
            parent.Children.Add(expander);
            Action toggle=()=>{body.IsEnabled=enabled.IsChecked==true;if(updating)return;options().Reflection=body.IsEnabled;live();};
            enabled.Checked+=(s,e)=>toggle();enabled.Unchecked+=(s,e)=>toggle();
            body.Children.Add(new TextBlock{Text="边调边看 Dock。倾斜角度改变倒影方向，搭配柔化与渐隐形成不同光感。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,8,0,8)});
            var presets=new WrapPanel();body.Children.Add(presets);
            var controls=new Slider[6];
            Func<int,string,double,double,double,double,string,Action<double>,Slider> add=(index,label,min,max,value,tick,unit,change)=>{
                var caption=new TextBlock{Text=label+"  "+value.ToString("0.#")+unit,Foreground=Brushes.White,Margin=new Thickness(0,10,0,4)};body.Children.Add(caption);
                var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=tick,IsSnapToTickEnabled=true,Margin=new Thickness(0,0,0,3)};body.Children.Add(slider);controls[index]=slider;
                System.Windows.Automation.AutomationProperties.SetName(slider,label);
                slider.ValueChanged+=(s,e)=>{caption.Text=label+"  "+slider.Value.ToString("0.#")+unit;if(updating)return;change(slider.Value);live();};return slider;
            };
            add(0,"倒影浓度",0,100,options().ReflectionOpacity,5,"%",v=>options().ReflectionOpacity=v);
            add(1,"倒影长度",15,75,options().ReflectionLength,1,"%",v=>options().ReflectionLength=v);
            add(2,"倾斜角度（负值向左，正值向右）",-40,40,options().ReflectionAngle,1,"°",v=>options().ReflectionAngle=v);
            add(3,"柔化程度",0,6,options().ReflectionBlur,.5,"",v=>options().ReflectionBlur=v);
            add(4,"与图标的距离",0,12,options().ReflectionGap,1," px",v=>options().ReflectionGap=v);
            add(5,"渐隐终点（越小越快消散）",20,100,options().ReflectionFade,5,"%",v=>options().ReflectionFade=v);
            Action<int> preset=index=>{
                updating=true;
                try{var o=options();DockReflection.Preset(o,index);double[] values={o.ReflectionOpacity,o.ReflectionLength,o.ReflectionAngle,o.ReflectionBlur,o.ReflectionGap,o.ReflectionFade};for(int i=0;i<values.Length;i++)controls[i].Value=values[i];}
                finally{updating=false;}live();
            };
            presets.Children.Add(FeatureUi.Button("轻柔玻璃",()=>preset(0)));
            presets.Children.Add(FeatureUi.Button("清晰镜面",()=>preset(1)));
            presets.Children.Add(FeatureUi.Button("斜光掠影",()=>preset(2)));
            body.Children.Add(FeatureUi.Button("恢复默认倒影",()=>preset(0)));
        }
    }
}
