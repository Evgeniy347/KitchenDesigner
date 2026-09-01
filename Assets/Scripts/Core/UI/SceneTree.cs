using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public static class SceneTree
    {
        public class Node
        {
            public LinkGroup? group;
            public KitchenElement? element;
            public int depth;
            public bool isRoot;
            public bool hasChildren;
            public bool collapsed;
        }

        public static List<Node> Build(
            IReadOnlyList<KitchenElement> allElements,
            IEnumerable<LinkGroup> groups,
            ISet<int>? collapsedGroupIds = null)
        {
            var nodes = new List<Node>();
            var collapsed = collapsedGroupIds ?? new HashSet<int>();
            var facadeToHost = MapAttachedFacadesToHosts(allElements);

            nodes.Add(new Node { isRoot = true, depth = 0, hasChildren = allElements.Count > 0 });

            var groupsByCreationId = new List<LinkGroup>(groups);
            groupsByCreationId.Sort((a, b) => a.id.CompareTo(b.id));

            var existingGroupIds = new HashSet<int>();
            foreach (var g in groupsByCreationId)
            {
                existingGroupIds.Add(g.id);
                var members = CollectMembers(allElements, g.id, facadeToHost);
                bool isCollapsed = collapsed.Contains(g.id);
                nodes.Add(new Node
                {
                    group = g,
                    depth = 1,
                    hasChildren = members.Count > 0,
                    collapsed = isCollapsed
                });
                if (!isCollapsed)
                    foreach (var m in members)
                        AddElementRows(nodes, allElements, m, 2);
            }

            var ungrouped = new List<KitchenElement>();
            foreach (var e in allElements)
            {
                if (e == null || facadeToHost.ContainsKey(e)) continue;
                if (e.GroupId == 0 || !existingGroupIds.Contains(e.GroupId))
                    ungrouped.Add(e);
            }
            SortByName(ungrouped);
            foreach (var e in ungrouped)
                AddElementRows(nodes, allElements, e, 1);

            return nodes;
        }

        private static Dictionary<KitchenElement, KitchenElement> MapAttachedFacadesToHosts(
            IReadOnlyList<KitchenElement> allElements)
        {
            var facadeToHost = new Dictionary<KitchenElement, KitchenElement>();
            foreach (var e in allElements)
            {
                var facade = AttachedFacadeOf(allElements, e);
                if (facade != null) facadeToHost[facade] = e;
            }
            return facadeToHost;
        }

        private static KitchenElement? AttachedFacadeOf(IReadOnlyList<KitchenElement> allElements,
            KitchenElement? owner)
        {
            if (owner is not IFacadeHost host || string.IsNullOrEmpty(host.AttachedFacadeName))
                return null;

            var facade = FindByName(allElements, host.AttachedFacadeName);
            return facade != null && ElementFacets.Of(facade).Has(ElementFacet.Facade) ? facade : null;
        }

        private static void AddElementRows(List<Node> nodes, IReadOnlyList<KitchenElement> allElements,
            KitchenElement e, int depth)
        {
            var child = AttachedFacadeOf(allElements, e);

            nodes.Add(new Node { element = e, depth = depth, hasChildren = child != null });
            if (child != null)
                nodes.Add(new Node { element = child, depth = depth + 1 });
        }

        private static List<KitchenElement> CollectMembers(IReadOnlyList<KitchenElement> allElements,
            int groupId, Dictionary<KitchenElement, KitchenElement> facadeToHost)
        {
            var members = new List<KitchenElement>();
            foreach (var e in allElements)
                if (e != null && e.GroupId == groupId && !facadeToHost.ContainsKey(e))
                    members.Add(e);
            SortByName(members);
            return members;
        }

        private static void SortByName(List<KitchenElement> list) =>
            list.Sort((a, b) => string.Compare(a.PartName, b.PartName, StringComparison.OrdinalIgnoreCase));

        private static KitchenElement? FindByName(IReadOnlyList<KitchenElement> allElements, string name)
        {
            foreach (var e in allElements)
                if (e != null && e.PartName == name) return e;
            return null;
        }
    }
}
