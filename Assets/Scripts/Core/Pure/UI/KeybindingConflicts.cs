using System.Collections.Generic;
using System.Text;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public static class KeybindingConflicts
    {
        public static bool IsInConflict(IReadOnlyList<KeyBindingConflict> conflicts, InputBinding binding)
        {
            if (binding.IsEmpty) return false;
            for (int i = 0; i < conflicts.Count; i++)
                if (conflicts[i].Binding == binding) return true;
            return false;
        }

        public static string DescribeOthers(
            IReadOnlyList<KeyBindingConflict> conflicts, InputAction action, InputBinding binding)
        {
            if (binding.IsEmpty) return string.Empty;

            for (int i = 0; i < conflicts.Count; i++)
            {
                if (conflicts[i].Binding != binding) continue;

                var others = conflicts[i].Actions;
                var sb = new StringBuilder();
                for (int j = 0; j < others.Count; j++)
                {
                    if (others[j] == action) continue;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(InputActionCatalog.DisplayNameOf(others[j]));
                }
                return sb.ToString();
            }
            return string.Empty;
        }
    }
}
