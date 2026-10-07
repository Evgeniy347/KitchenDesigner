using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class SceneElements
    {
        public static IEnumerable<KitchenElement> All() => PartRegistry.Instance.GetAll();

        public static void Clear(IEnumerable<KitchenElement> elements)
        {
            var doomed = new List<KitchenElement>();
            foreach (var e in elements)
                if (e != null) doomed.Add(e);

            if (CoversTheWholeRegistry(doomed)) PartRegistry.Clear();
            else foreach (var e in doomed) PartRegistry.Unregister(e);

            foreach (var e in doomed) Destroy(e.gameObject);
        }

        private static bool CoversTheWholeRegistry(List<KitchenElement> doomed)
        {
            var registry = PartRegistry.All;
            if (doomed.Count < registry.Count) return false;

            var listed = new HashSet<KitchenElement>(doomed);
            for (int i = 0; i < registry.Count; i++)
                if (registry[i] != null && !listed.Contains(registry[i])) return false;
            return true;
        }

        private static void Destroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }
    }
}
