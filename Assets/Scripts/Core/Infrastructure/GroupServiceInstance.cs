using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public class GroupServiceInstance : IGroupService
    {
        private readonly Dictionary<int, LinkGroup> _groups = new Dictionary<int, LinkGroup>();
        private int _nextId = 1;

        private readonly Dictionary<int, List<KitchenElement>> _membersByGroupId =
            new Dictionary<int, List<KitchenElement>>();

        private int _indexedAtMembership = -1;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static int _indexRebuilds;

        public static int TakeIndexRebuilds()
        {
            int n = _indexRebuilds;
            _indexRebuilds = 0;
            return n;
        }
#endif

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
            if (ModuleEditMode.Active == g) ModuleEditMode.Exit();
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
            if (g == null) return new List<KitchenElement>();

            using var _ = PerfMarkers.GroupMembersOf.Auto();

            RebuildIndexIfMembershipMoved();
            return _membersByGroupId.TryGetValue(g.id, out var members)
                ? new List<KitchenElement>(members)
                : new List<KitchenElement>();
        }

        private void RebuildIndexIfMembershipMoved()
        {
            if (_indexedAtMembership == GroupMembershipRevision.Version) return;
            _indexedAtMembership = GroupMembershipRevision.Version;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _indexRebuilds++;
#endif
            _membersByGroupId.Clear();
            foreach (var e in PartRegistry.GetAll())
            {
                if (e == null || e.GroupId == 0) continue;
                if (!_membersByGroupId.TryGetValue(e.GroupId, out var bucket))
                    _membersByGroupId[e.GroupId] = bucket = new List<KitchenElement>();
                bucket.Add(e);
            }
        }

        public IEnumerable<LinkGroup> AllGroups() => _groups.Values;

        public void AddTo(LinkGroup g, KitchenElement e)
        {
            if (g == null || e == null || !_groups.ContainsKey(g.id)) return;
            if (e.GroupId == g.id) return;
            e.GroupId = g.id;
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

        public LinkGroup Register(int id, string name, bool movable, string widthAxis = "x")
        {
            var g = new LinkGroup { id = id, name = name, movable = movable, widthAxis = widthAxis };
            _groups[id] = g;
            if (id >= _nextId) _nextId = id + 1;
            RaiseChanged();
            return g;
        }

        public void Clear()
        {
            ModuleEditMode.Exit();
            _groups.Clear();
            _nextId = 1;
            RaiseChanged();
        }
    }
}
