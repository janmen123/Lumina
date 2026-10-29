using System;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace GlassBoard {
    public partial class WidgetOptions {
        [DataMember] public bool IslandEnabled,IslandGlass;
        [DataMember(EmitDefaultValue=false)] public bool? IslandMotion;
        [DataMember(EmitDefaultValue=false)] public double? IslandTransparency,IslandLeft,IslandTop;
    }
    // A compact clock capsule that stays out of the four widget panels.
    public sealed class IslandWindow:GlassWindow {
        readonly WidgetManager manager;
        readonly StackPanel content=new StackPanel();
        readonly Grid header=new Grid();
        readonly TextBlock title=new TextBlock(), subtitle=new TextBlock(), time=new TextBlock();
        Point start;bool down,dragged;
        public override bool WantsTopmost{get{return false;}}
        public IslandWindow(WidgetManager m):base(28) {
            manager=m;Title="GlassBoard 灵动岛";Width=132;Height=68;Root.Margin=new Thickness(10);Surface.Effect=null;
            Surface.CornerRadius=new CornerRadius(26);Surface.BorderThickness=new Thickness(1);
            ContentGrid.Children.Add(content);
            header.Height=48;header.Margin=new Thickness(13,0,13,0);header.Cursor=Cursors.Hand;
            header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            title.Foreground=Brushes.White;title.FontSize=13;title.FontWeight=FontWeights.SemiBold;title.TextTrimming=TextTrimming.CharacterEllipsis;title.Text="GlassBoard";header.Children.Add(title);
            subtitle.Foreground=new SolidColorBrush(Color.FromArgb(170,255,255,255));subtitle.FontSize=10;subtitle.TextTrimming=TextTrimming.CharacterEllipsis;Grid.SetColumn(subtitle,1);subtitle.Margin=new Thickness(8,0,0,0);header.Children.Add(subtitle);
            time.Foreground=Brushes.White;time.FontFamily=new FontFamily("Segoe UI");time.FontSize=16;time.FontWeight=FontWeights.SemiBold;time.VerticalAlignment=VerticalAlignment.Center;time.Margin=new Thickness(8,0,0,0);Grid.SetColumn(time,2);header.Children.Add(time);
            content.Children.Add(header);
            header.MouseLeftButtonDown+=(s,e)=>{down=true;dragged=false;start=e.GetPosition(this);header.CaptureMouse();};
            header.MouseMove+=(s,e)=>{if(!down||e.LeftButton!=MouseButtonState.Pressed)return;Point p=e.GetPosition(this);if(Math.Abs(p.X-start.X)+Math.Abs(p.Y-start.Y)<5)return;down=false;dragged=true;header.ReleaseMouseCapture();if(manager.C.Prefs.Widgets.Locked)return;try{DragMove();var a=DesktopWindowPolicy.WorkArea(this,true);Left=FeatureRules.Position(Left,a.Left,a.Width,Width);Top=FeatureRules.Position(Top,a.Top,a.Height,Height);manager.C.Prefs.Widgets.IslandLeft=Left;manager.C.Prefs.Widgets.IslandTop=Top;manager.C.Save();}catch(InvalidOperationException){}};
            header.MouseLeftButtonUp+=(s,e)=>{if(!down)return;down=false;header.ReleaseMouseCapture();};
            ContextMenuOpening+=(s,e)=>{ContextMenu=CreateMenu();};
            Loaded+=(s,e)=>{var a=DesktopWindowPolicy.WorkArea(this,true);var o=manager.C.Prefs.Widgets;Left=FeatureRules.Position(o.IslandLeft??(a.Left+(a.Width-Width)/2),a.Left,a.Width,Width);Top=FeatureRules.Position(o.IslandTop??(a.Top+8),a.Top,a.Height,Height);RefreshLayer();};
            RefreshOptions();RefreshData();
        }
        ContextMenu CreateMenu(){var menu=MenuTheme.Create(false);var black=new MenuItem{Header="纯黑胶囊",IsCheckable=true,IsChecked=!manager.C.Prefs.Widgets.IslandGlass};black.Click+=(s,e)=>{manager.C.Prefs.Widgets.IslandGlass=false;RefreshOptions();manager.C.Save();};menu.Items.Add(black);var glass=new MenuItem{Header="透明玻璃",IsCheckable=true,IsChecked=manager.C.Prefs.Widgets.IslandGlass};glass.Click+=(s,e)=>{manager.C.Prefs.Widgets.IslandGlass=true;RefreshOptions();manager.C.Save();};menu.Items.Add(glass);menu.Items.Add(new MenuItem{Header="玻璃透明度（100% 全透明）",IsEnabled=false});var slider=new Slider{Width=180,Minimum=0,Maximum=100,Value=manager.C.Prefs.Widgets.IslandTransparency??30,TickFrequency=5,IsSnapToTickEnabled=true};slider.ValueChanged+=(s,e)=>{manager.C.Prefs.Widgets.IslandTransparency=slider.Value;RefreshOptions();};menu.Items.Add(new MenuItem{Header=slider,StaysOpenOnClick=true});var motion=new MenuItem{Header="展开动画",IsCheckable=true,IsChecked=manager.C.Prefs.Widgets.IslandMotion??true};motion.Click+=(s,e)=>{manager.C.Prefs.Widgets.IslandMotion=motion.IsChecked;manager.C.Save();};menu.Items.Add(motion);var settings=new MenuItem{Header="组件设置"};settings.Click+=(s,e)=>manager.C.ConfigureWidgets();menu.Items.Add(settings);var close=new MenuItem{Header="关闭灵动岛"};close.Click+=(s,e)=>{manager.C.Prefs.Widgets.IslandEnabled=false;manager.C.Save();manager.QueueSync();};menu.Items.Add(close);menu.Closed+=(s,e)=>manager.C.Save();return menu;}
        public void RefreshOptions(){var o=manager.C.Prefs.Widgets;MotionEnabled=o.IslandMotion??true;double trans=LayoutRules.Clamp(o.IslandTransparency??30,0,100,30);Surface.Background=o.IslandGlass?new SolidColorBrush(Color.FromArgb((byte)Math.Round(255*(1-trans/100)),39,46,52)):new SolidColorBrush(Color.FromRgb(4,5,7));Surface.BorderBrush=o.IslandGlass?new SolidColorBrush(Color.FromArgb((byte)Math.Round(110*(1-trans/100)),255,255,255)):Brushes.Transparent;}
        public void RefreshData(){time.Text=DateTime.Now.ToString("HH:mm");subtitle.Text=DateTime.Now.ToString("ddd");}
    }
}
