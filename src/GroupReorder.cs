using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Lumina {
    public static class GroupOrderRules {
        public static bool IsCustom(string name) {
            return !string.IsNullOrWhiteSpace(name) && name != "全部" && name != "未分组";
        }

        public static bool Move(List<string> groups, string source, string target, bool before) {
            if (groups == null || !IsCustom(source) || !IsCustom(target) || source == target) return false;
            int from = groups.IndexOf(source), to = groups.IndexOf(target);
            if (from < 0 || to < 0) return false;
            int insertion = to + (before ? 0 : 1);
            if (from < insertion) insertion--;
            if (from == insertion) return false;
            groups.RemoveAt(from);
            groups.Insert(insertion, source);
            return true;
        }
    }

    public partial class Controller {
        public bool ReorderGroup(string source, string target, bool before) {
            var state = BeginBoardEdit();
            if (!GroupOrderRules.Move(Prefs.Groups, source, target, before)) return false;
            EndBoardEdit(state, "调整分组顺序");
            Board.SyncGroups();
            Board.Render();
            Board.SetStatus("分组顺序已保存 · Ctrl+Z 撤销");
            return true;
        }
    }

    public partial class BoardWindow {
        const string GroupTabDragFormat = "Lumina.GroupTabOrder";
        bool groupDragRunning;
        string groupDragCandidate;
        Point groupDragStart;
        Border groupDropMarker;

        void ClearGroupDropMarker() {
            if (groupDropMarker != null) groupDropMarker.BorderBrush = Brushes.Transparent;
            groupDropMarker = null;
        }

        bool CanDropGroup(IDataObject data, string target) {
            if (!groupDragRunning || !data.GetDataPresent(GroupTabDragFormat)) return false;
            string source = data.GetData(GroupTabDragFormat) as string;
            return GroupOrderRules.IsCustom(source) && GroupOrderRules.IsCustom(target)
                && source != target && C.Prefs.Groups.Contains(source) && C.Prefs.Groups.Contains(target);
        }

        void AttachGroupReorder(Border box, string name) {
            bool custom = GroupOrderRules.IsCustom(name);
            // Reserve both edges so the insertion marker never shifts the tabs under the pointer.
            box.BorderThickness = new Thickness(2, 0, 2, 0);
            box.BorderBrush = Brushes.Transparent;
            if (custom) box.ToolTip = "点击打开；按住分组左右拖动可排序；拖入应用可归类";
            box.MouseLeftButtonDown += (s, e) => {
                groupDragCandidate = name;
                groupDragStart = e.GetPosition(this);
            };
            box.MouseLeave += (s, e) => {
                if (!groupDragRunning && groupDragCandidate == name) groupDragCandidate = null;
            };
            box.MouseLeftButtonUp += (s, e) => {
                if (!groupDragRunning && groupDragCandidate == name) {
                    groupDragCandidate = null;
                    OpenGroup(name);
                }
                e.Handled = true;
            };
            box.MouseMove += (s, e) => {
                if (!custom || groupDragRunning || groupDragCandidate != name || e.LeftButton != MouseButtonState.Pressed) return;
                Point point = e.GetPosition(this);
                if (Math.Abs(point.X - groupDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(point.Y - groupDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                groupDragCandidate = null;
                EndDragPreview();
                Focus();
                bool previousBusy = ModalBusy;
                ModalBusy = true;
                groupDragRunning = true;
                e.Handled = true;
                try {
                    var payload = new DataObject();
                    payload.SetData(GroupTabDragFormat, name);
                    DragDrop.DoDragDrop(box, payload, DragDropEffects.Move);
                } finally {
                    groupDragRunning = false;
                    groupDragCandidate = null;
                    ClearGroupDropMarker();
                    ModalBusy = previousBusy;
                    folderSignature = "";
                    Render();
                }
            };
            DragEventHandler preview = (s, e) => {
                if (!e.Data.GetDataPresent(GroupTabDragFormat)) return;
                e.Handled = true;
                ClearGroupDropMarker();
                StopGroupHover(null);
                bool allowed = CanDropGroup(e.Data, name);
                e.Effects = allowed ? DragDropEffects.Move : DragDropEffects.None;
                if (!allowed) return;
                bool before = e.GetPosition(box).X < box.ActualWidth / 2;
                Color accent = Color.FromRgb(110, 195, 255);
                box.BorderBrush = new LinearGradientBrush(before ? accent : Colors.Transparent,
                    before ? Colors.Transparent : accent, 0);
                groupDropMarker = box;
            };
            box.PreviewDragEnter += preview;
            box.PreviewDragOver += preview;
            box.PreviewDragLeave += (s, e) => {
                if (!e.Data.GetDataPresent(GroupTabDragFormat)) return;
                if (groupDropMarker == box) ClearGroupDropMarker();
                e.Handled = true;
            };
            box.PreviewDrop += (s, e) => {
                if (!e.Data.GetDataPresent(GroupTabDragFormat)) return;
                e.Handled = true;
                bool allowed = CanDropGroup(e.Data, name);
                e.Effects = allowed ? DragDropEffects.Move : DragDropEffects.None;
                ClearGroupDropMarker();
                if (allowed) C.ReorderGroup((string)e.Data.GetData(GroupTabDragFormat), name,
                    e.GetPosition(box).X < box.ActualWidth / 2);
            };
        }
    }
}
