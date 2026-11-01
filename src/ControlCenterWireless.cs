using System;using System.Linq;using System.Collections.Generic;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Animation;
namespace Lumina {
    public sealed partial class ControlCenterWindow {
        new Button Button(string label,RoutedEventHandler click,double width=80){
            var button=base.Button(label,click,width);button.Background=Polish.Color("#24FFFFFF");button.BorderBrush=Polish.Color("#30FFFFFF");button.Padding=new Thickness(8,4,8,4);
            var frame=new FrameworkElementFactory(typeof(Border));frame.Name="Frame";frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(12));frame.SetValue(Border.BorderThicknessProperty,new Thickness(1));
            foreach(var pair in new[]{new{Property=Border.BackgroundProperty,Name="Background"},new{Property=Border.BorderBrushProperty,Name="BorderBrush"},new{Property=Border.PaddingProperty,Name="Padding"}})frame.SetBinding(pair.Property,new System.Windows.Data.Binding(pair.Name){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
            var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetBinding(FrameworkElement.HorizontalAlignmentProperty,new System.Windows.Data.Binding("HorizontalContentAlignment"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);frame.AppendChild(content);
            var template=new ControlTemplate(typeof(Button)){VisualTree=frame};var focus=new Trigger{Property=IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,Polish.Color("#E6FFFFFF"),"Frame"));template.Triggers.Add(focus);var hover=new Trigger{Property=IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BorderBrushProperty,Polish.Color("#A0FFFFFF"),"Frame"));template.Triggers.Add(hover);var disabled=new Trigger{Property=IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(OpacityProperty,.4,"Frame"));template.Triggers.Add(disabled);button.Template=template;return button;
        }
        public static readonly DependencyProperty PanelGlyphSizeProperty=DependencyProperty.Register("PanelGlyphSize",typeof(double),typeof(ControlCenterWindow),new PropertyMetadata(24.0));
        public double PanelGlyphSize{get{return (double)GetValue(PanelGlyphSizeProperty);}set{SetValue(PanelGlyphSizeProperty,value);}}
        sealed class IconScaleConverter:System.Windows.Data.IValueConverter {
            public object Convert(object value,Type target,object parameter,System.Globalization.CultureInfo culture){return (double)value*(double)parameter;}
            public object ConvertBack(object value,Type target,object parameter,System.Globalization.CultureInfo culture){return System.Windows.Data.Binding.DoNothing;}
        }
        static readonly IconScaleConverter iconScale=new IconScaleConverter();
        Viewbox PanelIcon(string kind,double scale=1){var icon=ControlIcons.Create(kind,Polish.Color("#F2FFFFFF"),24);var size=new System.Windows.Data.Binding("PanelGlyphSize"){Source=this,Converter=iconScale,ConverterParameter=scale};icon.SetBinding(WidthProperty,size);icon.SetBinding(HeightProperty,size);return icon;}
        public void RefreshIconSizes(){PanelGlyphSize=C.Prefs.ControlCenter.PanelIconSize;}
        Button IconAction(string kind,string text,Action action,double width){var button=ActionButton("",action,width);var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(PanelIcon(kind,.65));row.Children.Add(new TextBlock{Text=text,Margin=new Thickness(7,0,0,0),VerticalAlignment=VerticalAlignment.Center});button.Content=row;return button;}
        sealed class WirelessCard {
            public string Kind;public Button Expand,Toggle;public TextBlock State;public FrameworkElement Arrow;public StackPanel List;public Border Details;public bool Open;public int Generation;
        }
        readonly List<WirelessCard> wirelessCards=new List<WirelessCard>();
        void BuildWirelessCards(){
            wirelessCards.Clear();
            AddWirelessCard("WiFi","Wi-Fi");AddWirelessCard("Bluetooth","蓝牙");
        }
        void AddWirelessCard(string kind,string title){
            var card=new WirelessCard{Kind=kind};wirelessCards.Add(card);
            var shell=new StackPanel();var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var label=new Grid();label.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});label.ColumnDefinitions.Add(new ColumnDefinition());label.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});
            var symbol=PanelIcon(kind=="WiFi"?"wifi":"bluetooth");symbol.HorizontalAlignment=HorizontalAlignment.Left;label.Children.Add(symbol);
            var words=new StackPanel{VerticalAlignment=VerticalAlignment.Center};words.Children.Add(new TextBlock{Text=title,FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White});card.State=new TextBlock{Text="读取中",FontSize=11,Foreground=Polish.Color("#D9FFFFFF"),Margin=new Thickness(0,3,0,0)};words.Children.Add(card.State);Grid.SetColumn(words,1);label.Children.Add(words);
            card.Arrow=PanelIcon("chevron",.6);card.Arrow.RenderTransformOrigin=new Point(.5,.5);card.Arrow.RenderTransform=new RotateTransform();Grid.SetColumn(card.Arrow,2);label.Children.Add(card.Arrow);
            card.Expand=Button("",(s,e)=>ExpandWireless(card),210);card.Expand.HorizontalAlignment=HorizontalAlignment.Stretch;card.Expand.Width=double.NaN;card.Expand.Height=66;card.Expand.Content=label;card.Expand.HorizontalContentAlignment=HorizontalAlignment.Stretch;card.Expand.Padding=new Thickness(12,8,8,8);card.Expand.Background=Brushes.Transparent;card.Expand.ToolTip="展开"+title+"列表";row.Children.Add(card.Expand);
            card.Toggle=Button("—",(s,e)=>ToggleRadio(kind),58);card.Toggle.Height=32;card.Toggle.VerticalAlignment=VerticalAlignment.Center;card.Toggle.ToolTip="开启或关闭"+title;Grid.SetColumn(card.Toggle,1);row.Children.Add(card.Toggle);shell.Children.Add(row);
            card.List=new StackPanel{Margin=new Thickness(12,2,12,12)};card.Details=new Border{Child=card.List,Visibility=Visibility.Collapsed,BorderThickness=new Thickness(0,1,0,0),BorderBrush=Polish.Color("#26FFFFFF")};shell.Children.Add(card.Details);
            body.Children.Add(new Border{Child=shell,CornerRadius=new CornerRadius(18),Background=Polish.Color("#18000000"),BorderBrush=Polish.Color("#30FFFFFF"),BorderThickness=new Thickness(1),Margin=new Thickness(0,0,0,10)});
        }
        void UpdateWirelessCards(){foreach(var card in wirelessCards){var radios=(snapshot.Radios??new List<RadioSnapshot>()).Where(r=>r.Kind==card.Kind).ToList();bool on=radios.Any(r=>r.Enabled);card.State.Text=InteractionRules.WirelessState(snapshot,card.Kind);card.State.ToolTip=string.Join("\n",snapshot.Errors??new List<string>());card.Toggle.Content=on?"开":"关";card.Toggle.Background=on?Polish.Color("#66FFFFFF"):Polish.Color("#24000000");card.Toggle.IsEnabled=!command&&radios.Any(r=>r.Available);}}
        void ExpandWireless(WirelessCard card){
            card.Open=!card.Open;card.Generation++;((RotateTransform)card.Arrow.RenderTransform).Angle=card.Open?180:0;card.Details.Visibility=card.Open?Visibility.Visible:Visibility.Collapsed;
            if(!card.Open)return;
            if(MotionEnabled)card.Details.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)));
            LoadWireless(card);
        }
        TextBlock WirelessMessage(string text){return new TextBlock{Text=text,Foreground=Polish.Color("#D9FFFFFF"),FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,10,4,10)};}
        async void LoadWireless(WirelessCard card){
            int generation=++card.Generation;card.List.Children.Clear();card.List.Children.Add(WirelessMessage("正在查找…"));
            try{
                var result=await C.Systems.Send(new BridgeRequest{Action=card.Kind=="WiFi"?"networks":"bluetooth",Enabled=true});
                if(stopped||page!="home"||!card.Open||generation!=card.Generation||!wirelessCards.Contains(card))return;
                card.List.Children.Clear();if(!string.IsNullOrWhiteSpace(result.Warning))card.List.Children.Add(WirelessMessage(result.Warning));
                if(card.Kind=="WiFi"){
                    var networks=(result.Networks??new List<WifiNetwork>()).OrderByDescending(n=>n.Connected).ThenByDescending(n=>n.Signal).ToList();
                    foreach(var network in networks){var selected=network;string detail=network.Connected?"已连接 · 点击断开":(network.Secure?"安全网络":"开放网络")+" · 信号 "+network.Signal+"%";card.List.Children.Add(WirelessItem(network.Connected?"check":"wifi",network.Name,detail,()=>Connect(selected)));}
                    if(networks.Count==0)card.List.Children.Add(WirelessMessage("未发现网络，请开启 Wi-Fi 后刷新。"));
                }else{
                    var devices=(result.Devices??new List<BluetoothDevice>()).OrderByDescending(d=>d.Connected).ThenBy(d=>d.Name).ToList();
                    foreach(var device in devices){card.List.Children.Add(WirelessItem("bluetooth",device.Name,device.Connected?"已连接":"已配对 · 点击管理连接",()=>Settings("ms-settings:bluetooth")));}
                    if(devices.Count==0)card.List.Children.Add(WirelessMessage("暂无已配对设备"));
                }
                var actions=new Grid{Margin=new Thickness(0,8,0,0)};actions.ColumnDefinitions.Add(new ColumnDefinition());actions.ColumnDefinitions.Add(new ColumnDefinition());
                var refresh=IconAction("refresh","刷新",()=>LoadWireless(card),115);actions.Children.Add(refresh);
                var manage=IconAction(card.Kind=="WiFi"?"settings":"plus",card.Kind=="WiFi"?"网络设置":"添加设备",()=>Settings(card.Kind=="WiFi"?"ms-settings:network-wifi":"ms-settings:bluetooth"),135);Grid.SetColumn(manage,1);actions.Children.Add(manage);card.List.Children.Add(actions);
            }catch(Exception ex){if(stopped||!card.Open||generation!=card.Generation)return;Store.Trace("Control center wireless: "+ex.Message);card.List.Children.Clear();var message=WirelessMessage("读取受限："+ex.GetBaseException().Message);message.ToolTip=ex.Message;card.List.Children.Add(message);card.List.Children.Add(ActionButton("重试",()=>LoadWireless(card),120));card.List.Children.Add(ActionButton("复制 AI 排查信息",()=>{modal=true;try{C.ShowAiHelp(this,card.Kind,ex.ToString());}finally{modal=false;}},180));card.List.Children.Add(ActionButton("打开系统设置",()=>Settings(card.Kind=="WiFi"?"ms-settings:network-wifi":"ms-settings:bluetooth"),160));}
        }
        Button WirelessItem(string glyph,string name,string detail,Action action){
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30)});row.ColumnDefinitions.Add(new ColumnDefinition());
            var symbol=PanelIcon(glyph,.8);symbol.HorizontalAlignment=HorizontalAlignment.Left;row.Children.Add(symbol);
            var text=new StackPanel();text.Children.Add(new TextBlock{Text=name,FontSize=13,Foreground=Brushes.White,TextTrimming=TextTrimming.CharacterEllipsis});text.Children.Add(new TextBlock{Text=detail,FontSize=10,Foreground=Polish.Color("#CCFFFFFF"),Margin=new Thickness(0,4,0,0),TextTrimming=TextTrimming.CharacterEllipsis});Grid.SetColumn(text,1);row.Children.Add(text);
            var button=ActionButton("",action);button.Width=double.NaN;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Height=60;button.Padding=new Thickness(10,7,10,7);button.Content=row;button.ToolTip=name+"\n"+detail;return button;
        }
        void QueueWirelessRefresh(string kind){Dispatcher.BeginInvoke(new Action(()=>{foreach(var card in wirelessCards.Where(c=>c.Open&&c.Kind==kind))LoadWireless(card);}));}
    }
}
