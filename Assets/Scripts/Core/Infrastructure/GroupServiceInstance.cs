using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Реализация <see cref="IGroupService"/>. Хранит реестр групп; принадлежность
    /// элемента группе — это его GroupId, состав группы выводится из PartRegistry
    /// (как и раньше в GroupManager).
    /// </summary>
    public class GroupServiceInstance : IGroupService
    {
        private readonly Dictionary<int, LinkGroup> _groups = new Dictionary<int, LinkGroup>();
        private int _nextId = 1;

        public event Action? Changed;

        private void RaiseChanged() => Changed?.Invoke();

        public LinkGroup Create(string name)
        {
            var g = new LinkGroup { id = _nextId++, name = string.IsNullOrEmpty(name) ? "Группа" : name };
            _groups[g.id] = g;
            RaiseChanged();
            return g;
        }

        public LinkGroup? Link(IList<KitchenElement> members)
        {
            if (members == null || members.Count < 2) return null;
            var g = new LinkGroup { id = _nextId++ };
            _groups[g.id] = g;
            foreach (var m in members)
                if (m != null) m.GroupId = g.id;
            RaiseChanged();
            return g;
        }

        public void Unlink(LinkGroup g)
        {
            if (g == null || !_groups.ContainsKey(g.id)) return;
            if (ModuleEditMode.Active == g) ModuleEditMode.Exit(); // роспуск редактируемого модуля
            foreach (var m in MembersOf(g))
                m.GroupId = 0;
            _groups.Remove(g.id);
            RaiseChanged();
        }

        public LinkGroup? GroupOf(KitchenElement e)
        {
            if (e == null || e.GroupId == 0) return null;
            return _groups.TryGetValue(e.GroupId, out var g) ? g : null;
        }

        public List<KitchenElement> MembersOf(LinkGroup g)
        {
            var list = new List<KitchenElement>();
            if (g == null) return list;
            foreach (var e in PartRegistry.GetAll())
                if (e != null && e.GroupId == g.id) list.Add(e);
            return list;
        }

        public IEnumerable<LinkGroup> AllGroups() => _groups.Values;

        public void AddTo(LinkGroup g, KitchenElement e)
        {
            if (g == null || e == null || !_groups.ContainsKey(g.id)) return;
            if (e.GroupId == g.id) return;
            e.GroupId = g.id;
            // Подвижность группы распространяется на нового участника.
            e.Movable = g.movable;
            RaiseChanged();
        }

        public void RemoveFrom(KitchenElement e)
        {
            if (e == null || e.GroupId == 0) return;
            e.GroupId = 0;
            RaiseChanged();
        }

        public void MoveTo(KitchenElement e, LinkGroup? g)
        {
            if (e == null) return;
            if (g == null) { RemoveFrom(e); return; }
            AddTo(g, e);
        }

        public void Rename(LinkGroup g, string name)
        {
            if (g == null || string.IsNullOrEmpty(name) || g.name == name) return;
            g.name = name;
            RaiseChanged();
        }

        public void SetMovable(LinkGroup g, bool movable)
        {
            if (g == null) return;
            g.movable = movable;
            foreach (var m in MembersOf(g))
                m.Movable = movable;
            RaiseChanged();
        }

        public LinkGroup Register(int id, string name, bool movable)
        {
            var g = new LinkGroup { id = id, name = name, movable = movable };
            _groups[id] = g;
            if (id >= _nextId) _nextId = id + 1;
            RaiseChanged();
            return g;
        }

        public void Clear()
        {
            ModuleEditMode.Exit(); // сцена перезагружается — режим не переживает загрузку
            _groups.Clear();
            _nextId = 1;
            RaiseChanged();
        }
    }
}
