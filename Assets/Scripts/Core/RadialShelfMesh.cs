using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Построитель меша для радиусной (угловой) полки — сектор
    /// цилиндра 90° в плоскости XZ, вытянутый по Y (толщина).</summary>
    public static class RadialShelfMesh
    {
        private const int Segments = 16;

        public static Mesh Build(float radiusUnits, float heightUnits)
        {
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Центр сектора в начале координат, угол от +X к +Z (0..90°).
            // Вершины строим в локальных координатах, ось Y — толщина полки.
            AddBottomCap(vertices, normals, uvs, triangles, radiusUnits, -heightUnits * 0.5f);
            AddTopCap(vertices, normals, uvs, triangles, radiusUnits, heightUnits * 0.5f);
            AddCurvedSide(vertices, normals, uvs, triangles, radiusUnits, heightUnits);
            AddFlatSides(vertices, normals, uvs, triangles, radiusUnits, heightUnits);

            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBottomCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, float radius, float y)
        {
            int centerIdx = vertices.Count;
            vertices.Add(new Vector3(0, y, 0));
            normals.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int start = vertices.Count;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 0.5f / Segments;
                var p = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                vertices.Add(p);
                normals.Add(Vector3.down);
                uvs.Add(new Vector2(p.x / radius * 0.5f + 0.5f, p.z / radius * 0.5f + 0.5f));
            }

            for (int i = 0; i < Segments; i++)
            {
                triangles.Add(centerIdx);
                triangles.Add(start + i + 1);
                triangles.Add(start + i);
            }
        }

        private static void AddTopCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, float radius, float y)
        {
            int centerIdx = vertices.Count;
            vertices.Add(new Vector3(0, y, 0));
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int start = vertices.Count;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 0.5f / Segments;
                var p = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                vertices.Add(p);
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(p.x / radius * 0.5f + 0.5f, p.z / radius * 0.5f + 0.5f));
            }

            for (int i = 0; i < Segments; i++)
            {
                triangles.Add(centerIdx);
                triangles.Add(start + i);
                triangles.Add(start + i + 1);
            }
        }

        private static void AddCurvedSide(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, float radius, float height)
        {
            float half = height * 0.5f;
            int start = vertices.Count;

            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 0.5f / Segments;
                var n = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var p = n * radius;

                vertices.Add(new Vector3(p.x, -half, p.z));
                normals.Add(n);
                uvs.Add(new Vector2(i / (float)Segments, 0));

                vertices.Add(new Vector3(p.x, half, p.z));
                normals.Add(n);
                uvs.Add(new Vector2(i / (float)Segments, 1));
            }

            for (int i = 0; i < Segments; i++)
            {
                int a = start + i * 2;
                int b = start + i * 2 + 1;
                int c = start + (i + 1) * 2;
                int d = start + (i + 1) * 2 + 1;

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);

                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(d);
            }
        }

        private static void AddFlatSides(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, float radius, float height)
        {
            float half = height * 0.5f;

            // Сторона вдоль оси X (z = 0, нормаль -Z).
            int xStart = vertices.Count;
            vertices.Add(new Vector3(0, -half, 0));
            normals.Add(-Vector3.forward);
            uvs.Add(Vector2.zero);
            vertices.Add(new Vector3(radius, -half, 0));
            normals.Add(-Vector3.forward);
            uvs.Add(Vector2.right);
            vertices.Add(new Vector3(0, half, 0));
            normals.Add(-Vector3.forward);
            uvs.Add(Vector2.up);
            vertices.Add(new Vector3(radius, half, 0));
            normals.Add(-Vector3.forward);
            uvs.Add(Vector2.one);

            triangles.Add(xStart);
            triangles.Add(xStart + 2);
            triangles.Add(xStart + 1);
            triangles.Add(xStart + 1);
            triangles.Add(xStart + 2);
            triangles.Add(xStart + 3);

            // Сторона вдоль оси Z (x = 0, нормаль -X).
            int zStart = vertices.Count;
            vertices.Add(new Vector3(0, -half, 0));
            normals.Add(-Vector3.right);
            uvs.Add(Vector2.zero);
            vertices.Add(new Vector3(0, -half, radius));
            normals.Add(-Vector3.right);
            uvs.Add(Vector2.right);
            vertices.Add(new Vector3(0, half, 0));
            normals.Add(-Vector3.right);
            uvs.Add(Vector2.up);
            vertices.Add(new Vector3(0, half, radius));
            normals.Add(-Vector3.right);
            uvs.Add(Vector2.one);

            triangles.Add(zStart);
            triangles.Add(zStart + 1);
            triangles.Add(zStart + 2);
            triangles.Add(zStart + 1);
            triangles.Add(zStart + 3);
            triangles.Add(zStart + 2);
        }
    }
}
