using System;using System.Collections.Generic;using System.Linq;
enum Visibility {Visible,Collapsed} enum MessageBoxResult {Yes,No} enum MessageBoxButton {YesNo}
static class MessageBox {public static MessageBoxResult Answer; public static MessageBoxResult Show(params object[] args){return Answer;}}
class GridLength {public double Value;public GridLength(double v){Value=v;}public static GridLength Auto=new GridLength(-1);}
class Control {public bool IsEnabled;public object Content;public string Text;public Visibility Visibility;public GridLength Height;}
class Entry {public string Path;}
class Prefs {public List<Entry> Apps=new List<Entry>();}
static class EntryRules {public static object Remove(Prefs p,HashSet<string> paths){p.Apps.RemoveAll(e=>paths.Contains(e.Path));return new object();}}
class Controller {public Prefs Prefs=new Prefs();public Board Board;object lastRemoval;public bool CanUndoRemoval {get{return lastRemoval!=null;}} public void Refresh(){Board.UpdateSelectionBar();}
public void RemoveMany(IEnumerable<string> paths) {
            var targets=new HashSet<string>(paths,StringComparer.OrdinalIgnoreCase);int count=Prefs.Apps.Count(a=>targets.Contains(a.Path));if(count==0)return;
            Board.ModalBusy=true;
            try {
                string text="从面板移除已选的 "+count+" 个入口？\n原软件和文件会保留，已固定的入口也会从 Dock 移除。\n移除后可点击「撤销移除」。";
                var answer=Board.IsVisible?MessageBox.Show(Board,text,"GlassBoard",MessageBoxButton.YesNo):MessageBox.Show(text,"GlassBoard",MessageBoxButton.YesNo);
                if(answer==MessageBoxResult.Yes) {lastRemoval=EntryRules.Remove(Prefs,targets);Board.FinishSelection();Refresh();Board.SetStatus("已移除 "+count+" 个入口 · 可撤销本次移除");}
            } finally {Board.ModalBusy=false;}
        }}
class Board {public Controller C;public bool ModalBusy,IsVisible=true;public bool editing;public object dragEntry;public HashSet<string> selected=new HashSet<string>();
public Control editSelection=new Control(),bulkBar=new Control(),bulkRow=new Control(),moveGroup=new Control(),selectAll=new Control(),clearSelection=new Control(),deleteSelected=new Control(),undoRemoval=new Control(),selectionCount=new Control();
public void SetStatus(string s){} public void Begin(){SetSelectionMode(true);UpdateSelectionBar();}
void SetSelectionMode(bool enabled) {editing=enabled;selected.Clear();dragEntry=null;if(editSelection!=null)editSelection.Content=enabled?"完成":"多选";}public void FinishSelection() {SetSelectionMode(false);}public void UpdateSelectionBar() {
            selected.RemoveWhere(p=>!C.Prefs.Apps.Any(a=>string.Equals(a.Path,p,StringComparison.OrdinalIgnoreCase)));
            bool visible=editing;bulkBar.Visibility=visible?Visibility.Visible:Visibility.Collapsed;bulkRow.Height=visible?GridLength.Auto:new GridLength(0);moveGroup.IsEnabled=editing;
            selectAll.IsEnabled=editing;clearSelection.IsEnabled=editing&&selected.Count>0;deleteSelected.IsEnabled=editing&&selected.Count>0;
            deleteSelected.Content="移除已选（"+selected.Count+"）";undoRemoval.IsEnabled=C.CanUndoRemoval;undoRemoval.Visibility=C.CanUndoRemoval?Visibility.Visible:Visibility.Collapsed;selectionCount.Text=editing?"已选 "+selected.Count+" · 跨页保留":"可撤销上次移除";
        }}
class Tests {static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);}static void Main(){
var c=new Controller();var b=new Board{C=c};c.Board=b;c.Prefs.Apps.Add(new Entry{Path="a"});c.Prefs.Apps.Add(new Entry{Path="b"});
b.Begin();b.selected.Add("a");b.UpdateSelectionBar();MessageBox.Answer=MessageBoxResult.No;c.RemoveMany(b.selected.ToArray());
Check(b.editing&&b.selected.Contains("a")&&b.bulkBar.Visibility==Visibility.Visible,"cancel must preserve selection");Check(!b.ModalBusy,"modal flag restored after cancel");
MessageBox.Answer=MessageBoxResult.Yes;c.RemoveMany(b.selected.ToArray());Check(c.Prefs.Apps.Count==1&&c.CanUndoRemoval,"confirmed removal");
Check(!b.editing&&b.selected.Count==0&&b.bulkBar.Visibility==Visibility.Collapsed&&b.bulkRow.Height.Value==0,"successful removal hides entire row");Check((string)b.editSelection.Content=="多选"&&b.undoRemoval.Visibility==Visibility.Visible,"button label and separate undo");Check(!b.ModalBusy,"modal flag restored after success");
b.Begin();b.selected.Add("b");b.FinishSelection();b.UpdateSelectionBar();Check(b.bulkBar.Visibility==Visibility.Collapsed&&c.CanUndoRemoval,"finish hides row even with undo");
b.Begin();b.selected.Add("missing");c.RemoveMany(b.selected.ToArray());Check(b.editing,"no targets leaves editing unchanged");
Console.WriteLine("PASS: production removal/selection methods with UI stubs; confirm, cancel, finish with undo, no-target and modal reset");}}
