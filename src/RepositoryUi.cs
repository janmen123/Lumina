using System;using System.Diagnostics;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
namespace GlassBoard {
    public partial class Controller {
        public const string RepositoryUrl="https://github.com/SENLIN-SAMA/win-.-";
        static Version CurrentVersion{get{return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;}}
        public static void OpenRepository(){try{Process.Start(new ProcessStartInfo(RepositoryUrl){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(ex.Message,"无法打开 GitHub");}}
        void BuildUpdateSettings(StackPanel panel,Window owner){
            panel.Children.Add(new TextBlock{Text="版本与下载 · v"+CurrentVersion.ToString(3),FontSize=18,Margin=new Thickness(0,22,0,10)});
            panel.Children.Add(new TextBlock{Text="新版本请前往 GitHub 仓库下载。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray});
            panel.Children.Add(FeatureUi.Button("打开 GitHub 仓库",OpenRepository));
            var address=new TextBox{Text=RepositoryUrl,IsReadOnly=true,Margin=new Thickness(0,8,0,8),MinHeight=36};panel.Children.Add(address);
        }
    }
}
