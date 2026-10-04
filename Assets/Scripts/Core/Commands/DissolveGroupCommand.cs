using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public class DissolveGroupCommand : IUndoCommand
    {
        private readonly LinkGroup _group;
        private readonly List<KitchenElement> _members;

        public DissolveGroupCommand(LinkGroup group)
        {
            _group = group;
            _members = GroupManager.MembersOf(group);
        }

        public string Description => $"Dissolve group {_group.name}";

        public void Execute() => GroupManager.Unlink(_group);

        public void Undo()
        {
            var restored = GroupManager.Register(_group.id, _group.name, _group.movable, _group.widthAxis);
            foreach (var member in _members)
                if (member != null) GroupManager.AddTo(restored, member);
        }
    }
}
