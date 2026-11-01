using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;


namespace Lumina {
    public class AddCandidate : INotifyPropertyChanged {
        public string Path {get;set;}
        public string Name {get{return EntryPathRules.Name(Path);}}
        bool selected=true;
        public bool Selected {get{return selected;}set{if(selected==value)return;selected=value;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Selected"));}}
        public event PropertyChangedEventHandler PropertyChanged;
    }
    public class BatchAddWindow : Window {
        readonly Controller C;readonly string targetGroup;readonly CancellationTokenSource cancellation=new CancellationTokenSource();
        readonly List<AddCandidate> candidates=new List<AddCandidate>();ListBox list;TextBlock status;Button add;WrapPanel sources;CheckBox recursive,launchOnly;bool busy,closed,changing;
        public BatchAddWindow(Controller controller) {
            C=controller;targetGroup=C.Board.IsVisible?C.Board.CurrentGroup:"全部";Title="Lumina · 添加到 "+targetGroup;Width=Math.Min(800,SystemParameters.WorkArea.Width-40);Height=Math.Min(670,SystemParameters.WorkArea.Height-50);
            WindowStartupLocation=WindowStartupLocation.CenterOwner;Owner=C.Board.IsVisible?(Window)C.Board:C.Dock;Topmost=true;Background=new SolidColorBrush(Color.FromRgb(32,34,42));Foreground=Brushes.White;
            var root=new Grid{Margin=new Thickness(24)};Content=root;root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var header=new StackPanel();root.Children.Add(header);header.Children.Add(new TextBlock{Text="一次加入应用、文件与文件夹",FontSize=22,Margin=new Thickness(0,0,0,8)});
            header.Children.Add(new TextBlock{Text="添加位置："+targetGroup+" · 选择来源并勾选，已有入口会自动去重。",Foreground=Brushes.LightGray,Margin=new Thickness(0,0,0,12)});
            sources=new WrapPanel();header.Children.Add(sources);
            sources.Children.Add(MakeButton("选择多个文件",PickFiles));sources.Children.Add(MakeButton("添加文件夹入口",()=>{string folder=FeatureUi.PickFolder(this,"添加文件夹本身作为入口（不扫描里面的文件）");if(folder!=null)Merge(new[]{folder});}));sources.Children.Add(MakeButton("扫描文件夹",PickFolder));
            sources.Children.Add(MakeButton("读取桌面",()=>Scan(new string[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)},false)));
            sources.Children.Add(MakeButton("读取开始菜单",()=>Scan(new string[]{Environment.GetFolderPath(Environment.SpecialFolder.Programs),Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)},true)));
            recursive=new CheckBox{Content="扫描文件夹时包含子文件夹",IsChecked=true,Foreground=Brushes.White,Margin=new Thickness(0,8,0,12)};header.Children.Add(recursive);launchOnly=new CheckBox{Content="只扫描启动入口（过滤卸载、更新、辅助进程和网页书签）",IsChecked=true,Foreground=Brushes.White,Margin=new Thickness(0,0,0,10)};header.Children.Add(launchOnly);
            list=new ListBox{Background=new SolidColorBrush(Color.FromRgb(25,27,34)),BorderThickness=new Thickness(0),Foreground=Brushes.White,Margin=new Thickness(0,0,0,12)};
            VirtualizingStackPanel.SetIsVirtualizing(list,true);VirtualizingStackPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);
            ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
            var itemStyle=new Style(typeof(ListBoxItem));itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));list.ItemContainerStyle=itemStyle;
            var check=new FrameworkElementFactory(typeof(CheckBox));check.SetBinding(ToggleButton.IsCheckedProperty,new Binding("Selected"){Mode=BindingMode.TwoWay});check.SetValue(FrameworkElement.MarginProperty,new Thickness(6));check.SetValue(Control.ForegroundProperty,Brushes.White);
            var labels=new FrameworkElementFactory(typeof(StackPanel));var name=new FrameworkElementFactory(typeof(TextBlock));name.SetBinding(TextBlock.TextProperty,new Binding("Name"));name.SetValue(TextBlock.FontSizeProperty,14.0);labels.AppendChild(name);
            var path=new FrameworkElementFactory(typeof(TextBlock));path.SetBinding(TextBlock.TextProperty,new Binding("Path"));path.SetValue(TextBlock.FontSizeProperty,11.0);path.SetValue(TextBlock.ForegroundProperty,Brushes.LightGray);path.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);labels.AppendChild(path);check.AppendChild(labels);
            list.ItemTemplate=new DataTemplate{VisualTree=check};Grid.SetRow(list,1);root.Children.Add(list);
            var footer=new StackPanel();Grid.SetRow(footer,2);root.Children.Add(footer);
            status=new TextBlock{Text="请选择文件，或读取桌面 / 开始菜单。",Margin=new Thickness(0,0,0,10),Foreground=Brushes.LightGray};footer.Children.Add(status);
            var actions=new WrapPanel();footer.Children.Add(actions);actions.Children.Add(MakeButton("全选",()=>SelectAll(true)));actions.Children.Add(MakeButton("取消全选",()=>SelectAll(false)));
            add=MakeButton("添加已选（0）",()=>{var paths=candidates.Where(x=>x.Selected).Select(x=>x.Path).ToArray();int before=C.Prefs.Apps.Count(a=>FeatureRules.InGroup(a,targetGroup));C.Add(paths,true,targetGroup);C.Board.ClearSearch();C.Board.SetStatus("已添加 "+(C.Prefs.Apps.Count(a=>FeatureRules.InGroup(a,targetGroup))-before)+" 个入口");Close();});add.IsEnabled=false;actions.Children.Add(add);
            actions.Children.Add(MakeButton("关闭",()=>Close()));Closed+=(s,e)=>{closed=true;cancellation.Cancel();};
        }
        static Button MakeButton(string title,Action action) {var b=new Button{Content=title,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,4)};b.Click+=(s,e)=>action();return b;}
        void SelectAll(bool value) {if(busy)return;changing=true;foreach(var c in candidates)c.Selected=value;changing=false;UpdateCount();}
        void UpdateCount() {int count=candidates.Count(x=>x.Selected);add.Content="添加已选（"+count+"）";add.IsEnabled=!busy&&count>0;if(!busy)status.Text="共 "+candidates.Count+" 个新入口 · 已选 "+count+" 个";}
        void Merge(IEnumerable<string> paths) {
            var known=new HashSet<string>(candidates.Select(c=>c.Path),StringComparer.OrdinalIgnoreCase);
            foreach(var path in EntryRules.Candidates(paths,C.Prefs.Apps.Where(a=>targetGroup=="全部"||FeatureRules.InGroup(a,targetGroup))))if(known.Add(path)) {
                var item=new AddCandidate{Path=path};item.PropertyChanged+=(s,e)=>{if(!changing)UpdateCount();};candidates.Add(item);
            }
            candidates.Sort((a,b)=>StringComparer.CurrentCultureIgnoreCase.Compare(a.Name,b.Name));list.ItemsSource=null;list.ItemsSource=candidates;UpdateCount();
        }
        void PickFiles() {
            var dialog=new Microsoft.Win32.OpenFileDialog{Title="按 Ctrl / Shift 多选软件、快捷方式或文件",Multiselect=true,Filter="应用、文档与表格|*.exe;*.lnk;*.appref-ms;*.url;*.cmd;*.bat;*.doc;*.docx;*.docm;*.xls;*.xlsx;*.xlsm;*.csv;*.ppt;*.pptx;*.pptm;*.wps;*.et;*.dps;*.xlsb;*.pdf;*.txt;*.rtf;*.md|所有文件|*.*"};
            if(dialog.ShowDialog(this)==true){if(dialog.FileNames.Any(DocumentRules.Supported))launchOnly.IsChecked=false;Merge(dialog.FileNames);}
        }
        void PickFolder() {
            using(var dialog=new Forms.FolderBrowserDialog{Description="选择要扫描的软件或快捷方式文件夹",ShowNewFolderButton=false}) {
                var owner=new Forms.NativeWindow();owner.AssignHandle(new WindowInteropHelper(this).Handle);
                try{if(dialog.ShowDialog(owner)==Forms.DialogResult.OK)Scan(new string[]{dialog.SelectedPath},recursive.IsChecked==true);}finally{owner.ReleaseHandle();}
            }
        }
        async void Scan(string[] roots,bool recurse) {
            if(busy)return;busy=true;sources.IsEnabled=false;recursive.IsEnabled=false;add.IsEnabled=false;list.IsEnabled=false;status.Text="正在扫描，可关闭窗口取消…";
            try {
                bool only=launchOnly.IsChecked==true;var found=await Task.Run(()=>{var result=ImportScanner.Scan(roots,recurse,cancellation.Token,2500,10000,only);if(only)result.Paths=result.Paths.Where(path=>{cancellation.Token.ThrowIfCancellationRequested();return LaunchInfoService.Primary(LaunchInfoService.Read(path));}).ToList();return result;},cancellation.Token);
                if(closed)return;busy=false;Merge(found.Paths);if(found.Skipped>0)status.Text+=" · 跳过 "+found.Skipped+" 个无法读取的目录";if(found.Limited)status.Text+=" · 达到扫描上限，请分文件夹添加";
            }catch(OperationCanceledException){}catch(Exception ex){if(!closed){Store.Log(ex);status.Text="扫描失败："+ex.Message;}}
            finally {busy=false;if(!closed){sources.IsEnabled=true;recursive.IsEnabled=true;list.IsEnabled=true;add.IsEnabled=candidates.Any(x=>x.Selected);}}
        }
    }
}
