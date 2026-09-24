using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BoxRunMesh
    {
        public const int FaceCount = 6;
        public const int VerticesPerFace = 4;
        public const int TrianglesPerFace = 2;
        public const int IndicesPerTriangle = 3;

        public static (Vector3[] Vertices, int[] Triangles) Build(
            Vector3 fromMm, Vector3 toMm, Vector3 widthAxis, float widthMm, float heightMm)
        {
            var direction = (toMm - fromMm).normalized;
            var width = widthAxis.normalized;
            var height = Vector3.Cross(direction, width);
            float halfWidth = widthMm * 0.5f;
            float halfHeight = heightMm * 0.5f;

            var fromCorners = Corners(fromMm, width, height, halfWidth, halfHeight);
            var toCorners = Corners(toMm, width, height, halfWidth, halfHeight);

            var vertices = new Vector3[FaceCount * VerticesPerFace];
            var triangles = new int[FaceCount * TrianglesPerFace * IndicesPerTriangle];
            int v = 0;
            int t = 0;

            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                AddQuad(vertices, triangles, ref v, ref t,
                    fromCorners[i], fromCorners[j], toCorners[j], toCorners[i]);
            }

            AddQuad(vertices, triangles, ref v, ref t,
                fromCorners[3], fromCorners[2], fromCorners[1], fromCorners[0]);

            AddQuad(vertices, triangles, ref v, ref t,
                toCorners[0], toCorners[1], toCorners[2], toCorners[3]);

            return (vertices, triangles);
        }

        public static float UnfoldedAreaM2(Vector3 fromMm, Vector3 toMm, float widthMm, float heightMm)
        {
            float lengthMm = (toMm - fromMm).magnitude;
            float perimeterMm = 2f * (widthMm + heightMm);
            return perimeterMm * lengthMm * 1e-6f;
        }

        private static Vector3[] Corners(Vector3 centre, Vector3 widthAxis, Vector3 heightAxis,
            float halfWidth, float halfHeight) => new[]
        {
            centre - widthAxis * halfWidth - heightAxis * halfHeight,
            centre + widthAxis * halfWidth - heightAxis * halfHeight,
            centre + widthAxis * halfWidth + heightAxis * halfHeight,
            centre - widthAxis * halfWidth + heightAxis * halfHeight,
        };

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
