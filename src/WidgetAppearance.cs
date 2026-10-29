using System;using System.Linq;using System.Globalization;using System.Windows;using System.Windows.Controls;using System.Windows.Controls.Primitives;using System.Windows.Input;using System.Windows.Media;using System.Windows.Media.Effects;using System.Windows.Shapes;
namespace GlassBoard {
    public sealed partial class WidgetWindow {
        TextBlock weekday,clockHours,clockMinutes;string clockKey="";
        WidgetStyle StyleFor(string kind){return M.C.Prefs.Widgets.Style(kind);}
        bool BareShell{get{return Kinds.Any(k=>StyleFor(k).Frameless);}}
        string AppearanceSignature{get{return string.Join("|",Kinds.Select(k=>StyleFor(k).Signature()))+"/"+M.C.Prefs.Widgets.Locked;}}
        Border widgetInputArea;
        void InitializeBareDragging(){
            // Layered Windows windows pass fully transparent pixels through to the desktop.
            // One alpha step keeps the complete widget rectangle reachable, including
            // whitespace and the 14-DIP perimeter, without covering child controls.
            widgetInputArea=new Border{Tag="widgetInputArea",Margin=new Thickness(-14),Background=new SolidColorBrush(Color.FromArgb(1,0,0,0))};Root.Children.Insert(0,widgetInputArea);
            Root.PreviewMouseLeftButtonDown+=(a,e)=>{if(!BareShell||M.C.Prefs.Widgets.Locked||e.ClickCount!=1||e.ButtonState!=MouseButtonState.Pressed)return;var source=e.OriginalSource as DependencyObject;while(source!=null&&source!=Root){if(source is ButtonBase||source is TextBoxBase||source is ComboBox||source is Thumb||source is ScrollBar)return;source=source is Visual?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);}if(source!=Root)return;e.Handled=true;try{DragMove();var area=DesktopWindowPolicy.WorkArea(this);Left=FeatureRules.Position(Left,area.Left,area.Width,Width);Top=FeatureRules.Position(Top,area.Top,area.Height,Height);var pos=M.C.Prefs.Widgets.Position(PanelId);pos.Left=Left;pos.Top=Top;M.C.Save();}catch(InvalidOperationException){}};
        }
        Brush StyleInk(string kind){var s=StyleFor(kind);var b=s.TextColor==""?(M.C.Prefs.Light?Polish.Ink(true):Brushes.White):(Brush)new BrushConverter().ConvertFromString(s.TextColor);var solid=b as SolidColorBrush;if(solid!=null){var c=solid.Color;c.A=(byte)Math.Round(255*s.TextOpacity/100);var result=new SolidColorBrush(c);result.Freeze();return result;}return b;}
        void PaintText(DependencyObject root,WidgetStyle s,Brush ink){
            var text=root as TextBlock;if(text!=null){text.Foreground=ink;text.Opacity=1;text.Effect=s.TextShadow?new DropShadowEffect{BlurRadius=3,ShadowDepth=1,Opacity=.65,Color=Colors.Black}:null;}
            var control=root as Control;if(control!=null){control.Foreground=ink;}
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)PaintText(VisualTreeHelper.GetChild(root,i),s,ink);
        }
        void PaintCard(Border card,string kind){PaintText(card,StyleFor(kind),StyleInk(kind));}
        void ApplyCardAppearance(){if(stack==null)return;foreach(var card in stack.Children.OfType<Border>()){string kind=card.Tag as string;if(!WidgetRules.Kinds.Contains(kind))continue;var style=StyleFor(kind);double opacity=WidgetSizing.Background(M.C.Prefs.Widgets,M.C.Prefs.Widgets.Position(PanelId));card.Background=style.Frameless||opacity==0?null:GlassMaterials.Fill(CurrentMaterial,LightTheme,opacity);card.BorderBrush=style.Frameless?null:Polish.Line(LightTheme);card.BorderThickness=new Thickness(style.Frameless?0:1);PaintCard(card,kind);}if(BareShell){ReleaseBackdrop();Surface.Visibility=Visibility.Collapsed;foreach(var host in Root.Children.OfType<Border>().Where(b=>b.Tag as string=="material"))host.Visibility=Visibility.Collapsed;}else{Surface.Visibility=Visibility.Visible;foreach(var host in Root.Children.OfType<Border>().Where(b=>b.Tag as string=="material"))host.Visibility=Visibility.Visible;}}
        void AddAppearanceMenu(ContextMenu menu,string kind){var item=new MenuItem{Header="外观 DIY · 文字颜色 / 无框 / 样式"};item.Click+=(s,e)=>M.C.CustomizeWidgets(kind,this);menu.Items.Insert(0,item);var bare=new MenuItem{Header="无框模式",IsCheckable=true,IsChecked=StyleFor(kind).Frameless};bare.Click+=(s,e)=>{StyleFor(kind).Frameless=bare.IsChecked;M.C.Save();M.Sync();};menu.Items.Insert(1,bare);}
        void ClockDesign(StackPanel parent){
            var s=StyleFor("clock");ResetAdvancedClock();weekday=clockHours=clockMinutes=null;
            clock=new TextBlock{FontSize=s.ClockSize,FontWeight=FontWeights.Light,Foreground=TextBrush};date=new TextBlock{FontSize=13,Margin=new Thickness(2,4,0,0),Foreground=MutedBrush};clockStatus=new TextBlock{FontSize=11,Margin=new Thickness(2,9,0,0),TextTrimming=TextTrimming.CharacterEllipsis,Foreground=MutedBrush};
            if(!string.IsNullOrEmpty(s.Font))clock.FontFamily=new FontFamily(s.Font);
            var body=new StackPanel{Cursor=Cursors.SizeAll};
            if(s.ClockStyle>=5){AdvancedClock(body,s);}else if(s.ClockStyle==1){
                var overlap=new Grid{Height=126};weekday=new TextBlock{FontFamily=new FontFamily("Segoe Script"),FontSize=70,FontStyle=FontStyles.Italic,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
                overlap.Children.Add(new Viewbox{Child=weekday,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,0,8),MaxHeight=112});
                var front=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,18,0,0)};clock.HorizontalAlignment=date.HorizontalAlignment=HorizontalAlignment.Center;front.Children.Add(clock);front.Children.Add(date);overlap.Children.Add(front);body.Children.Add(overlap);
            }else if(s.ClockStyle==4){
                var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};clockHours=new TextBlock{FontSize=s.ClockSize,FontFamily=clock.FontFamily,FontWeight=FontWeights.Light};clockMinutes=new TextBlock{FontSize=s.ClockSize,FontFamily=clock.FontFamily,FontWeight=FontWeights.Light};
                foreach(var t in new[]{clockHours,clockMinutes})row.Children.Add(new Border{CornerRadius=new CornerRadius(12),Padding=new Thickness(14,8,14,8),Margin=new Thickness(3),Background=new SolidColorBrush(Color.FromArgb(35,150,160,180)),Child=t});body.Children.Add(row);date.HorizontalAlignment=HorizontalAlignment.Center;body.Children.Add(date);
            }else{if(s.ClockStyle==3){weekday=new TextBlock{FontSize=28,FontWeight=FontWeights.SemiBold};body.Children.Add(weekday);}if(s.ClockStyle==2){clock.FontWeight=FontWeights.UltraLight;clock.HorizontalAlignment=HorizontalAlignment.Center;date.HorizontalAlignment=HorizontalAlignment.Center;}body.Children.Add(clock);body.Children.Add(date);}
            date.Visibility=s.ShowDate?Visibility.Visible:Visibility.Collapsed;if(!s.Frameless)body.Children.Add(clockStatus);
            body.MouseLeftButtonDown+=(a,e)=>{if(e.ClickCount!=1||M.C.Prefs.Widgets.Locked)return;e.Handled=true;try{DragMove();var area=DesktopWindowPolicy.WorkArea(this);Left=FeatureRules.Position(Left,area.Left,area.Width,Width);Top=FeatureRules.Position(Top,area.Top,area.Height,Height);var pos=M.C.Prefs.Widgets.Position(PanelId);pos.Left=Left;pos.Top=Top;M.C.Save();Backdrop(LightTheme);ApplyWidgetGlass();}catch(InvalidOperationException){}};parent.Children.Add(body);
        }
        static bool SetText(TextBlock text,string value){if(text==null||text.Text==value)return false;text.Text=value;return true;}
        void ClockTick(){var s=StyleFor("clock");var now=DateTime.Now;var key=now.ToString("yyyyMMddHHmm");bool running=M.C.Prefs.Widgets.TimerDueUtc.HasValue;if(!running&&key==clockKey)return;clockKey=key;var culture=CultureInfo.GetCultureInfo("en-US");SetText(clock,now.ToString(s.Hour24?"HH:mm":"hh:mm tt",culture));SetText(clockHours,now.ToString(s.Hour24?"HH":"hh"));SetText(clockMinutes,now.ToString("mm")+(s.Hour24||s.ClockStyle==13?"":" "+now.ToString("tt",culture)));if(s.ClockStyle>=5)AdvancedClockTick(now,s);else{SetText(date,s.ClockStyle==1?now.ToString("dd MMMM",culture):now.ToString("M月d日  dddd"));SetText(weekday,now.ToString("dddd",culture));}if(weekday!=null)weekday.Opacity=s.ClockStyle==1?.3:1;if(clockStatus!=null){SetText(clockStatus,ClockRules.Display(M.C.Prefs.Widgets,DateTime.UtcNow));clockStatus.ToolTip=clockStatus.Text;}}
    }
}
