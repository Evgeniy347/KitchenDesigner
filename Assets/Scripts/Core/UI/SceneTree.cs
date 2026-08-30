using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    /// <summary>
    /// Чистая модель дерева иерархии сцены для панели HierarchyPanelUI:
    ///   Кухня (корень)
    ///   ├─ Группа/модуль
    ///   │   ├─ элемент
    ///   │   │   └─ прикреплённый фасад (у ящика)
    ///   └─ элементы вне групп
    /// Никакого UI — только упорядоченный плоский список узлов с глубиной,
    /// поэтому логика полностью покрывается EditMode-тестами.
    /// </summary>
    public static class SceneTree
    {
        public class Node
        {
            public LinkGroup? group;        // не null — строка-группа
            public KitchenElement? element; // не null — строка-элемент
            public int depth;               // 0 — корень, 1 — группа/внегрупповой элемент…
            public bool isRoot;             // строка «Кухня»
            public bool hasChildren;        // есть что сворачивать (для стрелки)
            public bool collapsed;          // группа свёрнута (дети не выводятся)
        }

        /// <summary>Построить плоский список строк дерева в порядке отрисовки.</summary>
        /// <param name="all">Все элементы сцены (PartRegistry.GetAll()).</param>
        /// <param name="groups">Все группы (GroupManager.AllGroups()).</param>
        /// <param name="collapsedGroupIds">Свёрнутые группы (их члены не выводятся).</param>
        public static List<Node> Build(
            IReadOnlyList<KitchenElement> all,
            IEnumerable<LinkGroup> groups,
            ISet<int>? collapsedGroupIds = null)
        {
            var nodes = new List<Node>();
            var collapsed = collapsedGroupIds ?? new HashSet<int>();

            // Пристёгнутые фасады показываются ПОД своим хозяином (ящиком или
            // посудомойкой), а не на своём обычном месте.
            var attachedFacades = new Dictionary<KitchenElement, KitchenElement>(); // facade -> host
            foreach (var e in all)
            {
                if (e is IFacadeHost host && !string.IsNullOrEmpty(host.AttachedFacadeName))
                {
                    var facade = FindByName(all, host.AttachedFacadeName);
                    if (facade != null && ElementFacets.Of(facade).Has(ElementFacet.Facade))
                        attachedFacades[facade] = e;
                }
            }

            nodes.Add(new Node { isRoot = true, depth = 0, hasChildren = all.Count > 0 });

            // Группы — по id (порядок создания стабилен).
            var sortedGroups = new List<LinkGroup>(groups);
            sortedGroups.Sort((a, b) => a.id.CompareTo(b.id));

            var groupIds = new HashSet<int>();
            foreach (var g in sortedGroups)
            {
                groupIds.Add(g.id);
                var members = CollectMembers(all, g.id, attachedFacades);
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
                        AddElementRows(nodes, all, m, 2, attachedFacades);
            }

            // Вне групп (включая элементы с «мёртвым» GroupId, чья группа не существует).
            var ungrouped = new List<KitchenElement>();
            foreach (var e in all)
            {
                if (e == null || attachedFacades.ContainsKey(e)) continue;
                if (e.GroupId == 0 || !groupIds.Contains(e.GroupId))
                    ungrouped.Add(e);
            }
            SortByName(ungrouped);
            foreach (var e in ungrouped)
                AddElementRows(nodes, all, e, 1, attachedFacades);

            return nodes;
        }

        private static void AddElementRows(List<Node> nodes, IReadOnlyList<KitchenElement> all,
            KitchenElement e, int depth, Dictionary<KitchenElement, KitchenElement> attachedFacades)
        {
            KitchenElement? child = null;
            if (e is IFacadeHost host && !string.IsNullOrEmpty(host.AttachedFacadeName))
            {
                var facade = FindByName(all, host.AttachedFacadeName);
                if (facade != null && ElementFacets.Of(facade).Has(ElementFacet.Facade)) child = facade;
            }

            nodes.Add(new Node { element = e, depth = depth, hasChildren = child != null });
            if (child != null)
                nodes.Add(new Node { element = child, depth = depth + 1 });
        }

        private static List<KitchenElement> CollectMembers(IReadOnlyList<KitchenElement> all,
            int groupId, Dictionary<KitchenElement, KitchenElement> attachedFacades)
        {
            var members = new List<KitchenElement>();
            foreach (var e in all)
                if (e != null && e.GroupId == groupId && !attachedFacades.ContainsKey(e))
                    members.Add(e);
            SortByName(members);
            return members;
        }

        private static void SortByName(List<KitchenElement> list) =>
            list.Sort((a, b) => string.Compare(a.PartName, b.PartName, StringComparison.OrdinalIgnoreCase));

        private static KitchenElement? FindByName(IReadOnlyList<KitchenElement> all, string name)
        {
            foreach (var e in all)
                if (e != null && e.PartName == name) return e;
            return null;
        }
    }
}
