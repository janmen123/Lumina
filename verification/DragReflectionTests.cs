using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lumina;

public static class DragReflectionTests {
    [System.Runtime.InteropServices.DllImport("shell32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern uint DragQueryFile(IntPtr h,uint index,StringBuilder path,uint length);
    [System.Runtime.InteropServices.DllImport("ole32.dll")] static extern void ReleaseStgMedium(ref System.Runtime.InteropServices.ComTypes.STGMEDIUM medium);
    static int checks;
    public static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static void Pump(int ms=240){var f=new DispatcherFrame();var t=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};t.Tick+=(s,e)=>{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f);}
    static DockOptions Read(string json){using(var s=new MemoryStream(Encoding.UTF8.GetBytes(json)))return (DockOptions)new DataContractJsonSerializer(typeof(DockOptions)).ReadObject(s);}
    [STAThread] public static int Main(){
        var c=new Controller(false);
        try{
            c.Board.TestDragFixes();
            string root=Path.GetFullPath("payload-test");Directory.CreateDirectory(root);string file=Path.Combine(root,"sample.txt");File.WriteAllText(file,"Local drag test only");
            var data=BoardDragData.Create(new[]{file,root,Path.Combine(root,"missing.txt")},"文档");
            Check(data.GetFileDropList().Count==2,"real file and directory exported; missing path excluded");
            var format=new System.Runtime.InteropServices.ComTypes.FORMATETC{cfFormat=15,dwAspect=System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL};
            System.Runtime.InteropServices.ComTypes.STGMEDIUM medium;((System.Runtime.InteropServices.ComTypes.IDataObject)data).GetData(ref format,out medium);
            try{var pathBuffer=new StringBuilder(32768);Check(DragQueryFile(medium.unionmember,uint.MaxValue,null,0)==2,"native Windows CF_HDROP contains both items");DragQueryFile(medium.unionmember,0,pathBuffer,32768);Check(pathBuffer.ToString()==file,"native shell receives exact Unicode file path");}finally{ReleaseStgMedium(ref medium);}
            Check(((string[])data.GetData("Lumina.Entries")).Length==3,"internal payload retained");
            Check(BoardDragData.Allowed(data)==DragDropEffects.Copy,"outbound files copy only");
            var virtualData=BoardDragData.Create(new[]{"shell:AppsFolder\\test"},"全部");
            Check(!virtualData.GetDataPresent(DataFormats.FileDrop),"virtual shell items not exported as disk files");
            var defaults=Read("{}");Check(defaults.DragMotion&&defaults.ReflectionOpacity==45&&defaults.ReflectionAngle==0&&defaults.ReflectionFade==85,"old settings get defaults");
            var o=new DockOptions{Reflection=true,ReflectionOpacity=37,ReflectionAngle=-29,ReflectionLength=62,ReflectionBlur=3.5,ReflectionGap=7,ReflectionFade=65};
            using(var s=new MemoryStream()){var j=new DataContractJsonSerializer(typeof(DockOptions));j.WriteObject(s,o);s.Position=0;var r=(DockOptions)j.ReadObject(s);r.Normalize();Check(r.Reflection&&r.ReflectionOpacity==37&&r.ReflectionAngle==-29&&r.ReflectionLength==62&&r.ReflectionBlur==3.5&&r.ReflectionGap==7&&r.ReflectionFade==65,"all reflection settings survive save/load");}
            var copy=o.Copy();copy.ReflectionAngle=10;Check(o.ReflectionAngle==-29,"draft copy independent");
            o.ReflectionAngle=double.NaN;o.ReflectionBlur=100;o.ReflectionLength=-2;o.Normalize();Check(o.ReflectionAngle==0&&o.ReflectionBlur<=6&&o.ReflectionLength>=15,"invalid reflection options clamped");
            o.Reflection=false;Check(DockReflection.ExtraHeight(o)==0&&DockReflection.Create(new Border(),o)==null,"disabled reflection has zero layout cost");
            c.TestReflectionEditor();RenderPresets();
            Console.WriteLine("PASS: "+checks+" drag/reflection checks");return 0;
        }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);Console.WriteLine(e.StackTrace);return 1;}
        finally{c.ShuttingDown=true;c.Board.Close();c.Dock.Close();c.CloseWidgets();typeof(Controller).GetMethod("CloseControlCenter",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,null);}
    }
    static void RenderPresets(){
        var panel=new StackPanel{Background=new SolidColorBrush(Color.FromRgb(25,30,42)),Width=650};
        for(int p=0;p<3;p++){
            var o=new DockOptions{Reflection=true,IconSize=64};DockReflection.Preset(o,p);
            panel.Children.Add(new TextBlock{Text=new[]{"轻柔玻璃","清晰镜面","斜光掠影"}[p],Foreground=Brushes.White,FontSize=18,Margin=new Thickness(28,18,0,12)});
            var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(50,0,50,20)};
            for(int i=0;i<4;i++){
                var icon=new Border{Width=64,Height=64,CornerRadius=new CornerRadius(15),Background=new SolidColorBrush(new[]{Colors.DodgerBlue,Colors.MediumSeaGreen,Colors.OrangeRed,Colors.MediumPurple}[i]),Child=new TextBlock{Text=new[]{"文","图","▶","♫"}[i],FontSize=30,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
                var cell=new StackPanel{Margin=new Thickness(18,0,34,0)};cell.Children.Add(icon);cell.Children.Add(DockReflection.Create(icon,o));row.Children.Add(cell);
            }
            panel.Children.Add(row);
        }
        panel.Measure(new Size(650,1000));panel.Arrange(new Rect(0,0,650,panel.DesiredSize.Height));panel.UpdateLayout();
        var bitmap=new RenderTargetBitmap(650,(int)Math.Ceiling(panel.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(panel);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create("reflection-presets.png"))png.Save(f);
        Check(bitmap.PixelWidth==650,"presets render successfully");
    }
}
namespace Lumina {
    public partial class Controller {
        public void TestReflectionEditor(){
            var w=new Window{Width=620,Height=760,Background=new SolidColorBrush(Color.FromRgb(28,30,38))};
            var p=new StackPanel{Width=560,Margin=new Thickness(20)};w.Content=p;Polish.Apply(w,false);
            Prefs.Dock.Reflection=true;int updates=0;BuildReflectionEditor(p,()=>Prefs.Dock,()=>updates++);
            var expander=(Expander)p.Children[1];var body=(StackPanel)expander.Content;
            var sliders=body.Children.OfType<Slider>().ToArray();sliders[2].Value=27;
            DragReflectionTests.Check(Prefs.Dock.ReflectionAngle==27&&updates==1,"angle slider updates draft once");
            var buttons=(WrapPanel)body.Children[1];((Button)buttons.Children[2]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            DragReflectionTests.Check(Prefs.Dock.ReflectionAngle==-24&&sliders[2].Value==-24&&updates==2,"preset updates every control with one refresh");
            ((CheckBox)p.Children[0]).IsChecked=false;DragReflectionTests.Check(!body.IsEnabled&&!Prefs.Dock.Reflection,"disabled reflection disables dependent controls");
            ((CheckBox)p.Children[0]).IsChecked=true;
            p.Measure(new Size(600,760));p.Arrange(new Rect(0,0,600,p.DesiredSize.Height));p.UpdateLayout();
            var bitmap=new RenderTargetBitmap(600,(int)Math.Ceiling(p.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(p);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create("reflection-editor.png"))png.Save(f);w.Close();
        }
    }
    public partial class BoardWindow {
        void TestGrid(int count=8){
            EndDragPreview();ContentGrid.Children.Clear();
            Tiles=new UniformGrid{Columns=4,Rows=2,Width=400,Height=200,Background=Brushes.Transparent};ContentGrid.Children.Add(Tiles);
            C.Prefs.Apps.Clear();
            for(int i=0;i<count;i++){var a=new Entry{Path="test-"+i+".txt",Name="Item "+i};C.Prefs.Apps.Add(a);Tiles.Children.Add(new Border{Tag=a,Background=Brushes.Transparent});}
            Root.Measure(new Size(500,350));Root.Arrange(new Rect(0,0,500,350));Root.UpdateLayout();
        }
        void TestShift(int from,int to){dragShiftSource=(FrameworkElement)Tiles.Children[from];dragShiftSource.Opacity=.22;dragShiftFrom=from;dragShiftTo=to;ApplyBoardDragShifts();}
        DragEventArgs TestArgs(IDataObject data,Point point,RoutedEvent routed){var e=(DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs),BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,null,new object[]{data,DragDropKeyStates.LeftMouseButton,BoardDragData.Allowed(data),Tiles,point},null);e.RoutedEvent=routed;return e;}
        public void TestDragFixes(){
            TestGrid();TestShift(0,6);DragReflectionTests.Pump();
            DragReflectionTests.Check(BoardDropEntry(new Point(250,150))==C.Prefs.Apps[6],"empty visual slot resolves stable target across rows");
            var data=BoardDragData.Create(new[]{C.Prefs.Apps[0].Path},"全部");
            var args=TestArgs(data,new Point(250,150),DragDrop.PreviewDropEvent);args.RoutedEvent=DragDrop.PreviewDropEvent;
            Tiles.RaiseEvent(args);
            DragReflectionTests.Check(args.Handled&&C.Prefs.Apps[6].Path=="test-0.txt","real preview drop route commits forward long move");
            var history=(BoardEditHistory)typeof(Controller).GetField("editHistory",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(C);history.Undo(C.Prefs);
            DragReflectionTests.Check(C.Prefs.Apps[0].Path=="test-0.txt","reorder is undoable");
            TestGrid();TestShift(7,1);DragReflectionTests.Pump();
            data=BoardDragData.Create(new[]{C.Prefs.Apps[7].Path},"全部");args=TestArgs(data,new Point(150,50),DragDrop.PreviewDropEvent);args.RoutedEvent=DragDrop.PreviewDropEvent;Tiles.RaiseEvent(args);
            DragReflectionTests.Check(C.Prefs.Apps[1].Path=="test-7.txt","backward long move");
            TestGrid(5);DragReflectionTests.Check(SlotAt(new Point(150,150))==-1,"unused cell cancels rather than snapping to last item");
            TestShift(0,4);ResetBoardDragShifts();DragReflectionTests.Pump();
            DragReflectionTests.Check(Tiles.Children.OfType<FrameworkElement>().All(e=>!(e.RenderTransform is TranslateTransform)||(((TranslateTransform)e.RenderTransform).X==0&&((TranslateTransform)e.RenderTransform).Y==0)),"immediate cancellation clears pending clocks");
            TestGrid();TestShift(0,3);DragReflectionTests.Pump(60);var t=(TranslateTransform)((FrameworkElement)Tiles.Children[1]).RenderTransform;double before=t.X;
            TestShift(0,6);DragReflectionTests.Pump(100);
            DragReflectionTests.Check(Math.Abs(t.X+100)<.5,"unchanged destinations finish on original schedule");
            ResetBoardDragShifts();DragReflectionTests.Pump();DragReflectionTests.Check(Tiles.Children.OfType<FrameworkElement>().All(e=>e.Opacity==1),"source opacity restored");
            var watch=Stopwatch.StartNew();for(int i=0;i<1000;i++)TestShift(0,(i%7)+1);watch.Stop();Console.WriteLine("1000 target updates: "+watch.ElapsedMilliseconds+" ms (UI-thread synthetic workload)");
            EndDragPreview();DragReflectionTests.Pump();
            C.Prefs.Dock.DragMotion=false;dragPreviewText.Text="Preview test";dragPreview.Visibility=Visibility.Visible;MoveDragGhost(new Point());
            DragReflectionTests.Check(dragGhostWindow.IsVisible&&dragPreview.IsVisible&&dragPreviewIcon.Width==28,"disabled motion keeps basic visible preview");EndDragPreview();
            DragReflectionTests.Check(!dragGhostWindow.IsVisible&&!dragRenderingSubscribed,"render callback detached after drag");
            MotionPresets.DisableTransitions(C.Prefs);DragReflectionTests.Check(!C.Prefs.Dock.DragMotion,"disable transitions includes drag motion");
            TestGrid();var file=Path.GetFullPath("external-file-test.txt");File.WriteAllText(file,"Local test");
            data=BoardDragData.Create(new[]{file},"全部");int count=C.Prefs.Apps.Count;
            args=TestArgs(data,new Point(450,250),DragDrop.DropEvent);ExternalDrop(this,args);
            DragReflectionTests.Check(C.Prefs.Apps.Count==count&&args.Effects==DragDropEffects.None,"internal file payload outside grid never imports duplicate files");
            DragReflectionTests.Check(GroupDragEffect(data,"未分组")==DragDropEffects.Copy,"group target accepts copy-only file payload");
        }
    }
}

