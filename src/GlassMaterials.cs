using System;using System.Runtime.Serialization;using System.Linq;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;using System.Windows.Media.Effects;
namespace Lumina {
    public static class GlassMaterials {
        static ImageSource grain;
        public static Brush Fill(MaterialOptions options,bool light,double percent){if(percent<=0)return Brushes.Transparent;var color=string.IsNullOrEmpty(options.Tint)?(light?Color.FromRgb(234,237,244):Color.FromRgb(24,26,32)):(Color)ColorConverter.ConvertFromString(options.Tint);color.A=(byte)Math.Round(percent*255/100);if(options.Kind!=3&&options.Kind!=1)return new SolidColorBrush(color);var top=color;top.R=(byte)Math.Min(255,top.R+35);top.G=(byte)Math.Min(255,top.G+35);top.B=(byte)Math.Min(255,top.B+35);var bottom=color;if(options.Kind==1){top.A=(byte)(color.A*.65);bottom.A=(byte)(color.A*.25);}return new LinearGradientBrush(top,bottom,65);}
        public static ImageSource Grain(){if(grain!=null)return grain;var bytes=new byte[32*32*4];uint state=912367;for(int i=0;i<bytes.Length;i+=4){state=state*1664525+1013904223;byte value=(byte)(state>>24);bytes[i]=bytes[i+1]=bytes[i+2]=value;bytes[i+3]=60;}var image=BitmapSource.Create(32,32,96,96,PixelFormats.Bgra32,null,bytes,32*4);image.Freeze();grain=image;return image;}
    }
    public partial class Controller {
        void BuildMaterialEditor(StackPanel panel,string title,MaterialOptions options,Action changed){
            options.Normalize();panel.Children.Add(new TextBlock{Text=title,FontSize=17,Margin=new Thickness(0,18,0,8)});
            var types=new ComboBox{ItemsSource=new[]{"磨砂玻璃","清透玻璃","亚克力","渐变玻璃"},SelectedIndex=options.Kind,Margin=new Thickness(0,0,0,8)};panel.Children.Add(types);types.SelectionChanged+=(s,e)=>{if(types.SelectedIndex<0)return;options.Kind=types.SelectedIndex;changed();};
            var tint=new TextBox{Text=options.Tint,MinHeight=36,ToolTip="输入 #RRGGBB，例如 #334A70；留空使用当前主题颜色"};panel.Children.Add(new TextBlock{Text="材质色调（#RRGGBB，留空随主题）",Margin=new Thickness(0,6,0,5)});panel.Children.Add(tint);panel.Children.Add(FeatureUi.Button("应用色调",()=>{string text=tint.Text.Trim();if(text.Length>0&&!System.Text.RegularExpressions.Regex.IsMatch(text,@"^#[a-fA-F0-9]{6}$")){MessageBox.Show("请输入 #RRGGBB 格式的颜色，例如 #334A70。","Lumina");return;}options.Tint=text;changed();}));
            Action<string,double,Action<double>> slider=(label,value,set)=>{var caption=new TextBlock{Text=label+" "+Math.Round(value*100)+"%",Margin=new Thickness(0,8,0,4)};panel.Children.Add(caption);var control=new Slider{Minimum=0,Maximum=1,Value=value,TickFrequency=.05,IsSnapToTickEnabled=true};panel.Children.Add(control);control.ValueChanged+=(s,e)=>{set(control.Value);caption.Text=label+" "+Math.Round(control.Value*100)+"%";changed();};};slider("反光",options.Gloss,v=>options.Gloss=v);slider("细纹理",options.Grain,v=>options.Grain=v);
            var shadow=new CheckBox{Content="显示材质阴影",IsChecked=options.Shadow,Margin=new Thickness(0,10,0,5)};panel.Children.Add(shadow);shadow.Checked+=(s,e)=>{options.Shadow=true;changed();};shadow.Unchecked+=(s,e)=>{options.Shadow=false;changed();};
        }
    }
}
