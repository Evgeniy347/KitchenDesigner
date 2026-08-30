using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public interface IGroupService
    {
        event Action? Changed;

        LinkGroup Create(string name);

        LinkGroup? Link(IList<KitchenElement> members);

        void Unlink(LinkGroup g);

        LinkGroup? GroupOf(KitchenElement e);
        List<KitchenElement> MembersOf(LinkGroup g);
        IEnumerable<LinkGroup> AllGroups();

        void AddTo(LinkGroup g, KitchenElement e);

        void RemoveFrom(KitchenElement e);

        void MoveTo(KitchenElement e, LinkGroup? g);

        void Rename(LinkGroup g, string name);

        void SetMovable(LinkGroup g, bool movable);

        LinkGroup Register(int id, string name, bool movable, string widthAxis = "x");

        void Clear();
    }
}
