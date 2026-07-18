using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class OpeningCollision
    {
        /// <summary>
        /// Двоичным поиском находит максимальный прогресс открывания [0..1],
        /// при котором элемент не пересекается ни с одним другим объектом сцены.
        /// </summary>
        /// <param name="self">Открываемый элемент (исключается из проверки)</param>
        /// <param name="getBounds">Функция, возвращающая мировые границы (min, max) элемента при заданном прогрессе</param>
        /// <param name="exclude">Дополнительные элементы для исключения (например, прикреплённый ящик/фасад)</param>
        /// <param name="precision">Точность поиска (дробление ≤ precision)</param>
        public static float FindMaxProgress(
            KitchenElement self,
            System.Func<float, (Vector3 min, Vector3 max)> getBounds,
            List<KitchenElement>? exclude = null,
            float precision = 0.001f)
        {
            var others = new List<KitchenElement>();
            foreach (var el in PartRegistry.GetAll())
            {
                if (el == null || el == self) continue;
                if (exclude != null && exclude.Contains(el)) continue;
                others.Add(el);
            }
            if (others.Count == 0) return 1f;

            var otherAabbs = new List<(Vector3 min, Vector3 max)>();
            foreach (var el in others)
            {
                var verts = el.GetVertices();
                otherAabbs.Add(MinMax(verts));
            }

            var fullOpen = getBounds(1f);
            if (!Overlaps(fullOpen, otherAabbs))
                return 1f;

            float lo = 0f, hi = 1f;
            int steps = Mathf.CeilToInt(Mathf.Log(1f / precision, 2f));
            for (int iter = 0; iter < steps; iter++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Overlaps(getBounds(mid), otherAabbs))
                    hi = mid;
                else
                    lo = mid;
            }
            return lo;
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
