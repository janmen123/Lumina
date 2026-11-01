using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Lumina {
    public static class MenuAppearanceChecks {
        static void Drain(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
        public static void Run(){
            foreach(bool light in new[]{false,true}){
                var owner=new Window{Width=400,Height=240,ShowInTaskbar=false};var target=new Button{Content="菜单检查"};owner.Content=target;Polish.Apply(owner,light);
                var menu=MenuTheme.Create(light);var group=new MenuItem{Header="移动到分组"};var child=new MenuItem{Header="创作 / 中文长分组名称"};var nested=new MenuItem{Header="嵌套菜单"};var leaf=new MenuItem{Header="游戏"};nested.Items.Add(leaf);group.Items.Add(child);group.Items.Add(nested);group.Items.Add(new Separator());
                var check=new MenuItem{Header="系统音乐模式",IsCheckable=true,IsChecked=true};menu.Items.Add(check);menu.Items.Add(new Separator());menu.Items.Add(group);bool clicked=false;child.Click+=(s,e)=>clicked=true;
                try{
                    owner.Show();menu.PlacementTarget=target;menu.IsOpen=true;Drain();menu.UpdateLayout();group.ApplyTemplate();group.IsSubmenuOpen=true;Drain();
                    foreach(var node in new[]{group,child,nested}){node.ApplyTemplate();if(!object.ReferenceEquals(node.Style,menu.Resources[typeof(MenuItem)]))throw new Exception("子菜单未使用共享主题");if(!object.ReferenceEquals(node.Foreground,Polish.Ink(light)))throw new Exception("菜单文字未使用主题颜色");}
                    var popup=group.Template.FindName("PART_Popup",group) as Popup;var surface=popup==null?null:group.Template.FindName("SubmenuSurface",group) as Border;
                    if(surface==null||surface.Background==null||PopupChrome.GetMaterial(surface)!=(light?1:0))throw new Exception("子菜单未使用共享玻璃材质");
                    group.IsSubmenuOpen=false;Drain();if(popup.IsOpen)throw new Exception("子菜单关闭绑定失败");group.IsSubmenuOpen=true;Drain();nested.IsSubmenuOpen=true;Drain();leaf.ApplyTemplate();if(!object.ReferenceEquals(leaf.Style,child.Style))throw new Exception("三级菜单未继承主题");
                    check.ApplyTemplate();var mark=check.Template.FindName("Check",check) as TextBlock;if(mark==null||mark.Visibility!=Visibility.Visible)throw new Exception("菜单选中标记丢失");check.IsChecked=false;if(mark.Visibility!=Visibility.Collapsed)throw new Exception("菜单取消选中标记失败");
                    child.IsEnabled=false;var chrome=(Border)child.Template.FindName("Chrome",child);if(chrome.Opacity>=1)throw new Exception("菜单禁用样式丢失");child.IsEnabled=true;child.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));if(!clicked)throw new Exception("分组菜单点击事件丢失");
                    var slider=new Slider{Minimum=28,Maximum=80,Value=48,Width=180};menu.Items.Add(new MenuItem{Header=slider,StaysOpenOnClick=true});Drain();slider.Value=56;if(slider.Value!=56)throw new Exception("菜单内图标大小滑块失败");
                }finally{menu.IsOpen=false;owner.Close();}
            }
            using(var stream=new MemoryStream(Encoding.UTF8.GetBytes("{\"IconSize\":56,\"ShowRunning\":false}"))){var options=(DockOptions)new DataContractJsonSerializer(typeof(DockOptions)).ReadObject(stream);if(options.ShowShortcutOverlays||options.SharperIcons||options.IconSize!=56||options.ShowRunning)throw new Exception("旧配置角标默认值或原设置被改变");}
            var folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Lumina-icon-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);object shell=null,link=null;
            try{
                string path=System.IO.Path.Combine(folder,"中文 快捷方式.lnk");var type=Type.GetTypeFromProgID("WScript.Shell");shell=Activator.CreateInstance(type);link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});var linkType=link.GetType();linkType.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{Environment.GetFolderPath(Environment.SpecialFolder.System)+"\\notepad.exe"});linkType.InvokeMember("IconLocation",BindingFlags.SetProperty,null,link,new object[]{Environment.GetFolderPath(Environment.SpecialFolder.System)+"\\shell32.dll,3"});linkType.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
                var original=File.ReadAllBytes(path);var plain=Native.Icon(path,false);var overlay=Native.Icon(path,true);if(plain==null||overlay==null||!object.ReferenceEquals(plain,Native.Icon(path))||object.ReferenceEquals(plain,overlay))throw new Exception("快捷方式角标开关或图标缓存失败");if(Convert.ToBase64String(original)!=Convert.ToBase64String(File.ReadAllBytes(path)))throw new Exception("读取图标改动了快捷方式");var sharp=Native.Icon(path,false,true) as System.Windows.Media.Imaging.BitmapSource;var sharpOverlay=Native.Icon(path,true,true);if(sharp==null||sharpOverlay==null||!sharp.IsFrozen||sharp.PixelWidth>128||sharp.PixelHeight>128||object.ReferenceEquals(sharp,plain))throw new Exception("高清图标尺寸、角标组合或缓存失败");Native.ClearIconCache();if(object.ReferenceEquals(plain,Native.Icon(path)))throw new Exception("关闭高清图标没有清除缓存");if(Native.Icon(folder)==null)throw new Exception("文件夹无角标图标失败");
            }finally{if(link!=null&&Marshal.IsComObject(link))Marshal.ReleaseComObject(link);if(shell!=null&&Marshal.IsComObject(shell))Marshal.ReleaseComObject(shell);Directory.Delete(folder,true);}
        }
    }
}

