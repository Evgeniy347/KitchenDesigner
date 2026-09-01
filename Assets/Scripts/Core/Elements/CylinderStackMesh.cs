using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct CylinderSection
    {
        public readonly float Radius;
        public readonly float Height;

        public CylinderSection(float radius, float height)
        {
            Radius = radius;
            Height = height;
        }
    }

    public static class CylinderStackMesh
    {
        public const int Segments = 24;

        public static Mesh Build(params CylinderSection[] bottomToTop)
        {
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            float total = 0f;
            foreach (var section in bottomToTop) total += section.Height;

            float y = -total * 0.5f;
            foreach (var section in bottomToTop)
            {
                AddCylinder(vertices, normals, uvs, triangles,
                    section.Radius, section.Height, y + section.Height * 0.5f);
                y += section.Height;
            }

            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCylinder(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, float radius, float height, float centerY)
        {
            float halfH = height * 0.5f;
            float topY = centerY + halfH;
            float bottomY = centerY - halfH;

            AddCap(vertices, normals, uvs, triangles, radius, bottomY, Vector3.down);
            AddSide(vertices, normals, uvs, triangles, radius, bottomY, topY);
            AddCap(vertices, normals, uvs, triangles, radius, topY, Vector3.up);
        }

        private static void AddCap(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, float radius, float y, Vector3 normal)
        {
            int center = vertices.Count;
            vertices.Add(new Vector3(0f, y, 0f));
            normals.Add(normal);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int ring = vertices.Count;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices.Add(new Vector3(cos * radius, y, sin * radius));
                normals.Add(normal);
                uvs.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
            }

            bool up = normal.y > 0f;
            for (int i = 0; i < Segments; i++)
            {
                triangles.Add(center);
                triangles.Add(ring + (up ? i + 1 : i));
                triangles.Add(ring + (up ? i : i + 1));
            }
        }

        private static void AddSide(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, float radius, float bottomY, float topY)
        {
            int start = vertices.Count;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                var normal = new Vector3(cos, 0f, sin);
                vertices.Add(new Vector3(cos * radius, bottomY, sin * radius));
                normals.Add(normal);
                uvs.Add(new Vector2(i / (float)Segments, 0f));
                vertices.Add(new Vector3(cos * radius, topY, sin * radius));
                normals.Add(normal);
                uvs.Add(new Vector2(i / (float)Segments, 1f));
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
    }
}
