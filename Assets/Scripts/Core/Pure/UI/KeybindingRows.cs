using System;
using System.Collections.Generic;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public readonly struct KeybindingRow
    {
        public readonly bool IsGroupHeader;
        public readonly InputActionGroup Group;
        public readonly InputAction Action;

        private KeybindingRow(bool isGroupHeader, InputActionGroup group, InputAction action)
        {
            IsGroupHeader = isGroupHeader;
            Group = group;
            Action = action;
        }

        public static KeybindingRow Header(InputActionGroup group) =>
            new KeybindingRow(true, group, default);

        public static KeybindingRow ForAction(InputAction action) =>
            new KeybindingRow(false, InputActionCatalog.GroupOf(action), action);
    }

    public static class KeybindingRowList
    {
        public static readonly InputActionGroup[] GroupOrder =
            (InputActionGroup[])Enum.GetValues(typeof(InputActionGroup));

        public static IReadOnlyList<KeybindingRow> Build()
        {
            var rows = new List<KeybindingRow>();
            foreach (var group in GroupOrder)
            {
                rows.Add(KeybindingRow.Header(group));
                foreach (var action in InputActionCatalog.All)
                    if (InputActionCatalog.GroupOf(action) == group)
                        rows.Add(KeybindingRow.ForAction(action));
            }
            return rows;
        }
    }
}
