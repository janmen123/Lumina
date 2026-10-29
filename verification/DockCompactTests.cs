using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GlassBoard;

public static class DockCompactTests {
    static int checks;
    public static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    static DockOptions Read(string json){using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))return (DockOptions)new DataContractJsonSerializer(typeof(DockOptions)).ReadObject(stream);}
    [STAThread] public static int Main(){
        var c=new Controller(false);
        try{
            var legacy=Read("{\"IconSize\":44,\"Padding\":8}");legacy.Normalize();
            Check(legacy.VerticalPadding==4&&legacy.UseScreenBounds,"legacy settings receive compact defaults and screen-edge placement");
            var configured=Read("{\"VerticalPadding\":9,\"UseScreenBounds\":false}");configured.Normalize();
            Check(configured.VerticalPadding==9&&!configured.UseScreenBounds,"explicit spacing and work-area option preserved");
            configured.VerticalPadding=double.NaN;configured.Normalize();Check(configured.VerticalPadding==4,"invalid padding normalized");
            configured.VerticalPadding=100;configured.Normalize();Check(configured.VerticalPadding==28,"padding upper bound");
            var copy=configured.Copy();Check(copy.VerticalPadding==configured.VerticalPadding&&copy.UseScreenBounds==configured.UseScreenBounds,"appearance copy retains new fields");
            using(var stream=new MemoryStream()){
                new DataContractJsonSerializer(typeof(DockOptions)).WriteObject(stream,copy);stream.Position=0;
                var roundtrip=(DockOptions)new DataContractJsonSerializer(typeof(DockOptions)).ReadObject(stream);
                Check(roundtrip.VerticalPadding==28&&!roundtrip.UseScreenBounds,"new options persist through serialization");
            }
            var screen=new Rect(0,0,1920,1080);var work=new Rect(0,0,1920,1040);
            double height=160;
            Check(DockGeometryRules.BottomTop(screen,height,0)+height-14==1080,"zero gap puts visible border exactly at screen bottom");
            Check(DockGeometryRules.BottomTop(screen,height,12)+height-14==1068,"bottom gap measures visible border");
            Check(DockGeometryRules.Top(9999,screen,height)+height-14==1080,"drag can reach screen edge without losing glass");
            Check(DockGeometryRules.Top(-9999,screen,height)==0,"drag retains upper bound");
            Check(DockGeometryRules.BottomTop(work,height,0)+height-14==1040,"work-area opt-out respects taskbar boundary");
            var second=new Rect(-1280,-900,1280,900);
            Check(DockGeometryRules.Top(500,second,height)+height-14==0,"negative-coordinate monitor uses its own bottom edge");
            Check(DockGeometryRules.Top(-9999,second,height)==-900,"negative monitor top supported");

            Check(WindowActivationRules.SelectDock(new long[]{10,20},20,10)==20,"multiwindow click targets foreground rather than cycling");
            Check(WindowActivationRules.SelectDock(new long[]{10,20},99,20)==20,"background app restores its previous window");
            Check(WindowActivationRules.SelectDock(new long[]{10,20},99,30)==10,"closed previous window falls back to a live window");
            Check(WindowActivationRules.SelectDock(new long[0],99,20)==0,"no existing window permits launch");
            Check(WindowActivationRules.MinimizeDock(true,false),"foreground window minimizes");
            Check(!WindowActivationRules.MinimizeDock(false,false),"covered window restores");
            Check(!WindowActivationRules.MinimizeDock(false,true),"minimized window restores");
            Check(!WindowActivationRules.MinimizeDock(true,true),"minimized state wins over stale foreground state");
            Check(!RunningWindows.ToggleWindow(new RunningWindow{Handle=IntPtr.Zero}),"closed handle cannot trigger a window command");
            var parent=new Window();var dialog=new Window();var other=new Window();
            try{
                var parentHandle=new WindowInteropHelper(parent).EnsureHandle();
                new WindowInteropHelper(dialog).Owner=parentHandle;
                var dialogHandle=new WindowInteropHelper(dialog).EnsureHandle();var otherHandle=new WindowInteropHelper(other).EnsureHandle();
                Check(RunningWindows.OwnsForeground(parentHandle,dialogHandle),"native owned dialog maps to its main window");
                Check(!RunningWindows.OwnsForeground(parentHandle,otherHandle),"another document in same process stays independent");
                Check(RunningWindows.Valid(new RunningWindow{Handle=parentHandle,ProcessId=(uint)Process.GetCurrentProcess().Id}),"live native handle passes identity check");
                Check(!RunningWindows.Valid(new RunningWindow{Handle=parentHandle,ProcessId=uint.MaxValue}),"reused handle from wrong process rejected");
                Check(!parent.IsVisible&&!dialog.IsVisible&&!other.IsVisible,"native identity checks did not show windows");
            }finally{dialog.Close();other.Close();parent.Close();}
            c.Dock.TestCompactDock();
            Console.WriteLine("PASS: "+checks+" Dock placement, rendering and window-action checks (no desktop interaction)");return 0;
        }catch(Exception ex){Console.WriteLine(ex);return 1;}
        finally{c.ShuttingDown=true;c.Dock.ReleaseHotkey();c.Board.Close();c.Dock.Close();}
    }
}

namespace GlassBoard {
    public partial class DockWindow {
        internal void TestCompactDock(){
            bool handled=false;var result=Hook(IntPtr.Zero,0x21,IntPtr.Zero,IntPtr.Zero,ref handled);
            DockCompactTests.Check(handled&&result.ToInt32()==3,"mouse activation keeps app focus and still delivers click");
            C.Prefs.Dock.ShowComputer=false;C.Prefs.Dock.ShowSettings=false;C.Prefs.Dock.ShowSeparator=false;
            int scenarios=0;
            foreach(bool reflection in new[]{false,true})foreach(bool labels in new[]{false,true})foreach(double zoom in new[]{1.0,1.3,1.6})foreach(string style in new[]{"highlight","zoom","lift","zoom-lift"})foreach(double padding in new[]{0.0,4.0,28.0}){
                var o=C.Prefs.Dock;o.IconSize=64;o.MotionLevel=1.5;o.Reflection=reflection;o.ReflectionLength=75;o.ReflectionBlur=6;o.ReflectionGap=12;o.ReflectionAngle=40;o.ShowLabels=labels;o.Zoom=zoom;o.VerticalPadding=padding;o.HoverAnimationStyle=style;o.HoverAnimation=true;
                Refresh(false);Root.Measure(new Size(Width,Height));Root.Arrange(new Rect(0,0,Width,Height));Root.UpdateLayout();
                var button=Items.Children.OfType<Button>().First();
                double target=InteractionRules.ZoomHover(o.HoverAnimationStyle)?Math.Min(1.6,1+(o.Zoom-1)*o.MotionLevel):1;
                double lift=InteractionRules.LiftHover(o.HoverAnimationStyle)?-5*o.MotionLevel:0;
                var group=(TransformGroup)button.RenderTransform;((ScaleTransform)group.Children[0]).ScaleX=target;((ScaleTransform)group.Children[0]).ScaleY=target;((TranslateTransform)group.Children[1]).Y=lift;
                var bounds=button.TransformToAncestor(Root).TransformBounds(new Rect(0,0,button.ActualWidth,button.ActualHeight));
                if(bounds.Top < -14-1.5||bounds.Bottom>Root.ActualHeight+1.5)throw new Exception("hover clipped: "+reflection+"/"+labels+"/"+zoom+"/"+style+"/"+padding+" bounds="+bounds+" root="+Root.ActualHeight);
                scenarios++;
            }
            DockCompactTests.Check(scenarios==144,"144 reflection, label, hover and spacing combinations fit vertically");
            C.Prefs.Dock.HoverAnimation=false;C.Prefs.Dock.VerticalPadding=0;
            var plain=DockGeometryRules.Insets(C.Prefs.Dock);
            DockCompactTests.Check(plain.Top==0&&plain.Bottom==0,"disabled hover does not reserve unused animation space");
            var current=new DockOptions{IconSize=44,Padding=8,VerticalPadding=4,MotionLevel=1.3,Zoom=1.3,Reflection=true,ReflectionLength=63,ReflectionBlur=2,ReflectionGap=2};
            double oldButton=DockGeometryRules.ButtonHeight(current),oldHeight=28+oldButton+current.Padding*2+Math.Max(0,oldButton*.39*.65+6.5-current.Padding)*2;
            DockCompactTests.Check(oldHeight-DockGeometryRules.Height(current)>30,"user's reflection layout becomes over 30 logical pixels shorter");
            Console.WriteLine("Current appearance height: "+oldHeight.ToString("0.0")+" -> "+DockGeometryRules.Height(current).ToString("0.0")+" DIP");
            C.Prefs.Dock=current;Refresh(false);
            var doc=new StackPanel{Width=760,Background=new LinearGradientBrush(Color.FromRgb(22,61,61),Color.FromRgb(17,27,36),30),Margin=new Thickness(0)};
            foreach(bool compact in new[]{false,true}){
                double h=compact?DockGeometryRules.Height(current):oldHeight;
                var title=new TextBlock{Text=compact?"调整后 · 上下留白分开计算":"调整前 · 上下重复预留放大空间",Foreground=Brushes.White,FontSize=17,Margin=new Thickness(24,18,0,8)};doc.Children.Add(title);
                var pane=new Grid{Width=720,Height=h,Margin=new Thickness(20,0,20,4)};
                pane.Children.Add(new Border{Margin=new Thickness(14),CornerRadius=new CornerRadius(26),Background=Polish.Color("#75202B37"),BorderBrush=Polish.Color("#70D3E6ED"),BorderThickness=new Thickness(1)});
                var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=compact?DockGeometryRules.Insets(current):new Thickness(current.HoverPadding(),0,current.HoverPadding(),0)};
                foreach(string label in new[]{"▦","●","◆","▣","✦","◉","⚙"})row.Children.Add(DockButton(label,null,()=>{},label));pane.Children.Add(row);doc.Children.Add(pane);
            }
            doc.Measure(new Size(760,double.PositiveInfinity));doc.Arrange(new Rect(new Point(),doc.DesiredSize));doc.UpdateLayout();
            var bitmap=new RenderTargetBitmap(760,(int)Math.Ceiling(doc.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(doc);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create("dock-spacing-preview.png"))png.Save(stream);
            DockCompactTests.Check(!IsVisible,"layout checks and preview never show Dock on desktop");
        }
    }
}


