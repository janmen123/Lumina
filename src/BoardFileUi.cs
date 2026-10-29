using System;using System.IO;using System.Windows;using System.Windows.Controls;using System.Windows.Input;using System.Windows.Media;
namespace GlassBoard {
    public partial class BoardWindow {
        void BoardBlankMenu(object sender,MouseButtonEventArgs e){
            DependencyObject source=e.OriginalSource as DependencyObject;
            while(source!=null&&source!=this){var control=source as FrameworkElement;if(control!=null&&(control.Tag is Entry||control is System.Windows.Controls.Primitives.ButtonBase||control is TextBox||control is ComboBox||control is System.Windows.Controls.Primitives.Thumb||control is Slider))return;source=source is Visual?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);}
            NewFileMenu();e.Handled=true;
        }
        void NewFileMenu(){
            EndDragPreview();var menu=MenuTheme.Create(C.Prefs.Light);iconMenu=menu;menu.Closed+=(s,e)=>{if(iconMenu==menu)iconMenu=null;};
            string[] names={"文本文档 (.txt)","Markdown 文档 (.md)","Word 文档 (.docx)","Excel 工作簿 (.xlsx)","文件夹"};
            for(int i=0;i<names.Length;i++){int index=i;var item=new MenuItem{Header="新建"+names[i]};item.Click+=(s,e)=>{menu.IsOpen=false;C.CreateBoardFile(index==4?null:BoardFiles.Extensions[index],currentGroup);};menu.Items.Add(item);}
            menu.PlacementTarget=this;menu.IsOpen=true;
        }
    }
    public partial class Controller {
        public void CreateBoardFile(string extension,string group){
            bool previous=Board.ModalBusy;Board.ModalBusy=true;
            try{
                string path;
                if(extension==null){string parent=FeatureUi.PickFolder(OwnerWindow,"选择新文件夹的保存位置");if(parent==null)return;string name=FeatureUi.Ask(OwnerWindow,"新文件夹名称");if(name==null)return;path=BoardFiles.CreateFolder(parent,name);}
                else{
                    var pick=new Microsoft.Win32.SaveFileDialog{Title="新建文件并添加到当前分组",FileName="新建文档"+extension,DefaultExt=extension,AddExtension=true,Filter="所选格式 (*"+extension+")|*"+extension,InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)};
                    if(pick.ShowDialog(OwnerWindow)!=true)return;path=pick.FileName;BoardFiles.Create(path);
                }
                Add(new[]{path},true,group=="全部"?"未分组":group);Board.ClearSearch();Board.SetStatus("已新建："+Path.GetFileName(path)+" · 点击打开；移除入口不会删除原文件");
            }catch(Exception ex){Store.Log(ex);MessageBox.Show(OwnerWindow,ex.Message,"新建失败");}finally{Board.ModalBusy=previous;}
        }
        public void SaveSession(){
            try{Store.Save(Prefs);if(widgets!=null)widgets.Notes.Flush();Store.Trace("SESSION_END entries="+Prefs.Apps.Count);}catch(Exception ex){Store.Log(ex);}
        }
    }
}
