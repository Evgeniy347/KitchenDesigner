using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class DemoModeGuard
    {
        internal static Action? Prompt;

        internal static bool BlocksAndRollsBack(IUndoCommand? command)
        {
            if (DemoMode.Current.MutationsAllowed) return false;
            RollBack(command);
            (Prompt ?? UI.DemoModeDialogUI.ShowIfAvailable).Invoke();
            return true;
        }

        private static void RollBack(IUndoCommand? command)
        {
            if (command == null) return;
            try
            {
                command.Undo();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Demo] Откат отклонённой правки не удался: {ex.Message}");
            }
        }
    }
}
