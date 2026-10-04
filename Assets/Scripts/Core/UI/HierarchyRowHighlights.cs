using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    internal sealed class HierarchyRowHighlights
    {
        private readonly List<SceneTreeRowView> _rows = new List<SceneTreeRowView>();

        public int Count => _rows.Count;

        public void Forget() => _rows.Clear();

        public void Remember(SceneTreeRowView row) => _rows.Add(row);

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
                if (row == null || row.Node == null) continue;
                row.Paint(Highlighted(row.Node, sel));
            }
        }
    }
}
