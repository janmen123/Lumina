using System;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
namespace GlassBoard {
    public partial class WidgetOptions {
        [DataMember] public bool IslandEnabled,IslandGlass;
        [DataMember(EmitDefaultValue=false)] public bool? IslandMotion;
        [DataMember(EmitDefaultValue=false)] public double? IslandTransparency,IslandLeft,IslandTop;
    }
    // A separate window lets the island stay compact without consuming one of the four widget panels.
    public sealed class IslandWindow:GlassWindow {
        readonly WidgetManager manager;
        readonly StackPanel content=new StackPanel();
        readonly Grid header=new Grid();
        readonly TextBlock title=new TextBlock(), subtitle=new TextBlock(), time=new TextBlock();
        readonly Border artworkFrame=new Border();
        readonly Image artwork=new Image{Stretch=Stretch.UniformToFill};
        readonly Button playback=new Button();
        readonly StackPanel mediaControls=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
        Point start;bool down,dragged,media;ImageSource lastArtwork;
        public override bool WantsTopmost{get{return false;}}
        public IslandWindow(WidgetManager m):base(28) {
            manager=m;Title="GlassBoard 灵动岛";Width=132;Height=68;Root.Margin=new Thickness(10);Surface.Effect=null;
            Surface.CornerRadius=new CornerRadius(26);Surface.BorderThickness=new Thickness(1);
            ContentGrid.Children.Add(content);
            header.Height=48;header.Margin=new Thickness(13,0,13,0);header.Cursor=Cursors.Hand;
            header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            artworkFrame.Width=32;artworkFrame.Height=32;artworkFrame.CornerRadius=new CornerRadius(10);artworkFrame.Background=new SolidColorBrush(Color.FromArgb(40,255,255,255));artworkFrame.Margin=new Thickness(0,0,10,0);artworkFrame.Clip=new RectangleGeometry(new Rect(0,0,32,32),8,8);artworkFrame.Child=artwork;header.Children.Add(artworkFrame);
            var meta=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(meta,1);header.Children.Add(meta);
            title.Foreground=Brushes.White;title.FontSize=13;title.FontWeight=FontWeights.SemiBold;title.TextTrimming=TextTrimming.CharacterEllipsis;meta.Children.Add(title);
            subtitle.Foreground=new SolidColorBrush(Color.FromArgb(170,255,255,255));subtitle.FontSize=10;subtitle.TextTrimming=TextTrimming.CharacterEllipsis;meta.Children.Add(subtitle);
            time.Foreground=Brushes.White;time.FontFamily=new FontFamily("Segoe UI");time.FontSize=16;time.FontWeight=FontWeights.SemiBold;time.VerticalAlignment=VerticalAlignment.Center;time.Margin=new Thickness(8,0,0,0);Grid.SetColumn(time,3);header.Children.Add(time);
            content.Children.Add(header);
            mediaControls.Children.Add(ActionButton("◀◀",()=>Act("previous"),29));playback.Content="▶";playback.Width=29;playback.Height=30;playback.Margin=new Thickness(1);playback.FontSize=11;playback.Click+=(s,e)=>Act("toggle");mediaControls.Children.Add(playback);mediaControls.Children.Add(ActionButton("▶▶",()=>Act("next"),29));Grid.SetColumn(mediaControls,2);header.Children.Add(mediaControls);
            header.MouseLeftButtonDown+=(s,e)=>{down=true;dragged=false;start=e.GetPosition(this);header.CaptureMouse();};
            header.MouseMove+=(s,e)=>{if(!down||e.LeftButton!=MouseButtonState.Pressed)return;Point p=e.GetPosition(this);if(Math.Abs(p.X-start.X)+Math.Abs(p.Y-start.Y)<5)return;down=false;dragged=true;header.ReleaseMouseCapture();if(manager.C.Prefs.Widgets.Locked)return;try{DragMove();var a=DesktopWindowPolicy.WorkArea(this,true);Left=FeatureRules.Position(Left,a.Left,a.Width,Width);Top=FeatureRules.Position(Top,a.Top,a.Height,Height);manager.C.Prefs.Widgets.IslandLeft=Left;manager.C.Prefs.Widgets.IslandTop=Top;manager.C.Save();}catch(InvalidOperationException){}};
            header.MouseLeftButtonUp+=(s,e)=>{if(!down)return;down=false;header.ReleaseMouseCapture();if(!dragged&&manager.Music!=null&&!media)manager.Music.Wake();};
            ContextMenuOpening+=(s,e)=>{ContextMenu=CreateMenu();};
            Loaded+=(s,e)=>{var a=DesktopWindowPolicy.WorkArea(this,true);var o=manager.C.Prefs.Widgets;Left=FeatureRules.Position(o.IslandLeft??(a.Left+(a.Width-Width)/2),a.Left,a.Width,Width);Top=FeatureRules.Position(o.IslandTop??(a.Top+8),a.Top,a.Height,Height);RefreshLayer();};
            RefreshOptions();RefreshData();
        }
        Button ActionButton(string label,Action action,double width){var b=Button(label,(s,e)=>action(),width);b.Height=30;b.Margin=new Thickness(1);b.FontSize=11;return b;}
        void Act(string action){if(manager.Music!=null)manager.Music.Action(action);}
        ContextMenu CreateMenu(){var menu=MenuTheme.Create(false);var black=new MenuItem{Header="纯黑胶囊",IsCheckable=true,IsChecked=!manager.C.Prefs.Widgets.IslandGlass};black.Click+=(s,e)=>{manager.C.Prefs.Widgets.IslandGlass=false;RefreshOptions();manager.C.Save();};menu.Items.Add(black);var glass=new MenuItem{Header="透明玻璃",IsCheckable=true,IsChecked=manager.C.Prefs.Widgets.IslandGlass};glass.Click+=(s,e)=>{manager.C.Prefs.Widgets.IslandGlass=true;RefreshOptions();manager.C.Save();};menu.Items.Add(glass);menu.Items.Add(new MenuItem{Header="玻璃透明度（100% 全透明）",IsEnabled=false});var slider=new Slider{Width=180,Minimum=0,Maximum=100,Value=manager.C.Prefs.Widgets.IslandTransparency??30,TickFrequency=5,IsSnapToTickEnabled=true};slider.ValueChanged+=(s,e)=>{manager.C.Prefs.Widgets.IslandTransparency=slider.Value;RefreshOptions();};menu.Items.Add(new MenuItem{Header=slider,StaysOpenOnClick=true});var motion=new MenuItem{Header="展开动画",IsCheckable=true,IsChecked=manager.C.Prefs.Widgets.IslandMotion??true};motion.Click+=(s,e)=>{manager.C.Prefs.Widgets.IslandMotion=motion.IsChecked;manager.C.Save();};menu.Items.Add(motion);var settings=new MenuItem{Header="组件设置"};settings.Click+=(s,e)=>manager.C.ConfigureWidgets();menu.Items.Add(settings);var close=new MenuItem{Header="关闭灵动岛"};close.Click+=(s,e)=>{manager.C.Prefs.Widgets.IslandEnabled=false;manager.C.Save();manager.QueueSync();};menu.Items.Add(close);menu.Closed+=(s,e)=>manager.C.Save();return menu;}
        public void RefreshOptions(){var o=manager.C.Prefs.Widgets;MotionEnabled=o.IslandMotion??true;double trans=LayoutRules.Clamp(o.IslandTransparency??30,0,100,30);Surface.Background=o.IslandGlass?new SolidColorBrush(Color.FromArgb((byte)Math.Round(255*(1-trans/100)),39,46,52)):new SolidColorBrush(Color.FromRgb(4,5,7));Surface.BorderBrush=o.IslandGlass?new SolidColorBrush(Color.FromArgb((byte)Math.Round(110*(1-trans/100)),255,255,255)):Brushes.Transparent;}
        public void RefreshData(){var m=manager.Music;bool have=m!=null&&(m.Local?m.Playing||!string.IsNullOrEmpty(m.Album):m.Session.Available)&&!string.IsNullOrWhiteSpace(m.Title);var current=have?m.Artwork??m.AppIcon:null;if(current!=lastArtwork){lastArtwork=current;artwork.Source=current;}artworkFrame.Visibility=have?Visibility.Visible:Visibility.Collapsed;mediaControls.Visibility=have?Visibility.Visible:Visibility.Collapsed;Grid.SetColumn(time,have?3:0);Grid.SetColumnSpan(time,have?1:4);time.HorizontalAlignment=have?HorizontalAlignment.Right:HorizontalAlignment.Center;time.Margin=have?new Thickness(8,0,0,0):new Thickness(0);title.Text=have?m.Title:"";subtitle.Text=have?m.Artist:"";subtitle.Visibility=have?Visibility.Visible:Visibility.Collapsed;time.Text=DateTime.Now.ToString("HH:mm");playback.Content=have&&m.Playing?"Ⅱ":"▶";playback.ToolTip=have&&m.Playing?"暂停":"播放";if(media!=have){media=have;ResizeIsland();}}
        void ResizeIsland(){double width=media?365:132,height=68;ResizeTo(width,height);}
        void ResizeTo(double width,double height){double oldWidth=Width;double targetLeft=Left;if(IsLoaded&&!double.IsNaN(Left))targetLeft=Left-(width-oldWidth)/2;Animate(Window.WidthProperty,width);Animate(Window.HeightProperty,height);if(IsLoaded&&!double.IsNaN(targetLeft))Animate(Window.LeftProperty,targetLeft);}
        void Animate(DependencyProperty property,double value){double from=(double)GetValue(property);BeginAnimation(property,null);SetValue(property,value);if(MotionEnabled&&IsLoaded&&!double.IsNaN(from)){var a=new DoubleAnimation(from,value,TimeSpan.FromMilliseconds(180)){FillBehavior=FillBehavior.Stop,EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};BeginAnimation(property,a);}}
    }
}
