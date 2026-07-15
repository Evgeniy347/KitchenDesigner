using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Построитель меша для радиусной полки — прямоугольная доска
    /// в плоскости XZ с одним скруглённым углом. Локальные координаты:
    /// x ∈ [0, width], z ∈ [0, depth], y ∈ [−thickness/2, +thickness/2].
    /// Скруглён угол (x=width, z=depth): дуга радиуса cornerRadius с центром
    /// в (width−R, depth−R), от точки (width, depth−R) до (width−R, depth).</summary>
    public static class RadialShelfMesh
    {
        private const int Segments = 16;
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
            AddCap(vertices, normals, uvs, triangles, width, depth, cornerRadius, -half, Vector3.down);
            AddCap(vertices, normals, uvs, triangles, width, depth, cornerRadius, half, Vector3.up);
            AddCurvedSide(vertices, normals, uvs, triangles, width, depth, cornerRadius, half);
            AddFlatSides(vertices, normals, uvs, triangles, width, depth, cornerRadius, half);

            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Крышка (top/bottom): прямоугольник W×D минус квадрат угла,
        /// плюс веер четверти круга. Разбиение: прямоугольник A (x 0..W−R по всей
        /// глубине), прямоугольник B (x W−R..W, z 0..D−R), веер от центра дуги.</summary>
        private static void AddCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, float width, float depth, float r, float y, Vector3 normal)
        {
            bool up = normal.y > 0;
            float cx = width - r;
            float cz = depth - r;

            if (cx > Eps)
                AddCapQuad(vertices, normals, uvs, triangles, 0, 0, cx, depth, y, normal, width, depth, up);
            if (cz > Eps)
                AddCapQuad(vertices, normals, uvs, triangles, cx, 0, width, cz, y, normal, width, depth, up);

            // Веер четверти круга: центр дуги + точки дуги от (width, depth−R)
            // до (width−R, depth).
            int centerIdx = vertices.Count;
            vertices.Add(new Vector3(cx, y, cz));
            normals.Add(normal);
            uvs.Add(new Vector2(cx / width, cz / depth));

            int start = vertices.Count;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 0.5f / Segments;
                var p = new Vector3(cx + Mathf.Cos(angle) * r, y, cz + Mathf.Sin(angle) * r);
                vertices.Add(p);
                normals.Add(normal);
                uvs.Add(new Vector2(p.x / width, p.z / depth));
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
            float width, float depth, bool up)
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
                uvs.Add(new Vector2(c.x / width, c.z / depth));
            }

            // p00=start, p10=start+1, p01=start+2, p11=start+3.
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

        /// <summary>Скруглённая боковая стенка вдоль дуги угла.</summary>
        private static void AddCurvedSide(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, float width, float depth, float r, float half)
        {
            float cx = width - r;
            float cz = depth - r;
            int start = vertices.Count;

            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 0.5f / Segments;
                var n = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var p = new Vector3(cx + n.x * r, 0, cz + n.z * r);

                vertices.Add(new Vector3(p.x, -half, p.z));
                normals.Add(n);
                uvs.Add(new Vector2(i / (float)Segments, 0));

                vertices.Add(new Vector3(p.x, half, p.z));
                normals.Add(n);
                uvs.Add(new Vector2(i / (float)Segments, 1));
            }

            for (int i = 0; i < Segments; i++)
            {
                int a = start + i * 2;       // низ i
                int b = start + i * 2 + 1;   // верх i
                int c = start + (i + 1) * 2; // низ i+1
                int d = start + (i + 1) * 2 + 1;

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);

                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(d);
            }
        }

        /// <summary>Четыре плоские боковые стенки (укороченные у скруглённого угла).</summary>
        private static void AddFlatSides(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, float width, float depth, float r, float half)
        {
            float cx = width - r;
            float cz = depth - r;

            // z = 0: полная ширина, нормаль −Z.
            AddWall(vertices, normals, uvs, triangles,
                new Vector3(0, 0, 0), new Vector3(width, 0, 0), half, -Vector3.forward);

            // x = width: до начала дуги, нормаль +X.
            if (cz > Eps)
                AddWall(vertices, normals, uvs, triangles,
                    new Vector3(width, 0, 0), new Vector3(width, 0, cz), half, Vector3.right);

            // z = depth: от конца дуги до x=0, нормаль +Z.
            if (cx > Eps)
                AddWall(vertices, normals, uvs, triangles,
                    new Vector3(cx, 0, depth), new Vector3(0, 0, depth), half, Vector3.forward);

            // x = 0: полная глубина, нормаль −X.
            AddWall(vertices, normals, uvs, triangles,
                new Vector3(0, 0, depth), new Vector3(0, 0, 0), half, -Vector3.right);
        }

        /// <summary>Вертикальная стенка от pA до pB (y ±half). Порядок pA→pB
        /// выбирается так, чтобы up×(pB−pA) совпадал с наружной нормалью.</summary>
        private static void AddWall(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector3 pA, Vector3 pB, float half, Vector3 normal)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(pA.x, -half, pA.z)); // bl
            normals.Add(normal);
            uvs.Add(Vector2.zero);
            vertices.Add(new Vector3(pA.x, half, pA.z));  // tl
            normals.Add(normal);
            uvs.Add(Vector2.up);
            vertices.Add(new Vector3(pB.x, -half, pB.z)); // br
            normals.Add(normal);
            uvs.Add(Vector2.right);
            vertices.Add(new Vector3(pB.x, half, pB.z));  // tr
            normals.Add(normal);
            uvs.Add(Vector2.one);

            triangles.Add(start);     // bl
            triangles.Add(start + 1); // tl
            triangles.Add(start + 2); // br

            triangles.Add(start + 2); // br
            triangles.Add(start + 1); // tl
            triangles.Add(start + 3); // tr
        }
    }
}
