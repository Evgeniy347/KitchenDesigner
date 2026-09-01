using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class OpeningCollision
    {
        internal const int ScanSteps = 128;

        internal const float TouchGapMm = 5f;

        public static float FindMaxProgress(
            KitchenElement self,
            System.Func<float, (Vector3 min, Vector3 max)> getBounds,
            List<KitchenElement>? exclude = null,
            float precision = 0.001f)
        {
            var others = new List<KitchenElement>();
            foreach (var el in PartRegistry.All)
            {
                if (el == null || el == self) continue;
                if (exclude != null && exclude.Contains(el)) continue;
                others.Add(el);
            }
            if (others.Count == 0) return 1f;

            var closedBounds = getBounds(0f);
            var relevant = new List<(Vector3 min, Vector3 max)>();
            foreach (var el in others)
            {
                var aabb = MinMax(el.GetVertices());
                if (!TouchesWhenClosed(closedBounds, aabb))
                    relevant.Add(aabb);
            }
            if (relevant.Count == 0) return 1f;

            float prevFree = 0f, hit = -1f;
            for (int i = 1; i <= ScanSteps; i++)
            {
                float t = i / (float)ScanSteps;
                if (Overlaps(getBounds(t), relevant)) { hit = t; break; }
                prevFree = t;
            }
            if (hit < 0f) return 1f;

            float lo = prevFree, hi = hit;
            int steps = Mathf.CeilToInt(Mathf.Log((hi - lo) / precision, 2f));
            for (int iter = 0; iter < steps; iter++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Overlaps(getBounds(mid), relevant))
                    hi = mid;
                else
                    lo = mid;
            }
            return lo;
        }

        private static bool TouchesWhenClosed((Vector3 min, Vector3 max) a, (Vector3 min, Vector3 max) b)
        {
            float contactU = TouchGapMm * AppConstants.MM_TO_UNITS;
            return IntervalsTouchOrOverlap(a.min.x, a.max.x, b.min.x, b.max.x, contactU) &&
                   IntervalsTouchOrOverlap(a.min.y, a.max.y, b.min.y, b.max.y, contactU) &&
                   IntervalsTouchOrOverlap(a.min.z, a.max.z, b.min.z, b.max.z, contactU);
        }

        private static bool IntervalsTouchOrOverlap(float min1, float max1, float min2, float max2,
            float contactU)
        {
            float gap = Mathf.Max(min1 - max2, min2 - max1);
            return gap <= contactU;
        }

        private static bool Overlaps((Vector3 min, Vector3 max) a, List<(Vector3 min, Vector3 max)> others)
        {
            foreach (var (min, max) in others)
            {
                if (Tolerance.IntervalsOverlap(a.min.x, a.max.x, min.x, max.x) &&
                    Tolerance.IntervalsOverlap(a.min.y, a.max.y, min.y, max.y) &&
                    Tolerance.IntervalsOverlap(a.min.z, a.max.z, min.z, max.z))
                    return true;
            }
            return false;
        }

        public static (Vector3 min, Vector3 max) MinMax(Vector3[] verts)
        {
            float minX = verts[0].x, maxX = verts[0].x;
            float minY = verts[0].y, maxY = verts[0].y;
            float minZ = verts[0].z, maxZ = verts[0].z;
            for (int i = 1; i < verts.Length; i++)
            {
                if (verts[i].x < minX) minX = verts[i].x; else if (verts[i].x > maxX) maxX = verts[i].x;
                if (verts[i].y < minY) minY = verts[i].y; else if (verts[i].y > maxY) maxY = verts[i].y;
                if (verts[i].z < minZ) minZ = verts[i].z; else if (verts[i].z > maxZ) maxZ = verts[i].z;
            }
            return (new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
        }
    }
}
