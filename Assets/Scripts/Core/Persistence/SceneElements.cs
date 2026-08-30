using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class SceneElements
    {
        public static IEnumerable<KitchenElement> All() => PartRegistry.Instance.GetAll();

        public static void ClearKeepingBasePlate(IEnumerable<KitchenElement> elements)
        {
            foreach (var e in elements)
            {
                if (e == null) continue;
                if (e.GetComponent<BasePlate>() != null) continue;
                Destroy(e.gameObject);
            }
        }

        private static void Destroy(GameObject go)
        {
            if (go == null) return;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }
    }
}
