using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public static class PipeFittingMesh
    {
        public const int Segments = 24;

        public const float ParallelReferenceLimit = 0.9f;

        public static Mesh Build(PipeNodeKind kind, string sizeId)
        {
            float toU = AppConstants.MM_TO_UNITS;
            float radius = PipeFittingSpec.BodyDiameterMm(sizeId) * 0.5f * toU;
            var hub = ToUnits(PipeFittingSpec.HubOffsetMm(kind, sizeId));

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i < PipeFittingSpec.PortCount(kind); i++)
                AddTube(vertices, normals, uvs, triangles, hub,
                    ToUnits(PipeFittingSpec.PortOffsetMm(kind, sizeId, i)), radius);

            if (PipeFittingSpec.HasFlange(kind))
                AddTube(vertices, normals, uvs, triangles, hub,
                    hub + Vector3.down * (PipeFittingSpec.FlangeThicknessMm * toU),
                    PipeFittingSpec.FlangeDiameterMm(sizeId) * 0.5f * toU);

            var mesh = new Mesh
            {
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                uv = uvs.ToArray(),
                triangles = triangles.ToArray(),
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 ToUnits(in PointMm point) => new Vector3(
            point.XMm * AppConstants.MM_TO_UNITS,
            point.YMm * AppConstants.MM_TO_UNITS,
            point.ZMm * AppConstants.MM_TO_UNITS);

        private static void AddTube(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, Vector3 from, Vector3 to, float radius)
        {
            var along = to - from;
            if (along.sqrMagnitude <= Tolerance.EpsilonSqr) return;

            var axis = along.normalized;
            var reference = Mathf.Abs(axis.y) > ParallelReferenceLimit
                ? Vector3.right
                : Vector3.up;
            var right = Vector3.Cross(axis, reference).normalized;
            var forward = Vector3.Cross(axis, right).normalized;

            AddCap(vertices, normals, uvs, triangles, from, right, forward, -axis, radius);
            AddSide(vertices, normals, uvs, triangles, from, to, right, forward, radius);
            AddCap(vertices, normals, uvs, triangles, to, right, forward, axis, radius);
        }

        private static void AddCap(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, Vector3 centre, Vector3 right,
            Vector3 forward, Vector3 outward, float radius)
        {
            int centreIndex = vertices.Count;
            vertices.Add(centre);
            normals.Add(outward);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int ring = vertices.Count;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices.Add(centre + (right * cos + forward * sin) * radius);
                normals.Add(outward);
                uvs.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
            }

            bool flip = Vector3.Dot(Vector3.Cross(right, forward), outward) > 0f;
            for (int i = 0; i < Segments; i++)
            {
                int a = ring + i;
                int b = ring + i + 1;
                triangles.Add(centreIndex);
                triangles.Add(flip ? b : a);
                triangles.Add(flip ? a : b);
            }
        }

        private static void AddSide(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, Vector3 from, Vector3 to,
            Vector3 right, Vector3 forward, float radius)
        {
            int start = vertices.Count;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                var normal = (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)).normalized;
                float u = (float)i / Segments;

                vertices.Add(from + normal * radius);
                normals.Add(normal);
                uvs.Add(new Vector2(u, 0f));

                vertices.Add(to + normal * radius);
                normals.Add(normal);
                uvs.Add(new Vector2(u, 1f));
            }

            for (int i = 0; i < Segments; i++)
            {
                int a = start + i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
        }
    }
}
