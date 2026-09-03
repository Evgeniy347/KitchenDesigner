using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class WallMeshBuilder
    {
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

        public static Mesh Build(List<WindowCutout> cutouts, bool thicknessAlongX = false,
            EndShape? endShape = null)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            var shape = endShape ?? EndShape.Square;
            AddMiteredBoxNorm(verts, tris, cutouts, shape);

            if (thicknessAlongX)
            {
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
                    if (insideCount > 1) continue;

                    if (insideCount == 0)
                    {
                        if (frontFace)
                            AddQuad(verts, tris, V(xs, ys, zFront), V(xe, ys, zFront), V(xe, ye, zFront), V(xs, ye, zFront));
                        else
                            AddQuad(verts, tris, V(xe, ys, zFront), V(xs, ys, zFront), V(xs, ye, zFront), V(xe, ye, zFront));
                    }
                    else if (frontFace)
                    {
                        bool wallBelow = j > 0 && CountInside(cutouts, cx, (ySplits[j - 1] + ys) * 0.5f) == 0;
                        bool wallAbove = j < ySplits.Count - 2 && CountInside(cutouts, cx, (ye + ySplits[j + 2]) * 0.5f) == 0;
                        bool wallLeft = i > 0 && CountInside(cutouts, (xSplits[i - 1] + xs) * 0.5f, cy) == 0;
                        bool wallRight = i < xSplits.Count - 2 && CountInside(cutouts, (xe + xSplits[i + 2]) * 0.5f, cy) == 0;

                        if (wallBelow)
                            AddQuad(verts, tris, V(xs, ys, zBack), V(xe, ys, zBack), V(xe, ys, zFront), V(xs, ys, zFront));
                        if (wallAbove)
                            AddQuad(verts, tris, V(xs, ye, zFront), V(xe, ye, zFront), V(xe, ye, zBack), V(xs, ye, zBack));
                        if (wallLeft)
                            AddQuad(verts, tris, V(xs, ys, zFront), V(xs, ys, zBack), V(xs, ye, zBack), V(xs, ye, zFront));
                        if (wallRight)
                            AddQuad(verts, tris, V(xe, ys, zBack), V(xe, ys, zFront), V(xe, ye, zFront), V(xe, ye, zBack));
                    }
                }
            }
        }

        internal static void CollapseNearDuplicates(List<float> splits, float minGap)
        {
            float hi = splits[splits.Count - 1];
            int w = 1;
            for (int r = 1; r < splits.Count; r++)
            {
                if (splits[r] - splits[w - 1] >= minGap && hi - splits[r] >= minGap)
                    splits[w++] = splits[r];
            }
            splits[w++] = hi;
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
                    if (count > 1) break;
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
