using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class GroupDeletion
    {
        public static string DeleteDescription(LinkGroup group) => $"Delete group {group.name}";

        public static void Dissolve(LinkGroup group) => CommandStack.Execute(new DissolveGroupCommand(group));

        public static void DeleteWithContents(LinkGroup group)
        {
            var members = GroupManager.MembersOf(group);
            var selection = SelectionManager.Instance;
            if (selection != null) selection.DeselectAll();

            CommandStack.BeginCapture();
            try
            {
                CommandStack.Execute(new DissolveGroupCommand(group));
                var deleted = new HashSet<GameObject>();
                foreach (var member in members)
                    DeleteMember(member, deleted);
            }
            finally
            {
                CommandStack.EndCapture(DeleteDescription(group), commit: true);
            }
        }

        private static void DeleteMember(KitchenElement member, HashSet<GameObject> deleted)
        {
            if (member == null || !deleted.Add(member.gameObject)) return;
            var upper = DrawerLinks.DetachPairedUpper(member);
            if (upper != null && deleted.Add(upper)) CommandStack.Execute(new DeleteCommand(upper));
            CommandStack.Execute(new DeleteCommand(member.gameObject));
        }
    }
}
