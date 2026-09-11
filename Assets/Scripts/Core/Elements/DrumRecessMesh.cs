using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class DrumRecessMesh
    {
        public const int Segments = 32;

        private const float AngleEpsilon = 1e-4f;

        private const float AxisAlignedDirection = 1e-6f;

        public static Mesh Build(Vector3 sizeMM, Vector2 boreCenterMM, float boreDiameterMM,
            float boreDepthMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            float w = sizeMM.x * toU;
            float h = sizeMM.y * toU;
            float d = sizeMM.z * toU;
            float r = boreDiameterMM * 0.5f * toU;
            float depth = boreDepthMM * toU;
            var bore = new Vector2(boreCenterMM.x * toU, boreCenterMM.y * toU);

            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float halfD = d * 0.5f;
            float boreBottomZ = halfD - depth;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            var angles = RingAngles(bore, halfW, halfH);

            AddFrontRing(vertices, normals, uvs, triangles, angles, bore, r, halfW, halfH, halfD);
            AddBoreWall(vertices, normals, uvs, triangles, angles, bore, r, halfD, boreBottomZ);
            AddBoreBottom(vertices, normals, uvs, triangles, angles, bore, r, boreBottomZ,
                halfW, halfH);
            AddOuterFaces(vertices, normals, uvs, triangles, halfW, halfH, halfD);

            var mesh = new Mesh { name = "DrumRecess" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<float> RingAngles(Vector2 from, float halfW, float halfH)
        {
            var angles = new List<float>(Segments + 4);
            for (int i = 0; i < Segments; i++) angles.Add(i * Mathf.PI * 2f / Segments);

            foreach (var corner in new[]
                     {
                         new Vector2(halfW, halfH), new Vector2(-halfW, halfH),
                         new Vector2(-halfW, -halfH), new Vector2(halfW, -halfH),
                     })
            {
                float angle = Mathf.Atan2(corner.y - from.y, corner.x - from.x);
                if (angle < 0f) angle += Mathf.PI * 2f;
                angles.Add(angle);
            }

            angles.Sort();

            var unique = new List<float>(angles.Count);
            foreach (var angle in angles)
                if (unique.Count == 0 || angle - unique[unique.Count - 1] > AngleEpsilon)
                    unique.Add(angle);
            if (unique.Count > 1
                && Mathf.PI * 2f - unique[unique.Count - 1] + unique[0] <= AngleEpsilon)
                unique.RemoveAt(unique.Count - 1);
            return unique;
        }

        private static Vector2 OnRectangle(Vector2 from, float angle, float halfW, float halfH)
        {
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            float byWidth = Mathf.Abs(cos) < AxisAlignedDirection
                ? float.MaxValue
                : ((cos > 0f ? halfW : -halfW) - from.x) / cos;
            float byHeight = Mathf.Abs(sin) < AxisAlignedDirection
                ? float.MaxValue
                : ((sin > 0f ? halfH : -halfH) - from.y) / sin;

            float t = Mathf.Min(byWidth, byHeight);
            return new Vector2(from.x + cos * t, from.y + sin * t);
        }

        private static Vector2 FaceUv(Vector2 point, float halfW, float halfH) =>
            new Vector2(point.x / (halfW * 2f) + 0.5f, point.y / (halfH * 2f) + 0.5f);

        private static void AddFrontRing(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, List<float> angles, Vector2 bore,
            float r, float halfW, float halfH, float z)
        {
            int start = vertices.Count;
            for (int i = 0; i < angles.Count; i++)
            {
                float angle = angles[i];
                var inner = bore + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
                var outer = OnRectangle(bore, angle, halfW, halfH);

                vertices.Add(new Vector3(inner.x, inner.y, z));
                normals.Add(Vector3.forward);
                uvs.Add(FaceUv(inner, halfW, halfH));

                vertices.Add(new Vector3(outer.x, outer.y, z));
                normals.Add(Vector3.forward);
                uvs.Add(FaceUv(outer, halfW, halfH));
            }

            for (int i = 0; i < angles.Count; i++)
            {
                int next = (i + 1) % angles.Count;
                int ci = start + i * 2;
                int bi = ci + 1;
                int cn = start + next * 2;
                int bn = cn + 1;

                triangles.Add(ci);
                triangles.Add(bi);
                triangles.Add(bn);

                triangles.Add(ci);
                triangles.Add(bn);
                triangles.Add(cn);
            }
        }

        private static void AddBoreWall(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, List<float> angles, Vector2 bore,
            float r, float frontZ, float bottomZ)
        {
            int start = vertices.Count;
            for (int i = 0; i < angles.Count; i++)
            {
                float angle = angles[i];
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                var inward = new Vector3(-cos, -sin, 0f);

                vertices.Add(new Vector3(bore.x + cos * r, bore.y + sin * r, frontZ));
                normals.Add(inward);
                uvs.Add(new Vector2(i / (float)angles.Count, 1f));

                vertices.Add(new Vector3(bore.x + cos * r, bore.y + sin * r, bottomZ));
                normals.Add(inward);
                uvs.Add(new Vector2(i / (float)angles.Count, 0f));
            }

            for (int i = 0; i < angles.Count; i++)
            {
                int next = (i + 1) % angles.Count;
                int fi = start + i * 2;
                int ki = fi + 1;
                int fn = start + next * 2;
                int kn = fn + 1;

                triangles.Add(fi);
                triangles.Add(fn);
                triangles.Add(ki);

                triangles.Add(fn);
                triangles.Add(kn);
                triangles.Add(ki);
            }
        }

        private static void AddBoreBottom(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, List<float> angles, Vector2 bore,
            float r, float z, float halfW, float halfH)
        {
            int center = vertices.Count;
            vertices.Add(new Vector3(bore.x, bore.y, z));
            normals.Add(Vector3.forward);
            uvs.Add(FaceUv(bore, halfW, halfH));

            int ring = vertices.Count;
            for (int i = 0; i < angles.Count; i++)
            {
                float cos = Mathf.Cos(angles[i]);
                float sin = Mathf.Sin(angles[i]);
                var point = bore + new Vector2(cos * r, sin * r);
                vertices.Add(new Vector3(point.x, point.y, z));
                normals.Add(Vector3.forward);
                uvs.Add(FaceUv(point, halfW, halfH));
            }

            for (int i = 0; i < angles.Count; i++)
            {
                int next = (i + 1) % angles.Count;
                triangles.Add(center);
                triangles.Add(ring + i);
                triangles.Add(ring + next);
            }
        }

        private static void AddOuterFaces(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, float halfW, float halfH, float halfD)
        {
            AddQuad(vertices, normals, uvs, triangles, Vector3.back,
                new Vector3(-halfW, -halfH, -halfD), new Vector3(-halfW, halfH, -halfD),
                new Vector3(halfW, halfH, -halfD), new Vector3(halfW, -halfH, -halfD));

            AddQuad(vertices, normals, uvs, triangles, Vector3.right,
                new Vector3(halfW, -halfH, -halfD), new Vector3(halfW, halfH, -halfD),
                new Vector3(halfW, halfH, halfD), new Vector3(halfW, -halfH, halfD));

            AddQuad(vertices, normals, uvs, triangles, Vector3.left,
                new Vector3(-halfW, -halfH, halfD), new Vector3(-halfW, halfH, halfD),
                new Vector3(-halfW, halfH, -halfD), new Vector3(-halfW, -halfH, -halfD));

            AddQuad(vertices, normals, uvs, triangles, Vector3.up,
                new Vector3(-halfW, halfH, -halfD), new Vector3(-halfW, halfH, halfD),
                new Vector3(halfW, halfH, halfD), new Vector3(halfW, halfH, -halfD));

            AddQuad(vertices, normals, uvs, triangles, Vector3.down,
                new Vector3(-halfW, -halfH, halfD), new Vector3(-halfW, -halfH, -halfD),
                new Vector3(halfW, -halfH, -halfD), new Vector3(halfW, -halfH, halfD));
        }

        private static void AddQuad(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, Vector3 normal,
            Vector3 a, Vector3 b, Vector3 c, Vector3 e)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(e);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
    }
}
