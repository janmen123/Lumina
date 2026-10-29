using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GlassBoard {
    // A single running indicator used by the Dock and the board tiles: a dot for
    // one window, a small count badge when the app has several open windows.
    public static class RunningBadge {
        public static Border Create(){
            var text=new TextBlock{Tag="running-count",FontSize=8.5,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(16,26,38)),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Collapsed,IsHitTestVisible=false};
            var badge=new Border{Tag="running",MinWidth=5,Height=4,CornerRadius=new CornerRadius(2),Background=new SolidColorBrush(Color.FromRgb(135,205,255)),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,1),Visibility=Visibility.Collapsed,IsHitTestVisible=false};
            badge.Child=text;return badge;
        }
        public static void Apply(Border badge,int count){
            if(badge==null)return;
            if(count<=0){badge.Visibility=Visibility.Collapsed;return;}
            badge.Visibility=Visibility.Visible;var text=badge.Child as TextBlock;
            if(count<=1){
                badge.MinWidth=5;badge.Height=4;badge.CornerRadius=new CornerRadius(2);badge.Margin=new Thickness(0,0,0,1);
                if(text!=null){text.Text="";text.Visibility=Visibility.Collapsed;}
            }else{
                badge.MinWidth=14;badge.Height=13;badge.CornerRadius=new CornerRadius(7);badge.Margin=new Thickness(0,0,0,0);
                if(text!=null){text.Text=count>9?"9+":count.ToString();text.Visibility=Visibility.Visible;}
            }
        }
    }
}
