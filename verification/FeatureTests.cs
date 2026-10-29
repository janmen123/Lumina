using System;using System.Reflection;using System.Collections;using System.Collections.Generic;using GlassBoard;
class FeatureTests {
static int count;static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
[STAThread]static int Main(){try{
var flags=BindingFlags.Static|BindingFlags.NonPublic;
var quote=typeof(DockWindow).GetMethod("QuoteDropArgument",flags);
Check((string)quote.Invoke(null,new object[]{@"C:\my files\a.png"})=="\"C:\\my files\\a.png\"","path with spaces");
Check((string)quote.Invoke(null,new object[]{"https://example.com/?q=a&b=2"})=="\"https://example.com/?q=a&b=2\"","URL argument isolation");
Check((string)quote.Invoke(null,new object[]{"C:\\folder\\"})=="\"C:\\folder\\\\\"","trailing slash escaping");
var list=(List<RunningWindow>)typeof(RunningWindows).GetField("windows",flags).GetValue(null);var match=typeof(RunningWindows).GetMethod("MatchWindows",flags);
Func<Entry,string[],List<RunningWindow>> run=(entry,paths)=>(List<RunningWindow>)match.Invoke(null,new object[]{entry,paths});
list.Add(new RunningWindow{Path=@"C:\Apps\v2\editor.exe",Title="Editor document"});list.Add(new RunningWindow{Path=@"C:\Apps\v1\editor.exe",Title="Editor old"});
Check(run(new Entry{Name="Editor"},new[]{@"C:\Apps\v1\editor.exe"}).Count==1,"exact path takes priority");
Check(run(new Entry{Name="Editor"},new[]{@"C:\Apps\v3\editor.exe"}).Count==2,"executable filename fallback");
list.Clear();list.Add(new RunningWindow{Path=@"D:\DSH\DeepSeek Harness.exe",Title="Chat - DeepSeek Harness"});
Check(run(new Entry{Name="DSH"},new[]{@"D:\DSH\Update.exe"}).Count==1,"DSH launcher fallback");
list.Clear();list.Add(new RunningWindow{Path=@"D:\Douyin\5\douyin.exe",Title="douyin"});
Check(run(new Entry{Name="抖音"},new[]{@"D:\Douyin\Launcher.exe"}).Count==1,"Douyin launcher fallback");
Check(run(new Entry{Name="Other"},new[]{@"D:\Other\Launcher.exe"}).Count==0,"unrelated window excluded");
var root=new System.Windows.Controls.StackPanel();root.Children.Add(new System.Windows.Controls.TextBlock{Text="透明度"});var search=typeof(Controller).GetMethod("SearchElements",flags);var found=(IEnumerable)search.Invoke(null,new object[]{root});int n=0;foreach(var x in found)n++;Check(n==1,"settings logical traversal");
Console.WriteLine(count+" checks passed");return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
