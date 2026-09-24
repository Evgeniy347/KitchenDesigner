using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class RectTransitionMesh
    {
        public const int FaceCount = 6;
        public const int VerticesPerFace = 4;
        public const int TrianglesPerFace = 2;
        public const int IndicesPerTriangle = 3;

        public static (Vector3[] Vertices, int[] Triangles) Build(
            Vector3 fromMm, Vector3 toMm, Vector3 widthAxis,
            float widthAMm, float heightAMm, float widthBMm, float heightBMm)
        {
            var direction = (toMm - fromMm).normalized;
            var width = widthAxis.normalized;
            var height = Vector3.Cross(direction, width);

            var fromCorners = Corners(fromMm, width, height, widthAMm * 0.5f, heightAMm * 0.5f);
            var toCorners = Corners(toMm, width, height, widthBMm * 0.5f, heightBMm * 0.5f);

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

        public static float LateralAreaM2(Vector3 fromMm, Vector3 toMm,
            float widthAMm, float heightAMm, float widthBMm, float heightBMm)
        {
            float lengthMm = (toMm - fromMm).magnitude;
            float slantAlongHeightMm = SlantMm(lengthMm, (heightBMm - heightAMm) * 0.5f);
            float slantAlongWidthMm = SlantMm(lengthMm, (widthBMm - widthAMm) * 0.5f);

            float widthFacesMm2 = (widthAMm + widthBMm) * slantAlongHeightMm;
            float heightFacesMm2 = (heightAMm + heightBMm) * slantAlongWidthMm;
            return (widthFacesMm2 + heightFacesMm2) * 1e-6f;
        }

        public static float ClosedSurfaceAreaMm2(Vector3 fromMm, Vector3 toMm,
            float widthAMm, float heightAMm, float widthBMm, float heightBMm)
        {
            float capsMm2 = widthAMm * heightAMm + widthBMm * heightBMm;
            float lateralMm2 = LateralAreaM2(fromMm, toMm, widthAMm, heightAMm, widthBMm, heightBMm)
                * 1e6f;
            return capsMm2 + lateralMm2;
        }

        private static float SlantMm(float lengthMm, float halfDeltaMm) =>
            Mathf.Sqrt(lengthMm * lengthMm + halfDeltaMm * halfDeltaMm);

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
