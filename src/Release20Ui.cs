using System;using System.Linq;using System.Windows;using System.Windows.Controls;using System.Windows.Controls.Primitives;using System.Windows.Input;using System.Windows.Media;using System.Windows.Media.Animation;using System.Runtime.InteropServices;
namespace GlassBoard {
    public static class NativeStartMenu {
        [DllImport("user32.dll")]static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
        public static void Open(){
            // Request the actual Windows menu using the standard Ctrl+Esc shortcut.
            // Avoid releasing any modifier the user is holding.
            if(Keyboard.Modifiers!=ModifierKeys.None){MessageBox.Show("请松开 Ctrl、Alt、Shift 或 Windows 键后重试。","Windows 开始菜单");return;}
            try{keybd_event(0x11,0,0,UIntPtr.Zero);keybd_event(0x1B,0,0,UIntPtr.Zero);}finally{keybd_event(0x1B,0,2,UIntPtr.Zero);keybd_event(0x11,0,2,UIntPtr.Zero);}
        }
    }
    public partial class DockWindow {
        ControlTemplate InvisibleDockGrip(){var area=new FrameworkElementFactory(typeof(Border));area.SetValue(Border.BackgroundProperty,Brushes.Transparent);return new ControlTemplate(typeof(Thumb)){VisualTree=area};}
    }
    public partial class Controller {
        void BuildSettingsNavigation(DockPanel host,TabControl tabs){
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.ContentSourceProperty,"SelectedContent");presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Stretch);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Stretch);tabs.Template=new ControlTemplate(typeof(TabControl)){VisualTree=presenter};
            host.Children.Remove(tabs);
            var navigation=new StackPanel();var scroll=new ScrollViewer{Width=190,Margin=new Thickness(0,0,10,0),Content=navigation,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};DockPanel.SetDock(scroll,System.Windows.Controls.Dock.Left);host.Children.Add(scroll);host.Children.Add(tabs);
            var buttons=new System.Collections.Generic.Dictionary<int,Button>();var groups=new System.Collections.Generic.Dictionary<int,Expander>();
            Action sync=()=>{foreach(var pair in buttons){bool selected=pair.Key==tabs.SelectedIndex;pair.Value.Background=selected?Polish.Color("#344A70"):Brushes.Transparent;pair.Value.BorderBrush=selected?Polish.Color("#6A9BFA"):Brushes.Transparent;if(selected)groups[pair.Key].IsExpanded=true;}};
            Action<string,int[]> groupPage=(title,indices)=>{var children=new StackPanel();var parent=new Expander{Header=new TextBlock{Text=title,Foreground=Brushes.LightGray},Content=children,IsExpanded=true,Margin=new Thickness(0,6,0,8),Foreground=Brushes.White};navigation.Children.Add(parent);foreach(int i in indices){int index=i;var tab=(TabItem)tabs.Items[i];var button=new Button{Content=new TextBlock{Text=Convert.ToString(tab.Header),Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap},Style=Polish.ButtonStyle(false),HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(10,9,10,9),Margin=new Thickness(15,2,2,2),Background=Brushes.Transparent,BorderBrush=Brushes.Transparent};button.Click+=(s,e)=>tabs.SelectedIndex=index;buttons[i]=button;groups[i]=parent;children.Children.Add(button);}};
            groupPage("外观与动效",new[]{0,1,8});groupPage("组件与系统",new[]{3,2,4});groupPage("帮助与反馈",new[]{6,7,5});
            tabs.SelectionChanged+=(s,e)=>{if(e.Source==tabs)sync();};sync();
        }
    }
    public partial class BoardWindow {
        void AnimatePage(int direction){
            if(Tiles==null)return;Tiles.BeginAnimation(UIElement.OpacityProperty,null);Tiles.Opacity=1;Tiles.RenderTransform=Transform.Identity;
            if(!C.Prefs.BoardPageMotion)return;
            var duration=TimeSpan.FromMilliseconds(160);var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
            Tiles.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.2,1,duration){FillBehavior=FillBehavior.Stop});
            if(C.Prefs.BoardPageStyle=="slide"){var shift=new TranslateTransform();Tiles.RenderTransform=shift;shift.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(direction>0?24:-24,0,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});}
            else if(C.Prefs.BoardPageStyle=="scale"){var scale=new ScaleTransform(1,1);Tiles.RenderTransformOrigin=new Point(.5,.5);Tiles.RenderTransform=scale;scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(.97,1,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(.97,1,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});}
        }
    }
}
