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

            // Фильтр: исключаем элементы, с которыми уже есть пересечение
            // в закрытом состоянии (контейнеры — корпуса, стены). Иначе
            // ящик не откроется из корпуса, а окно/дверь — из стены.
            var closedBounds = getBounds(0f);
            var relevant = new List<(Vector3 min, Vector3 max)>();
            foreach (var el in others)
            {
                var aabb = MinMax(el.GetVertices());
                if (!OverlapsPair(closedBounds, aabb))
                    relevant.Add(aabb);
            }
            if (relevant.Count == 0) return 1f;

            // Пересечение немонотонно по прогрессу: тонкое препятствие можно
            // «пролететь насквозь» (конечная поза уже за ним), а поворотный фасад
            // на промежуточных углах выступает дальше крайних поз. Поэтому ищем
            // ПЕРВОЕ пересечение сканированием пути, затем уточняем двоичным
            // поиском на последнем свободном интервале.
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

        /// <summary>Шаг сканирования пути 1/128: для ящика с ходом 0,5 м это ~4 мм —
        /// мельче самого тонкого препятствия (панель 16–18 мм), проскок исключён.</summary>
        private const int ScanSteps = 128;

        private static bool OverlapsPair((Vector3 min, Vector3 max) a, (Vector3 min, Vector3 max) b)
        {
            return Tolerance.IntervalsOverlap(a.min.x, a.max.x, b.min.x, b.max.x) &&
                   Tolerance.IntervalsOverlap(a.min.y, a.max.y, b.min.y, b.max.y) &&
                   Tolerance.IntervalsOverlap(a.min.z, a.max.z, b.min.z, b.max.z);
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
