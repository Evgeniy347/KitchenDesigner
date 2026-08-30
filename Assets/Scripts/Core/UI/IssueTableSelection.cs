using System.Collections.Generic;
using KitchenDesigner.Core.Analysis;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class IssueTableSelection
    {
        private readonly List<AnalysisIssue> _visible = new();
        private readonly HashSet<AnalysisIssue> _selected = new();
        private AnalysisIssue? _anchor;
        private int _anchorVisibleIdx = -1;
        private int _focusVisibleIdx = -1;

        public IReadOnlyList<AnalysisIssue> Visible => _visible;
        public int VisibleCount => _visible.Count;
        public int SelectedCount => _selected.Count;
        public int FocusVisibleIdx => _focusVisibleIdx;
        public bool IsSelected(AnalysisIssue iss) => _selected.Contains(iss);

        public void Clear()
        {
            _selected.Clear();
            _anchor = null;
            _anchorVisibleIdx = -1;
            _focusVisibleIdx = -1;
        }

        public void BeginRebuild() => _visible.Clear();

        public void AddVisible(AnalysisIssue iss) => _visible.Add(iss);

        public void EndRebuild()
        {
            if (_focusVisibleIdx >= _visible.Count) _focusVisibleIdx = -1;
            if (_anchorVisibleIdx >= _visible.Count) _anchorVisibleIdx = -1;
            if (!_anchor.HasValue || _anchorVisibleIdx >= 0) return;

            var anchor = _anchor.Value;
            for (int i = 0; i < _visible.Count; i++)
            {
                if (!anchor.Equals(_visible[i])) continue;
                _anchorVisibleIdx = i;
                return;
            }
        }

        public bool Click(AnalysisIssue iss, int index, bool ctrl, bool shift)
        {
            bool selectsInScene = false;

            if (shift && _anchor.HasValue && _anchorVisibleIdx >= 0
                && _anchorVisibleIdx < _visible.Count
                && index >= 0 && index < _visible.Count)
            {
                int from = Mathf.Min(_anchorVisibleIdx, index);
                int to = Mathf.Max(_anchorVisibleIdx, index);
                for (int i = from; i <= to; i++) _selected.Add(_visible[i]);
            }
            else if (ctrl)
            {
                if (!_selected.Remove(iss)) _selected.Add(iss);
            }
            else
            {
                _selected.Clear();
                _selected.Add(iss);
                _anchor = iss;
                _anchorVisibleIdx = index;
                selectsInScene = true;
            }

            _focusVisibleIdx = index;
            return selectsInScene;
        }

        public void SelectAll()
        {
            if (_visible.Count == 0) return;
            _selected.Clear();
            for (int i = 0; i < _visible.Count; i++) _selected.Add(_visible[i]);
        }

        public bool MoveFocusTo(int visibleIndex)
        {
            if (visibleIndex < 0 || visibleIndex >= _visible.Count) return false;
            var iss = _visible[visibleIndex];
            _selected.Clear();
            _selected.Add(iss);
            _anchor = iss;
            _anchorVisibleIdx = visibleIndex;
            _focusVisibleIdx = visibleIndex;
            return true;
        }

        public string SelectedAsClipboardText()
        {
            if (_selected.Count == 0) return string.Empty;
            var sb = new System.Text.StringBuilder();
            bool first = true;
            for (int i = 0; i < _visible.Count; i++)
            {
                var iss = _visible[i];
                if (!_selected.Contains(iss)) continue;
                if (!first) sb.Append('\n');
                sb.Append(IssueDisplay.FormatRowForCopy(iss));
                first = false;
            }
            return sb.ToString();
        }
    }
}
