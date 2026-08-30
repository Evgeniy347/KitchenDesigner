using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class RadialShelfMesh
    {
        public const int Segments = 16;
        private const float Eps = 1e-5f;

        public static Mesh Build(float width, float depth, float thickness, float cornerRadius)
        {
            cornerRadius = Mathf.Clamp(cornerRadius, Eps, Mathf.Min(width, depth));

            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            float half = thickness * 0.5f;
            var uvScale = new Vector2(Mathf.Max(Eps, width), Mathf.Max(Eps, thickness));
            AddCap(vertices, normals, uvs, triangles, width, depth, cornerRadius, -half, Vector3.down, uvScale);
            AddCap(vertices, normals, uvs, triangles, width, depth, cornerRadius, half, Vector3.up, uvScale);
            AddCurvedSide(vertices, normals, uvs, triangles, width, depth, cornerRadius, half, uvScale);
            AddFlatSides(vertices, normals, uvs, triangles, width, depth, cornerRadius, half, uvScale);

            var toCenter = new Vector3(width * 0.5f, 0f, depth * 0.5f);
            for (int i = 0; i < vertices.Count; i++)
                vertices[i] -= toCenter;

            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, float width, float depth, float r, float y, Vector3 normal,
            Vector2 uvScale)
        {
            bool up = normal.y > 0;
            float cx = width - r;
            float cz = depth - r;

            if (cx > Eps)
                AddCapQuad(vertices, normals, uvs, triangles, 0, 0, cx, depth, y, normal, uvScale, up);
            if (cz > Eps)
                AddCapQuad(vertices, normals, uvs, triangles, cx, 0, width, cz, y, normal, uvScale, up);

            int centerIdx = vertices.Count;
            vertices.Add(new Vector3(cx, y, cz));
            normals.Add(normal);
            uvs.Add(new Vector2(cx / uvScale.x, cz / uvScale.y));

            int start = vertices.Count;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 0.5f / Segments;
                var p = new Vector3(cx + Mathf.Cos(angle) * r, y, cz + Mathf.Sin(angle) * r);
                vertices.Add(p);
                normals.Add(normal);
                uvs.Add(new Vector2(p.x / uvScale.x, p.z / uvScale.y));
            }

            for (int i = 0; i < Segments; i++)
            {
                triangles.Add(centerIdx);
                if (up)
                {
                    triangles.Add(start + i + 1);
                    triangles.Add(start + i);
                }
                else
                {
                    triangles.Add(start + i);
                    triangles.Add(start + i + 1);
                }
            }
        }

        private static void AddCapQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, float x0, float z0, float x1, float z1, float y, Vector3 normal,
            Vector2 uvScale, bool up)
        {
            int start = vertices.Count;
            var corners = new[]
            {
                new Vector3(x0, y, z0),
                new Vector3(x1, y, z0),
                new Vector3(x0, y, z1),
                new Vector3(x1, y, z1),
            };
            foreach (var c in corners)
            {
                vertices.Add(c);
                normals.Add(normal);
                uvs.Add(new Vector2(c.x / uvScale.x, c.z / uvScale.y));
            }

            if (up)
            {
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 1);
            }
            else
            {
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 3);
            }
        }

        private static void AddCurvedSide(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, float width, float depth, float r, float half, Vector2 uvScale)
        {
            float cx = width - r;
            float cz = depth - r;
            int start = vertices.Count;

            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 0.5f / Segments;
                var n = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var p = new Vector3(cx + n.x * r, 0, cz + n.z * r);
                float u = angle * r / uvScale.x;

                vertices.Add(new Vector3(p.x, -half, p.z));
                normals.Add(n);
                uvs.Add(new Vector2(u, 0));

                vertices.Add(new Vector3(p.x, half, p.z));
                normals.Add(n);
                uvs.Add(new Vector2(u, 1));
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
            List<int> triangles, float width, float depth, float r, float half, Vector2 uvScale)
        {
            float cx = width - r;
            float cz = depth - r;

            AddWall(vertices, normals, uvs, triangles,
                new Vector3(0, 0, 0), new Vector3(width, 0, 0), half, -Vector3.forward, uvScale);

            if (cz > Eps)
                AddWall(vertices, normals, uvs, triangles,
                    new Vector3(width, 0, 0), new Vector3(width, 0, cz), half, Vector3.right, uvScale);

            if (cx > Eps)
                AddWall(vertices, normals, uvs, triangles,
                    new Vector3(cx, 0, depth), new Vector3(0, 0, depth), half, Vector3.forward, uvScale);

            AddWall(vertices, normals, uvs, triangles,
                new Vector3(0, 0, depth), new Vector3(0, 0, 0), half, -Vector3.right, uvScale);
        }

        private static void AddWall(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector3 pA, Vector3 pB, float half, Vector3 normal, Vector2 uvScale)
        {
            int start = vertices.Count;
            float u = (pB - pA).magnitude / uvScale.x;

            vertices.Add(new Vector3(pA.x, -half, pA.z));
            normals.Add(normal);
            uvs.Add(Vector2.zero);
            vertices.Add(new Vector3(pA.x, half, pA.z));
            normals.Add(normal);
            uvs.Add(Vector2.up);
            vertices.Add(new Vector3(pB.x, -half, pB.z));
            normals.Add(normal);
            uvs.Add(new Vector2(u, 0f));
            vertices.Add(new Vector3(pB.x, half, pB.z));
            normals.Add(normal);
            uvs.Add(new Vector2(u, 1f));

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);

            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 3);
        }
    }
}
