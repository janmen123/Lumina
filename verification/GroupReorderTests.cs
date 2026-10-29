using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using GlassBoard;

public static class GroupReorderTests {
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static Preferences RoundTrip(Preferences prefs) {
        using (var stream = new MemoryStream()) {
            var serializer = new DataContractJsonSerializer(typeof(Preferences));
            serializer.WriteObject(stream, prefs); stream.Position = 0;
            var restored = (Preferences)serializer.ReadObject(stream);
            restored.Normalize(); return restored;
        }
    }
    [STAThread]
    public static int Main() {
        var initial = new[] { "学习", "游戏", "社交", "工具", "软件", "文件夹", "日常", "系统" };
        // Every source, target and insertion edge, including adjacent and no-op moves.
        for (int source = 0; source < initial.Length; source++)
        for (int target = 0; target < initial.Length; target++)
        foreach (bool before in new[] { true, false }) {
            var actual = initial.ToList();
            var expected = initial.Where((name, index) => index != source).ToList();
            if (source == target) expected = initial.ToList();
            else expected.Insert(expected.IndexOf(initial[target]) + (before ? 0 : 1), initial[source]);
            bool changed = GroupOrderRules.Move(actual, initial[source], initial[target], before);
            Check(actual.SequenceEqual(expected), "source/target order");
            Check(changed == !initial.SequenceEqual(expected), "no-op result");
            Check(actual.Distinct().Count() == initial.Length, "group membership preserved");
        }
        foreach (string invalid in new[] { null, "", "全部", "未分组", "不存在" }) {
            var groups = initial.ToList();
            Check(!GroupOrderRules.Move(groups, invalid, "学习", true), "invalid source rejected");
            Check(!GroupOrderRules.Move(groups, "学习", invalid, false), "invalid target rejected");
            Check(groups.SequenceEqual(initial), "invalid drop keeps order");
        }
        var prefs = new Preferences { Groups = initial.ToList() };
        prefs.Apps.Add(new Entry { Name = "示例", Path = "C:\\example.exe", Group = "学习",
            ExtraGroups = new List<string> { "工具" }, Pinned = true });
        var history = new BoardEditHistory();
        var snapshot = BoardEditState.Capture(prefs);
        GroupOrderRules.Move(prefs.Groups, "系统", "学习", true);
        history.Remember(snapshot, prefs, "调整分组顺序");
        var moved = prefs.Groups.ToArray();
        var loaded = RoundTrip(prefs);
        Check(loaded.Groups.SequenceEqual(moved), "order survives save, load and normalization");
        Check(loaded.Apps[0].Group == "学习" && loaded.Apps[0].ExtraGroups.Contains("工具")
            && loaded.Apps[0].Pinned, "app group membership and pin preserved");
        history.Undo(prefs); Check(prefs.Groups.SequenceEqual(initial), "undo");
        history.Redo(prefs); Check(prefs.Groups.SequenceEqual(moved), "redo");
        FeatureRules.AddShared(prefs, new[] { "C:\\example.exe" }, "游戏");
        Check(prefs.Groups.SequenceEqual(moved), "adding app to group preserves order");
        FeatureRules.RenameGroup(prefs, "系统", "系统工具");
        Check(prefs.Groups[0] == "系统工具", "renaming preserves position");

        // Use the application's nonpersistent controller, without showing windows or touching user settings.
        var controller = (Controller)Activator.CreateInstance(typeof(Controller),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { false }, null);
        controller.Prefs.Groups = initial.ToList();
        var board = controller.Board;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var dragRunning = typeof(BoardWindow).GetField("groupDragRunning", flags);
        var canDrop = typeof(BoardWindow).GetMethod("CanDropGroup", flags);
        var groupData = new DataObject(); groupData.SetData("GlassBoard.GroupTabOrder", "学习");
        Func<IDataObject, string, bool> accepts = (data, name) => (bool)canDrop.Invoke(board, new object[] { data, name });
        Check(!accepts(groupData, "游戏"), "external group payload rejected");
        dragRunning.SetValue(board, true);
        Check(accepts(groupData, "游戏"), "own group drag accepted");
        Check(!accepts(groupData, "全部") && !accepts(groupData, "未分组"), "built-in groups fixed");
        Check(!accepts(groupData, "学习"), "self drop rejected");
        var appData = new DataObject(); appData.SetData("GlassBoard.Entry", "C:\\example.exe");
        Check(!accepts(appData, "游戏"), "app drag not interpreted as group reorder");
        var fileData = new DataObject(DataFormats.FileDrop, new[] { "C:\\example.exe" });
        Check(!accepts(fileData, "游戏"), "file drag not interpreted as group reorder");
        dragRunning.SetValue(board, false);
        Check(controller.ReorderGroup("系统", "学习", true), "controller integration");
        Check(controller.CanUndoRemoval && controller.Prefs.Groups[0] == "系统", "controller records undo");
        var box = new Border();
        typeof(BoardWindow).GetMethod("AttachGroupReorder", flags).Invoke(board, new object[] { box, "游戏" });
        Check(box.ToolTip.ToString().Contains("拖动"), "drag instructions available");
        // Exercise the real routed drop handlers on an in-memory board, with no desktop window.
        controller.Prefs.Groups = initial.ToList();
        board.Width = 1200; board.Height = 800;
        typeof(BoardWindow).GetField("preparingView", flags).SetValue(board, true);
        typeof(BoardWindow).GetMethod("BuildForSize", flags).Invoke(board, null);
        var bar = (WrapPanel)typeof(BoardWindow).GetField("folderBar", flags).GetValue(board);
        bar.Measure(new Size(1200, 200)); bar.Arrange(new Rect(0, 0, 1200, 200));
        var targetBox = bar.Children.OfType<Border>().First(b => (string)b.Tag == "学习");
        var dragged = new DataObject(); dragged.SetData("GlassBoard.GroupTabOrder", "系统");
        var dragArgs = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs),
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
            new object[] { dragged, DragDropKeyStates.LeftMouseButton, DragDropEffects.Move, targetBox, new Point(1, 1) }, null);
        dragRunning.SetValue(board, true);
        dragArgs.RoutedEvent = DragDrop.PreviewDragOverEvent;
        targetBox.RaiseEvent(dragArgs);
        Check(dragArgs.Handled && dragArgs.Effects == DragDropEffects.Move, "group drag routed to reorder handler");
        Check(controller.Prefs.Groups.SequenceEqual(initial), "hover does not commit order");
        dragArgs.RoutedEvent = DragDrop.PreviewDropEvent; dragArgs.Handled = false;
        targetBox.RaiseEvent(dragArgs);
        Check(dragArgs.Handled && controller.Prefs.Groups[0] == "系统", "drop commits before target");
        dragRunning.SetValue(board, false); board.Render();
        Check(bar.Children.OfType<Border>().Select(b => (string)b.Tag).SequenceEqual(
            new[] { "全部", "未分组" }.Concat(controller.Prefs.Groups)), "visible tab order follows saved order");
        var controllerHistory = (BoardEditHistory)typeof(Controller).GetField("editHistory", flags).GetValue(controller);
        controllerHistory.Undo(controller.Prefs); board.SyncGroups(); board.Render();
        Check(controller.Prefs.Groups.SequenceEqual(initial), "UI move can be undone");
        controller.ShuttingDown = true; board.Close(); controller.Dock.Close();
        Console.WriteLine("PASS: " + checks + " group reorder checks");
        return 0;
    }
}
