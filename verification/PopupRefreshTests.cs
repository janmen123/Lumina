using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lumina;

public static class PopupRefreshTests {
    static int checks;
    public static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static T Find<T>(DependencyObject node) where T:DependencyObject {
        if(node is T)return (T)node;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++){var result=Find<T>(VisualTreeHelper.GetChild(node,i));if(result!=null)return result;}return null;
    }
    static void Layout(FrameworkElement e,double width,double height){e.Measure(new Size(width,height));e.Arrange(new Rect(0,0,width,Math.Min(height,e.DesiredSize.Height)));e.UpdateLayout();}
    [STAThread] static int Main(){
        var c=new Controller(false);
        try{
            c.Board.TestRenderingLifecycle();
            var preview=new Grid{Width=760,Background=new LinearGradientBrush(Color.FromRgb(25,63,65),Color.FromRgb(20,27,42),35)};
            preview.ColumnDefinitions.Add(new ColumnDefinition());preview.ColumnDefinitions.Add(new ColumnDefinition());
            for(int index=0;index<2;index++){
                bool light=index==1;
                var owner=new Window();Polish.Apply(owner,light);
                var stack=new StackPanel{Margin=new Thickness(14),Background=light?Polish.Color("#FFDDE8EC"):Brushes.Transparent};Grid.SetColumn(stack,index);preview.Children.Add(stack);
                stack.Children.Add(new TextBlock{Text=light?"浅色 · 菜单与下拉选项":"深色 · 菜单与下拉选项",FontSize=17,Foreground=Polish.Ink(light),Margin=new Thickness(18,15,0,10)});
                c.Prefs.Light=light;
                var menu=(ContextMenu)typeof(BoardWindow).GetMethod("CreateMoreMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c.Board,null);
                Layout(menu,345,450);
                var menuBitmap=new RenderTargetBitmap(345,(int)Math.Ceiling(menu.ActualHeight),96,96,PixelFormats.Pbgra32);menuBitmap.Render(menu);stack.Children.Add(new Image{Source=menuBitmap,Stretch=Stretch.None,HorizontalAlignment=HorizontalAlignment.Left,SnapsToDevicePixels=true});
                Check(menu.Items.Count==11,"compact more menu retains 9 commands and two separators");
                var undo=(MenuItem)menu.Items[0];undo.ApplyTemplate();Check(undo.InputGestureText=="Ctrl+Z"&&undo.Header.ToString()=="撤销","shortcut separate from label");
                var display=(MenuItem)menu.Items[4];display.ApplyTemplate();Check(display.Items.Count==5,"display controls preserved");
                var popup=(Popup)display.Template.FindName("PART_Popup",display);Check(popup!=null&&popup.AllowsTransparency,"submenu popup contract retained");
                var surface=(Border)display.Template.FindName("SubmenuSurface",display);Check(PopupChrome.GetMaterial(surface)==(light?1:0),"submenu uses shared glass material");
                Layout((FrameworkElement)popup.Child,300,250);var nested=(MenuItem)display.Items[0];nested.ApplyTemplate();Check(nested.Style==menu.Resources[typeof(MenuItem)],"submenu inherits item style across popup tree");
                var item=new MenuItem{Header="选中项",IsChecked=true,Style=(Style)menu.Resources[typeof(MenuItem)]};Layout(item,220,40);item.ApplyTemplate();
                Check(((TextBlock)item.Template.FindName("Check",item)).Visibility==Visibility.Visible,"menu check visible");item.IsEnabled=false;
                Check(((Border)item.Template.FindName("Chrome",item)).Opacity<1,"disabled menu item faded");
                Check(item.FocusVisualStyle==null,"no legacy dotted focus adorner");
                var combo=new ComboBox{Width=270,Style=(Style)owner.Resources[typeof(ComboBox)],ItemsSource=new[]{"全部","未分组","文档","日常","学习","游戏","工具"},SelectedIndex=2};
                foreach(System.Collections.DictionaryEntry resource in owner.Resources)combo.Resources[resource.Key]=resource.Value;
                stack.Children.Add(combo);Layout(combo,270,40);combo.ApplyTemplate();
                var comboPopup=(Popup)combo.Template.FindName("PART_Popup",combo);Check(comboPopup!=null&&comboPopup.AllowsTransparency,"combo popup retained");
                var option=new ComboBoxItem{Content="文档",IsSelected=true,Style=(Style)owner.Resources[typeof(ComboBoxItem)]};Layout(option,250,36);option.ApplyTemplate();
                Check(((TextBlock)option.Template.FindName("SelectionMark",option)).Visibility==Visibility.Visible,"combo selected mark visible");Check(option.FocusVisualStyle==null,"combo no dotted focus adorner");
                // A static presentation of the actual dropdown styles, without opening desktop windows.
                var choices=new StackPanel();foreach(string name in new[]{"全部","未分组","文档","日常"})choices.Children.Add(new ComboBoxItem{Content=name,IsSelected=name=="文档",Style=(Style)owner.Resources[typeof(ComboBoxItem)]});
                stack.Children.Add(new Border{Width=270,CornerRadius=new CornerRadius(12),Background=PopupChrome.Tint(light),BorderBrush=Polish.Line(light),BorderThickness=new Thickness(1),Padding=new Thickness(5),Margin=new Thickness(0,6,0,15),Child=choices});
                foreach(var orientation in new[]{Orientation.Vertical,Orientation.Horizontal}){
                    var scroll=new ScrollBar{Orientation=orientation,Style=PopupChrome.ScrollStyle(light),Minimum=0,Maximum=800,ViewportSize=200,Value=100};Layout(scroll,orientation==Orientation.Vertical?8:200,orientation==Orientation.Vertical?200:8);scroll.ApplyTemplate();
                    var track=(Track)scroll.Template.FindName("PART_Track",scroll);Check(track!=null&&track.Thumb!=null,"scrollbar track and thumb retained");scroll.Value=350;Check(track.Value==350,"scrollbar value binding works");Check(track.IsDirectionReversed==(orientation==Orientation.Vertical),"scroll direction correct");
                }
                var view=new ScrollViewer{Height=80,Width=240,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=new Border{Height=600}};view.Resources[typeof(ScrollBar)]=PopupChrome.ScrollStyle(light);Layout(view,240,80);
                var bar=Find<ScrollBar>(view);Check(bar!=null&&bar.Style==PopupChrome.ScrollStyle(light),"ScrollViewer uses thin shared scrollbar");
                view.ScrollToVerticalOffset(200);view.UpdateLayout();Check(view.VerticalOffset==200,"scroll content remains navigable with custom track");
                owner.Close();
            }
            preview.Measure(new Size(760,1100));preview.Arrange(new Rect(0,0,760,preview.DesiredSize.Height));preview.UpdateLayout();
            var bitmap=new RenderTargetBitmap(760,(int)Math.Ceiling(preview.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(preview);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create("menu-preview.png"))png.Save(file);
            Console.WriteLine("PASS: "+checks+" popup/render lifecycle checks");return 0;
        }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);Console.WriteLine(e.StackTrace);if(e.InnerException!=null)Console.WriteLine(e.InnerException.Message);return 1;}
        finally{c.ShuttingDown=true;c.Board.Close();c.Dock.Close();}
    }
}
namespace Lumina {
    public partial class BoardWindow {
        public void TestRenderingLifecycle(){
            StartDragRendering();StartDragRendering();PopupRefreshTests.Check(dragRenderingSubscribed,"render subscription is active and idempotent");
            dragGhostShown=true;
            var args=(RenderingEventArgs)Activator.CreateInstance(typeof(RenderingEventArgs),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{TimeSpan.FromMilliseconds(8.33)},null);
            OnDragRendering(null,args);PopupRefreshTests.Check(lastDragRenderingTime==args.RenderingTime,"uses frame timestamp rather than timer interval");
            OnDragRendering(null,args);PopupRefreshTests.Check(lastDragRenderingTime==args.RenderingTime,"same-frame callback remains deduplicated");
            EndDragGhostMotion();PopupRefreshTests.Check(!dragRenderingSubscribed&&lastDragRenderingTime==TimeSpan.MinValue,"end unsubscribes static rendering event");
            StartDragRendering();dragGhostShown=false;OnDragRendering(null,EventArgs.Empty);PopupRefreshTests.Check(!dragRenderingSubscribed,"hidden ghost cannot retain render loop");
        }
    }
}


