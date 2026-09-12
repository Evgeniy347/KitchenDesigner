using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class ElementCreation
    {
        public static void Commit(GameObject go)
        {
            var element = go != null ? go.GetComponent<KitchenElement>() : null;
            if (element == null) return;

            MmGrid.Snap(element);
            CommandStack.Execute(new CreateCommand(go));
            SelectionManager.Instance?.Select(element);
        }
    }
}
