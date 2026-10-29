using System;using System.Linq;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Interop;
namespace GlassBoard {
    public partial class Controller {
        sealed class ColorOwner:System.Windows.Forms.IWin32Window {public IntPtr Handle{get;private set;}public ColorOwner(Window w){Handle=new WindowInteropHelper(w).Handle;}}
        public void CustomizeWidgets(string selected=null,Window owner=null){
            bool previous=Board.ModalBusy;Board.ModalBusy=true;
            try{
                var w=FeatureUi.Dialog("组件外观 DIY",owner??OwnerWindow,620,730);var root=new StackPanel{Margin=new Thickness(24)};w.Content=new ScrollViewer{Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
                root.Children.Add(new TextBlock{Text="颜色、无框与样式",FontSize=23,Margin=new Thickness(0,0,0,14)});
                var kind=new ComboBox{ItemsSource=WidgetRules.Names,SelectedIndex=Math.Max(0,Array.IndexOf(WidgetRules.Kinds,selected)),MinHeight=36};root.Children.Add(kind);
                var editor=new StackPanel{Margin=new Thickness(0,16,0,0)};root.Children.Add(editor);
                Action apply=()=>{Prefs.Widgets.NormalizeStyles();Save();RefreshWidgets();};
                Action fill=null;fill=()=>{
                    editor.Children.Clear();string k=WidgetRules.Kinds[Math.Max(0,kind.SelectedIndex)];var s=Prefs.Widgets.Style(k);
                    Action<string> label=text=>editor.Children.Add(new TextBlock{Text=text,Margin=new Thickness(0,12,0,6),TextWrapping=TextWrapping.Wrap});
                    Action<string,bool,Action<bool>> check=(text,value,set)=>{var b=new CheckBox{Content=text,IsChecked=value,Margin=new Thickness(0,7,0,7)};b.Click+=(a,e)=>{set(b.IsChecked==true);apply();};editor.Children.Add(b);};
                    Action<string,string[],int,Action<int>> combo=(text,items,index,set)=>{label(text);var b=new ComboBox{ItemsSource=items,SelectedIndex=index,MinHeight=36};b.SelectionChanged+=(a,e)=>{if(b.SelectedIndex>=0){set(b.SelectedIndex);apply();}};editor.Children.Add(b);};
                    Action<string,double,double,double,Action<double>> range=(text,min,max,value,set)=>{var title=new TextBlock{Text=text+" · "+Math.Round(value),Margin=new Thickness(0,12,0,6)};editor.Children.Add(title);var b=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=1,IsSnapToTickEnabled=true};b.ValueChanged+=(a,e)=>{title.Text=text+" · "+Math.Round(b.Value);};b.PreviewMouseLeftButtonUp+=(a,e)=>{set(b.Value);apply();};b.KeyUp+=(a,e)=>{set(b.Value);apply();};editor.Children.Add(b);};
                    label("文字颜色 · 留空使用主题默认色");var row=new WrapPanel();editor.Children.Add(row);var hex=new TextBox{Text=s.TextColor,Width=135,MinHeight=36,MaxLength=7,Margin=new Thickness(0,0,8,0)};row.Children.Add(hex);
                    row.Children.Add(FeatureUi.Button("调色板",()=>{using(var picker=new System.Windows.Forms.ColorDialog{FullOpen=true}){if(s.TextColor!="")picker.Color=System.Drawing.ColorTranslator.FromHtml(s.TextColor);if(picker.ShowDialog(new ColorOwner(w))==System.Windows.Forms.DialogResult.OK){s.TextColor=System.Drawing.ColorTranslator.ToHtml(picker.Color);s.TextColor="#"+picker.Color.R.ToString("X2")+picker.Color.G.ToString("X2")+picker.Color.B.ToString("X2");hex.Text=s.TextColor;apply();}}}));
                    row.Children.Add(FeatureUi.Button("应用色值",()=>{string color=WidgetStyle.Color(hex.Text);if(hex.Text.Trim()!=""&&color==""){MessageBox.Show(w,"请输入六位色值，例如 #FFFFFF。");return;}s.TextColor=color;hex.Text=color;apply();}));
                    var swatches=new WrapPanel{Margin=new Thickness(0,8,0,0)};editor.Children.Add(swatches);foreach(string c in new[]{"#FFFFFF","#161A22","#F3DBB3","#BBDFFF","#D8C5FF","#B8E4CE"}){string chosen=c;var b=new Button{Background=(Brush)new BrushConverter().ConvertFromString(c),Width=32,Height=28,Margin=new Thickness(3),ToolTip=c};b.Click+=(a,e)=>{s.TextColor=chosen;hex.Text=chosen;apply();};swatches.Children.Add(b);}
                    range("文字不透明度 %",0,100,s.TextOpacity,v=>s.TextOpacity=v);check("增强可读性 · 轻微文字阴影",s.TextShadow,v=>s.TextShadow=v);
                    check("无框模式 · 隐藏玻璃底板、边框、拖动条和角标",s.Frameless,v=>s.Frameless=v);
                    label("无框时可拖动文字区域移动组件。右键可重新进入 DIY，锁定后可从总设置解锁。");
                    var panel=Prefs.Widgets.Position(WidgetRules.Panel(Prefs.Widgets,k));bool compactWeather=false;range("所在面板宽度",compactWeather?96:240,1000,compactWeather?(panel.CompactWeatherWidth??panel.Width??240):(panel.Width??450),v=>{if(compactWeather)panel.CompactWeatherWidth=v;else panel.Width=v;});
                    if(k=="clock"){
                        combo("时钟样式 · 共 14 款",ClockCatalog.Names,s.ClockStyle,v=>s.ClockStyle=v);editor.Children.Add(FeatureUi.Button("使用无框推荐效果",()=>{s.Frameless=true;s.TextShadow=true;if(s.TextColor=="")s.TextColor="#FFFFFF";apply();fill();}));
                        label("时钟字体 · 可选择或填写已安装字体；留空使用样式默认字体");var font=new TextBox{Text=s.Font,MinHeight=36};editor.Children.Add(font);var fontList=new ComboBox{ItemsSource=Fonts.SystemFontFamilies.Select(f=>f.Source).OrderBy(x=>x).ToList(),MinHeight=34,Margin=new Thickness(0,6,0,0)};fontList.SelectionChanged+=(a,e)=>{if(fontList.SelectedItem!=null)font.Text=fontList.SelectedItem.ToString();};editor.Children.Add(fontList);editor.Children.Add(FeatureUi.Button("应用字体",()=>{s.Font=font.Text.Trim();apply();}));
                        range("时间字号",28,100,s.ClockSize,v=>s.ClockSize=v);check("显示日期",s.ShowDate,v=>s.ShowDate=v);check("24 小时制",s.Hour24,v=>s.Hour24=v);check("显示上午／下午标签（纵向大字海报）",s.ShowAmPm,v=>s.ShowAmPm=v);
                    }
                    var actions=new WrapPanel{Margin=new Thickness(0,16,0,0)};editor.Children.Add(actions);
                    actions.Children.Add(FeatureUi.Button("配色应用到全部组件",()=>{foreach(string key in WidgetRules.Kinds){var target=Prefs.Widgets.Style(key);target.TextColor=s.TextColor;target.TextOpacity=s.TextOpacity;target.TextShadow=s.TextShadow;}apply();}));
                    actions.Children.Add(FeatureUi.Button("无框状态应用到全部",()=>{foreach(string key in WidgetRules.Kinds)Prefs.Widgets.Style(key).Frameless=s.Frameless;apply();}));
                    actions.Children.Add(FeatureUi.Button("恢复本组件默认外观",()=>{Prefs.Widgets.Styles.Remove(s);apply();fill();}));
                };
                kind.SelectionChanged+=(a,e)=>fill();fill();root.Children.Add(FeatureUi.Button("完成",()=>w.Close()));w.ShowDialog();
            }finally{Board.ModalBusy=previous;}
        }
    }
}
