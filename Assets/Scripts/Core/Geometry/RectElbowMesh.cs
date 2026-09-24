using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class RectElbowMesh
    {
        public const int PolygonVertexCount = 7;
        public const int ReflexVertexIndex = 5;
        public const int SideFaceCount = 7;

        public static (Vector3[] Vertices, int[] Triangles) Build(
            Vector3 hub, Vector3 axisA, Vector3 axisB, float widthMm, float heightMm,
            float legLengthMm)
        {
            var u = axisA.normalized;
            var v = axisB.normalized;
            var w = Vector3.Cross(u, v).normalized;
            float hh = heightMm * 0.5f;
            float hw = widthMm * 0.5f;

            var polygon = CrossSection(legLengthMm, hh);

            Vector3 At(Vector2 p, float wOffset) => hub + u * p.x + v * p.y + w * wOffset;

            var nearRing = new Vector3[PolygonVertexCount];
            var farRing = new Vector3[PolygonVertexCount];
            for (int i = 0; i < PolygonVertexCount; i++)
            {
                nearRing[i] = At(polygon[i], -hw);
                farRing[i] = At(polygon[i], hw);
            }

            int capTriangleCount = PolygonVertexCount - 2;
            var vertices = new Vector3[PolygonVertexCount * 2 + SideFaceCount * 4];
            var triangles = new int[capTriangleCount * 2 * 3 + SideFaceCount * 2 * 3];
            int vi = 0;
            int ti = 0;

            AppendCap(vertices, triangles, ref vi, ref ti, nearRing, flip: true);
            AppendCap(vertices, triangles, ref vi, ref ti, farRing, flip: false);

            for (int i = 0; i < PolygonVertexCount; i++)
            {
                int j = (i + 1) % PolygonVertexCount;
                AddQuad(vertices, triangles, ref vi, ref ti,
                    nearRing[i], nearRing[j], farRing[j], farRing[i]);
            }

            return (vertices, triangles);
        }

        public static float ClosedSurfaceAreaMm2(float widthMm, float heightMm, float legLengthMm)
        {
            float hh = heightMm * 0.5f;
            float capArea = 4f * legLengthMm * hh - 2f * hh * hh;
            float mitreLengthMm = 2f * hh * Mathf.Sqrt(2f);
            float sideRimLengthMm = 4f * legLengthMm + mitreLengthMm;
            return 2f * capArea + widthMm * sideRimLengthMm;
        }

        private static Vector2[] CrossSection(float legLengthMm, float hh) => new[]
        {
            new Vector2(legLengthMm, -hh),
            new Vector2(hh, -hh),
            new Vector2(-hh, hh),
            new Vector2(-hh, legLengthMm),
            new Vector2(hh, legLengthMm),
            new Vector2(hh, hh),
            new Vector2(legLengthMm, hh),
        };

        private static void AppendCap(Vector3[] vertices, int[] triangles, ref int vi, ref int ti,
            Vector3[] ring, bool flip)
        {
            int start = vi;
            for (int i = 0; i < ring.Length; i++) vertices[vi++] = ring[i];

            for (int i = 0; i < ring.Length; i++)
            {
                if (i == ReflexVertexIndex) continue;
                int next = (i + 1) % ring.Length;
                if (next == ReflexVertexIndex) continue;

                if (flip)
                {
                    triangles[ti++] = start + ReflexVertexIndex;
                    triangles[ti++] = start + next;
                    triangles[ti++] = start + i;
                }
                else
                {
                    triangles[ti++] = start + ReflexVertexIndex;
                    triangles[ti++] = start + i;
                    triangles[ti++] = start + next;
                }
            }
        }

        private static void AddQuad(Vector3[] vertices, int[] triangles, ref int v, ref int t,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = v;
            vertices[v++] = a;
            vertices[v++] = b;
            vertices[v++] = c;
            vertices[v++] = d;

            triangles[t++] = start;
            triangles[t++] = start + 1;
            triangles[t++] = start + 2;
            triangles[t++] = start;
            triangles[t++] = start + 2;
            triangles[t++] = start + 3;
        }
    }
}
