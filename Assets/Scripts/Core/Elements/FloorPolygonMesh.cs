using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class FloorPolygonMesh
    {
        public static Mesh Build(IReadOnlyList<Vector2Int> localPointsMm, Vector2Int sizeMm)
        {
            if (localPointsMm == null || localPointsMm.Count < 3)
                throw new ArgumentException("Floor polygon requires at least 3 points");
            if (sizeMm.x <= 0 || sizeMm.y <= 0)
                throw new ArgumentException("Floor polygon bounds must be positive");

            var points = new List<Vector2>(localPointsMm.Count);
            foreach (var p in localPointsMm)
                points.Add(new Vector2((float)p.x / sizeMm.x, (float)p.y / sizeMm.y));
            if (SignedArea(points) < 0f) points.Reverse();

            var face = Triangulate(points);
            var vertices = new List<Vector3>(points.Count * 2 + points.Count * 4);
            var triangles = new List<int>(face.Count * 2 + points.Count * 6);

            for (int i = 0; i < points.Count; i++)
            {
                vertices.Add(new Vector3(points[i].x, 0.5f, points[i].y));
                vertices.Add(new Vector3(points[i].x, -0.5f, points[i].y));
            }
            for (int i = 0; i < face.Count; i += 3)
            {
                int a = face[i], b = face[i + 1], c = face[i + 2];
                triangles.Add(a * 2); triangles.Add(c * 2); triangles.Add(b * 2);
                triangles.Add(a * 2 + 1); triangles.Add(b * 2 + 1); triangles.Add(c * 2 + 1);
            }
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                int k = vertices.Count;
                vertices.Add(new Vector3(points[i].x, -0.5f, points[i].y));
                vertices.Add(new Vector3(points[j].x, -0.5f, points[j].y));
                vertices.Add(new Vector3(points[j].x, 0.5f, points[j].y));
                vertices.Add(new Vector3(points[i].x, 0.5f, points[i].y));
                triangles.Add(k); triangles.Add(k + 2); triangles.Add(k + 1);
                triangles.Add(k); triangles.Add(k + 3); triangles.Add(k + 2);
            }

            var mesh = new Mesh { name = "FloorPolygon" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<int> Triangulate(IReadOnlyList<Vector2> p)
        {
            var remaining = new List<int>();
            for (int i = 0; i < p.Count; i++) remaining.Add(i);
            var result = new List<int>((p.Count - 2) * 3);
            int guard = p.Count * p.Count;
            while (remaining.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int b = remaining[i];
                    int c = remaining[(i + 1) % remaining.Count];
                    if (Cross(p[a], p[b], p[c]) <= 0f) continue;
                    bool contains = false;
                    for (int q = 0; q < remaining.Count; q++)
                    {
                        int v = remaining[q];
                        if (v != a && v != b && v != c && InTriangle(p[v], p[a], p[b], p[c]))
                        { contains = true; break; }
                    }
                    if (contains) continue;
                    result.Add(a); result.Add(b); result.Add(c);
                    remaining.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) throw new ArgumentException("Floor polygon must be simple and non-degenerate");
            }
            if (remaining.Count != 3) throw new ArgumentException("Floor polygon cannot be triangulated");
            result.Add(remaining[0]); result.Add(remaining[1]); result.Add(remaining[2]);
            return result;
        }

        private static float SignedArea(IReadOnlyList<Vector2> p)
        {
            float area = 0f;
            for (int i = 0; i < p.Count; i++)
            { var a = p[i]; var b = p[(i + 1) % p.Count]; area += a.x * b.y - b.x * a.y; }
            return area * 0.5f;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
            (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
            Cross(a, b, p) >= 0f && Cross(b, c, p) >= 0f && Cross(c, a, p) >= 0f;
    }
}
