using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal sealed class HierarchyRowHighlights
    {
        private readonly List<Row> _rows = new List<Row>();

        private readonly struct Row
        {
            public Row(Image tinted, SceneTree.Node node, Color resting)
            {
                Tinted = tinted;
                Node = node;
                Resting = resting;
            }

            public readonly Image Tinted;
            public readonly SceneTree.Node Node;
            public readonly Color Resting;
        }

        public int Count => _rows.Count;

        public void Forget() => _rows.Clear();

        public void Remember(Image tinted, SceneTree.Node node, Color resting) =>
            _rows.Add(new Row(tinted, node, resting));

        public static bool Highlighted(SceneTree.Node node, SelectionManager? sel)
        {
            if (sel == null || node.isRoot) return false;
            if (node.group == null)
                return node.element != null && sel.IsSelected(node.element);

            var members = GroupManager.MembersOf(node.group);
            if (members.Count == 0) return false;
            foreach (var m in members)
                if (!sel.IsSelected(m)) return false;
            return true;
        }

        public void Repaint(SelectionManager? sel)
        {
            foreach (var row in _rows)
            {
                if (row.Tinted == null) continue;
                row.Tinted.color = Highlighted(row.Node, sel) ? UIStyle.RowSelected : row.Resting;
            }
        }
    }
}
