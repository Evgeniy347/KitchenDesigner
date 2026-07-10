using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    /// <summary>Группа связанных объектов («замок»): общий id у элементов.</summary>
    public class LinkGroup
    {
        public int id;
        public string name = "Группа";
        public bool movable = true;
    }

    /// <summary>Связывание объектов в группу. Элемент хранит GroupId; реестр —
    /// сами группы (имя/подвижность). Состав группы выводится из BoardRegistry.</summary>
    public static class GroupManager
    {
        private static readonly Dictionary<int, LinkGroup> _groups = new Dictionary<int, LinkGroup>();
        private static int _nextId = 1;

        public static LinkGroup Link(IList<KitchenElement> members)
        {
            if (members == null || members.Count < 2) return null;
            var g = new LinkGroup { id = _nextId++ };
            _groups[g.id] = g;
            foreach (var m in members)
                if (m != null) m.GroupId = g.id;
            return g;
        }

        public static void Unlink(LinkGroup g)
        {
            if (g == null) return;
            foreach (var m in MembersOf(g))
                m.GroupId = 0;
            _groups.Remove(g.id);
        }

        public static LinkGroup GroupOf(KitchenElement e)
        {
            if (e == null || e.GroupId == 0) return null;
            return _groups.TryGetValue(e.GroupId, out var g) ? g : null;
        }

        public static List<KitchenElement> MembersOf(LinkGroup g)
        {
            var list = new List<KitchenElement>();
            if (g == null) return list;
            foreach (var e in BoardRegistry.GetAll())
                if (e != null && e.GroupId == g.id) list.Add(e);
            return list;
        }

        /// <summary>Применить подвижность группы ко всем её элементам.</summary>
        public static void SetMovable(LinkGroup g, bool movable)
        {
            if (g == null) return;
            g.movable = movable;
            foreach (var m in MembersOf(g))
                m.Movable = movable;
        }

        public static void Clear()
        {
            _groups.Clear();
            _nextId = 1;
        }

        public static IEnumerable<LinkGroup> AllGroups() => _groups.Values;

        /// <summary>Восстановление группы из сохранения.</summary>
        public static LinkGroup Register(int id, string name, bool movable)
        {
            var g = new LinkGroup { id = id, name = name, movable = movable };
            _groups[id] = g;
            if (id >= _nextId) _nextId = id + 1;
            return g;
        }
    }
}
