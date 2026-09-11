using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class GroupBounds
    {
        public static bool Of(IReadOnlyList<Vector3[]>? vertexSets,
            out Vector3 center, out Vector3 size)
        {
            center = Vector3.zero;
            size = Vector3.zero;
            if (vertexSets == null) return false;

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool any = false;

            for (int s = 0; s < vertexSets.Count; s++)
            {
                var set = vertexSets[s];
                if (set == null) continue;
                for (int i = 0; i < set.Length; i++)
                {
                    min = Vector3.Min(min, set[i]);
                    max = Vector3.Max(max, set[i]);
                    any = true;
                }
            }

            if (!any) return false;
            center = (min + max) * 0.5f;
            size = max - min;
            return true;
        }

        public static Face[] FacesOf(Vector3 center, Vector3 size) =>
            GappedBox.Faces(size, BoxGaps.None, center, Quaternion.identity);
    }
}
