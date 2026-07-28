using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class WallMeshBuilder
    {
        /// <summary>Минимальный размер ячейки в нормализованных координатах
        /// (единичный куб). Разделители ближе этого порога схлопываются в одну
        /// границу (см. CollapseNearDuplicates): иначе между ними остаётся
        /// ячейка-волосок, которую отбрасывали целиком — вместе с гранями и
        /// откосом, — оставляя сквозную щель со светом у краёв проёма.</summary>
        public const float MinCellNorm = 0.001f;

        public struct WindowCutout
        {
            public Vector2 centerNorm;
            public Vector2 halfSizeNorm;
        }

        [System.Serializable]
        public struct EndShape
        {
            public float startFront, startBack, endFront, endBack;
            public static EndShape Square => new EndShape
            { startFront = -0.5f, startBack = -0.5f, endFront = 0.5f, endBack = 0.5f };
        }

        /// <summary>Единичный куб стены с сквозными вырезами под окна.
        /// Вырезы задаются в плоскости «ширина × высота»; по умолчанию ширина —
        /// локальная X (толщина по Z). Для стен, повёрнутых длиной вдоль Z,
        /// передайте thicknessAlongX = true — оси X и Z меняются местами.</summary>
        public static Mesh Build(List<WindowCutout> cutouts, bool thicknessAlongX = false,
            EndShape? endShape = null)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            var shape = endShape ?? EndShape.Square;
            AddMiteredBoxNorm(verts, tris, cutouts, shape);

            if (thicknessAlongX)
            {
                // Свап X↔Z зеркалит меш — разворачиваем обход треугольников,
                // чтобы нормали остались наружными.
                for (int i = 0; i < verts.Count; i++)
                    verts[i] = new Vector3(verts[i].z, verts[i].y, verts[i].x);
                for (int t = 0; t < tris.Count; t += 3)
                    (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]);
            }

            var mesh = new Mesh { name = "Wall" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddMiteredBoxNorm(List<Vector3> verts, List<int> tris,
            List<WindowCutout> cutouts, EndShape s)
        {
            const float y0 = -0.5f, y1 = 0.5f, front = 0.5f, back = -0.5f;
            AddFaceWithCutoutsNorm(verts, tris, s.startFront, s.endFront, y0, y1,
                front, back, true, cutouts);
            AddFaceWithCutoutsNorm(verts, tris, s.startBack, s.endBack, y0, y1,
                back, front, false, cutouts);
            AddQuad(verts, tris, V(s.endFront, y0, front), V(s.endBack, y0, back),
                V(s.endBack, y1, back), V(s.endFront, y1, front));
            AddQuad(verts, tris, V(s.startBack, y0, back), V(s.startFront, y0, front),
                V(s.startFront, y1, front), V(s.startBack, y1, back));
            AddQuad(verts, tris, V(s.startFront, y1, front), V(s.endFront, y1, front),
                V(s.endBack, y1, back), V(s.startBack, y1, back));
            AddQuad(verts, tris, V(s.startBack, y0, back), V(s.endBack, y0, back),
                V(s.endFront, y0, front), V(s.startFront, y0, front));
        }

        private static void AddBoxNorm(List<Vector3> verts, List<int> tris,
            Vector3 center, Vector3 half, List<WindowCutout> cutouts)
        {
            float x0 = center.x - half.x;
            float x1 = center.x + half.x;
            float y0 = center.y - half.y;
            float y1 = center.y + half.y;
            float z0 = center.z - half.z;
            float z1 = center.z + half.z;

            if (cutouts.Count == 0)
            {
                AddQuad(verts, tris, V(x0, y0, z1), V(x1, y0, z1), V(x1, y1, z1), V(x0, y1, z1));
                AddQuad(verts, tris, V(x1, y0, z0), V(x0, y0, z0), V(x0, y1, z0), V(x1, y1, z0));
                AddQuad(verts, tris, V(x1, y0, z1), V(x1, y0, z0), V(x1, y1, z0), V(x1, y1, z1));
                AddQuad(verts, tris, V(x0, y0, z0), V(x0, y0, z1), V(x0, y1, z1), V(x0, y1, z0));
                AddQuad(verts, tris, V(x0, y1, z1), V(x1, y1, z1), V(x1, y1, z0), V(x0, y1, z0));
                AddQuad(verts, tris, V(x0, y0, z0), V(x1, y0, z0), V(x1, y0, z1), V(x0, y0, z1));
                return;
            }

            AddFaceWithCutoutsNorm(verts, tris, x0, x1, y0, y1, z1, z0, true, cutouts);
            // Диапазон X обязан быть возрастающим (fx0 < fx1), иначе сплиты
            // вырезов не попадают в грань и дыра растягивается на всю ширину;
            // обратный обход задней грани обеспечивает флаг frontFace = false.
            AddFaceWithCutoutsNorm(verts, tris, x0, x1, y0, y1, z0, z1, false, cutouts);

            AddQuad(verts, tris, V(x1, y0, z1), V(x1, y0, z0), V(x1, y1, z0), V(x1, y1, z1));
            AddQuad(verts, tris, V(x0, y0, z0), V(x0, y0, z1), V(x0, y1, z1), V(x0, y1, z0));
            AddQuad(verts, tris, V(x0, y1, z1), V(x1, y1, z1), V(x1, y1, z0), V(x0, y1, z0));
            AddQuad(verts, tris, V(x0, y0, z0), V(x1, y0, z0), V(x1, y0, z1), V(x0, y0, z1));
        }

        private static void AddFaceWithCutoutsNorm(List<Vector3> verts, List<int> tris,
            float fx0, float fx1, float fy0, float fy1, float zFront, float zBack,
            bool frontFace, List<WindowCutout> cutouts)
        {
            var xSplits = new List<float> { fx0, fx1 };
            var ySplits = new List<float> { fy0, fy1 };

            foreach (var co in cutouts)
            {
                float wl = co.centerNorm.x - co.halfSizeNorm.x;
                float wr = co.centerNorm.x + co.halfSizeNorm.x;
                float wb = co.centerNorm.y - co.halfSizeNorm.y;
                float wt = co.centerNorm.y + co.halfSizeNorm.y;
                if (wl > fx0 && wl < fx1) xSplits.Add(wl);
                if (wr > fx0 && wr < fx1) xSplits.Add(wr);
                if (wb > fy0 && wb < fy1) ySplits.Add(wb);
                if (wt > fy0 && wt < fy1) ySplits.Add(wt);
            }

            xSplits.Sort();
            ySplits.Sort();

            // Сливаем почти совпадающие края окон в одну границу до нарезки
            // ячеек. Без этого пара окон «на одной высоте» (Y отличается на
            // доли мм) даёт ячейку тоньше порога, которую отбрасывали вместе
            // с откосом — сквозная полоса света у верха и низа проёма.
            CollapseNearDuplicates(xSplits, MinCellNorm);
            CollapseNearDuplicates(ySplits, MinCellNorm);

            for (int i = 0; i < xSplits.Count - 1; i++)
            {
                float xs = xSplits[i], xe = xSplits[i + 1];
                if (xe - xs < MinCellNorm) continue;
                for (int j = 0; j < ySplits.Count - 1; j++)
                {
                    float ys = ySplits[j], ye = ySplits[j + 1];
                    if (ye - ys < MinCellNorm) continue;

                    float cx = (xs + xe) * 0.5f, cy = (ys + ye) * 0.5f;
                    int insideCount = CountInside(cutouts, cx, cy);
                    if (insideCount > 1) continue; // overlap — без геометрии

                    if (insideCount == 0)
                    {
                        if (frontFace)
                            AddQuad(verts, tris, V(xs, ys, zFront), V(xe, ys, zFront), V(xe, ye, zFront), V(xs, ye, zFront));
                        else
                            AddQuad(verts, tris, V(xe, ys, zFront), V(xs, ys, zFront), V(xs, ye, zFront), V(xe, ye, zFront));
                    }
                    else if (frontFace)
                    {
                        // Reveal-квады строим только на внешних рёбрах проёма.
                        // Проверяем центр соседней ячейки по индексу. Если сосед — стена
                        // (insideCount == 0) или за пределами сетки, ребро внешнее.
                        int nxBot = j > 0 ? CountInside(cutouts, cx, (ySplits[j - 1] + ys) * 0.5f) : 0;
                        int nxTop = j < ySplits.Count - 2 ? CountInside(cutouts, cx, (ye + ySplits[j + 2]) * 0.5f) : 0;
                        int nxLft = i > 0 ? CountInside(cutouts, (xSplits[i - 1] + xs) * 0.5f, cy) : 0;
                        int nxRgt = i < xSplits.Count - 2 ? CountInside(cutouts, (xe + xSplits[i + 2]) * 0.5f, cy) : 0;

                        // Bottom ребро (y = ys)
                        if (nxBot == 0)
                            AddQuad(verts, tris, V(xs, ys, zBack), V(xe, ys, zBack), V(xe, ys, zFront), V(xs, ys, zFront));
                        // Top ребро (y = ye)
                        if (nxTop == 0)
                            AddQuad(verts, tris, V(xs, ye, zFront), V(xe, ye, zFront), V(xe, ye, zBack), V(xs, ye, zBack));
                        // Left ребро (x = xs)
                        if (nxLft == 0)
                            AddQuad(verts, tris, V(xs, ys, zFront), V(xs, ys, zBack), V(xs, ye, zBack), V(xs, ye, zFront));
                        // Right ребро (x = xe)
                        if (nxRgt == 0)
                            AddQuad(verts, tris, V(xe, ys, zBack), V(xe, ys, zFront), V(xe, ye, zFront), V(xe, ye, zBack));
                    }
                }
            }
        }

        /// <summary>Схлопывает отсортированные разделители, отстоящие менее чем
        /// на minGap, в одну границу. Крайние значения (границы самой грани)
        /// сохраняются всегда, поэтому на выходе не меньше двух точек и ни один
        /// промежуток не тоньше порога — ячеек-волосков не возникает.
        ///
        /// internal и с порогом-параметром, потому что тем же приёмом режет свою
        /// плоскость <see cref="PlaneWithHolesMesh"/> — только там координаты в
        /// миллиметрах, а не нормализованные.</summary>
        internal static void CollapseNearDuplicates(List<float> splits, float minGap)
        {
            float hi = splits[splits.Count - 1];
            int w = 1; // splits[0] (ближний край грани) всегда остаётся
            for (int r = 1; r < splits.Count; r++)
            {
                // Не поглощаем дальний край и не оставляем промежуток тоньше порога.
                if (splits[r] - splits[w - 1] >= minGap && hi - splits[r] >= minGap)
                    splits[w++] = splits[r];
            }
            splits[w++] = hi; // дальний край грани обязан уцелеть
            splits.RemoveRange(w, splits.Count - w);
        }

        private static int CountInside(List<WindowCutout> cutouts, float px, float py)
        {
            int count = 0;
            foreach (var co in cutouts)
            {
                if (px > co.centerNorm.x - co.halfSizeNorm.x &&
                    px < co.centerNorm.x + co.halfSizeNorm.x &&
                    py > co.centerNorm.y - co.halfSizeNorm.y &&
                    py < co.centerNorm.y + co.halfSizeNorm.y)
                {
                    count++;
                    if (count > 1) break; // достаточно — overlap, дальше не интересно
                }
            }
            return count;
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private static void AddQuad(List<Vector3> verts, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }
    }
}
