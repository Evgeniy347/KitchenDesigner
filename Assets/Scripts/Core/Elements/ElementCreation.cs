using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class ElementCreation
    {
        public static void Commit(GameObject go)
        {
            if (IsGone(go)) return;

            var element = go.GetComponent<KitchenElement>();
            if (element == null) return;

            MmGrid.Snap(element);
            CommandStack.Execute(new CreateCommand(go));
            SelectionManager.Instance?.Select(element);
        }

        private static bool IsGone(GameObject go) => go == null;
    }
}
