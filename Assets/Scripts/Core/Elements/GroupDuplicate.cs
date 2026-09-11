using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class GroupDuplicate
    {
        public const string GroupDescription = "Duplicate group";

        public static List<KitchenElement> Of(IReadOnlyList<KitchenElement>? sources,
            Vector3 offset, out IUndoCommand? command)
        {
            command = null;
            var copies = new List<KitchenElement>();
            if (sources == null) return copies;

            var created = new List<IUndoCommand>();
            foreach (var source in sources)
            {
                if (source == null || !ModuleEditMode.IsEditable(source)) continue;

                var duplicated = ElementFactory.Duplicate(source);
                var copy = duplicated != null ? duplicated.GetComponent<KitchenElement>() : null;
                if (copy == null) continue;

                copy.transform.position = source.transform.position + offset;
                if (ModuleEditMode.IsActive) copy.GroupId = ModuleEditMode.Active!.id;
                created.Add(new CreateCommand(duplicated!));
                copies.Add(copy);
            }

            if (created.Count == 0) return copies;
            command = created.Count == 1
                ? created[0]
                : new CompositeCommand(GroupDescription, created);
            return copies;
        }
    }
}
