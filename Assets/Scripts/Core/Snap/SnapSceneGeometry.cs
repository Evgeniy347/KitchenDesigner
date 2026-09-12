using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class SnapSceneGeometry
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static int GeometryBuildsInLastPass { get; private set; }

        public static int ElementsInLastPass { get; private set; }
#endif

        public static List<ElementGeometry> For(IReadOnlyList<KitchenElement> elements,
            KitchenElement? seatedElement)
        {
            var result = new List<ElementGeometry>(elements != null ? elements.Count : 0);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GeometryBuildsInLastPass = 0;
            ElementsInLastPass = 0;
#endif
            if (elements == null) return result;

            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                if (e == null || !e.gameObject.activeInHierarchy) continue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                ElementsInLastPass++;
#endif
                if (ElementSnapshotReuse.TryReuseGeometry(e, out var kept))
                {
                    result.Add(kept);
                    continue;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                GeometryBuildsInLastPass++;
#endif
                var geometry = e.ToGeometry();
                if (seatedElement != null && !PipeDocking.MaySeatOn(seatedElement, e))
                    geometry = geometry.WithoutPorts();
                result.Add(geometry);
            }

            return result;
        }
    }
}
