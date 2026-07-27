using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public class SetGroupCommand : IUndoCommand
    {
        private LinkGroup _target;
        private readonly bool _created;
        private readonly string _name, _axis, _beforeAxis;
        private readonly List<KitchenElement> _members;
        private readonly Dictionary<KitchenElement, int> _before = new Dictionary<KitchenElement, int>();
        public string Description => $"Set group {_name}";

        public SetGroupCommand(LinkGroup target, bool created, string name, string axis,
            List<KitchenElement> members)
        {
            _target = target; _created = created; _name = name; _axis = axis;
            _beforeAxis = target.widthAxis; _members = new List<KitchenElement>(members);
            foreach (var e in GroupManager.MembersOf(target)) _before[e] = e.GroupId;
            foreach (var e in members) if (!_before.ContainsKey(e)) _before[e] = e.GroupId;
        }

        public void Execute()
        {
            if (!IsRegistered(_target))
                _target = GroupManager.Register(_target.id, _name, true, _axis);
            foreach (var e in GroupManager.MembersOf(_target))
                if (!_members.Contains(e)) GroupManager.RemoveFrom(e);
            foreach (var e in _members) GroupManager.MoveTo(e, _target);
            GroupManager.Rename(_target, _name);
            _target.widthAxis = _axis;
        }

        public void Undo()
        {
            foreach (var e in new List<KitchenElement>(GroupManager.MembersOf(_target)))
                GroupManager.RemoveFrom(e);
            if (_created) GroupManager.Unlink(_target);
            else _target.widthAxis = _beforeAxis;
            foreach (var pair in _before)
            {
                var old = FindGroup(pair.Value);
                GroupManager.MoveTo(pair.Key, old);
            }
        }

        private static bool IsRegistered(LinkGroup group)
        {
            foreach (var g in GroupManager.AllGroups()) if (g.id == group.id) return true;
            return false;
        }

        private static LinkGroup? FindGroup(int id)
        {
            if (id == 0) return null;
            foreach (var g in GroupManager.AllGroups()) if (g.id == id) return g;
            return null;
        }
    }
}
