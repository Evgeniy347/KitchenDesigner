using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class WallMeshBuilder
    {
        /// <summary>Минимальный размер ячейки в нормализованных координатах
        /// (единичный куб). Ячейки тоньше этого порога порождают вырожденные
        /// треугольники — визуальную полосу между близкими окнами.</summary>
        public const float MinCellNorm = 0.001f;

        public struct WindowCutout
        {
            public Vector2 centerNorm;
            public Vector2 halfSizeNorm;
        }

        /// <summary>Единичный куб стены с сквозными вырезами под окна.
        /// Вырезы задаются в плоскости «ширина × высота»; по умолчанию ширина —
        /// локальная X (толщина по Z). Для стен, повёрнутых длиной вдоль Z,
        /// передайте thicknessAlongX = true — оси X и Z меняются местами.</summary>
        public static Mesh Build(List<WindowCutout> cutouts, bool thicknessAlongX = false)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            AddBoxNorm(verts, tris, Vector3.zero, Vector3.one * 0.5f, cutouts);

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

            for (int i = 0; i < xSplits.Count - 1; i++)
            {
                float xs = xSplits[i], xe = xSplits[i + 1];
                if (xe - xs < MinCellNorm) continue;
                for (int j = 0; j < ySplits.Count - 1; j++)
                {
                    float ys = ySplits[j], ye = ySplits[j + 1];
                    if (ye - ys < MinCellNorm) continue;

                    float cx = (xs + xe) * 0.5f, cy = (ys + ye) * 0.5f;
                    int insideCount = 0;
                    foreach (var co in cutouts)
                    {
                        if (cx > co.centerNorm.x - co.halfSizeNorm.x &&
                            cx < co.centerNorm.x + co.halfSizeNorm.x &&
                            cy > co.centerNorm.y - co.halfSizeNorm.y &&
                            cy < co.centerNorm.y + co.halfSizeNorm.y)
                        { insideCount++; }
                    }

                    if (insideCount == 0)
                    {
                        if (frontFace)
                            AddQuad(verts, tris, V(xs, ys, zFront), V(xe, ys, zFront), V(xe, ye, zFront), V(xs, ye, zFront));
                        else
                            AddQuad(verts, tris, V(xe, ys, zFront), V(xs, ys, zFront), V(xs, ye, zFront), V(xe, ye, zFront));
                    }
                    else if (frontFace && insideCount == 1)
                    {
                        // Reveal-квады (внутренние стенки проёма) строим только для ячеек,
                        // которые внутри ровно одного окна. В overlap-регионе (insideCount > 1)
                        // reveal-квады оказываются внутри объединённого проёма и видны как
                        // артефактная полоса — поэтому пропускаем их.
                        AddQuad(verts, tris, V(xs, ys, zBack), V(xe, ys, zBack), V(xe, ys, zFront), V(xs, ys, zFront));
                        AddQuad(verts, tris, V(xs, ye, zFront), V(xe, ye, zFront), V(xe, ye, zBack), V(xs, ye, zBack));
                        AddQuad(verts, tris, V(xs, ys, zFront), V(xs, ys, zBack), V(xs, ye, zBack), V(xs, ye, zFront));
                        AddQuad(verts, tris, V(xe, ys, zBack), V(xe, ys, zFront), V(xe, ye, zFront), V(xe, ye, zBack));
                    }
                }
            }
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
