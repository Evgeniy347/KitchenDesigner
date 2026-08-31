using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ProfileExtrusionMesh
    {
        public const float HardEdgeAngleDeg = 45f;

        public static Mesh Build(Vector2[] profile, float width, float depth, float thickness,
            float yOffset = 0f)
        {
            var mesh = new Mesh();
            if (profile == null || profile.Length < 3) return mesh;

            int count = profile.Length;
            float half = thickness * 0.5f;
            float uvWidth = Mathf.Max(Tolerance.EpsilonUnits, width);
            float uvDepth = Mathf.Max(Tolerance.EpsilonUnits, depth);

            var faceNormals = FaceNormals(profile, RoundedRectProfile.MinPointSpacing(width, depth));
            var arcLengths = ArcLengths(profile);
            EdgeNormals(faceNormals, out var incoming, out var outgoing);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            AddCap(vertices, normals, uvs, triangles, profile, half + yOffset, true, uvWidth, uvDepth);
            AddCap(vertices, normals, uvs, triangles, profile, -half + yOffset, false, uvWidth, uvDepth);

            float topV = thickness / uvDepth;
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                AddWall(vertices, normals, uvs, triangles,
                    profile[i], profile[next], outgoing[i], incoming[next],
                    arcLengths[i] / uvWidth, arcLengths[i + 1] / uvWidth,
                    -half + yOffset, half + yOffset, topV);
            }

            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2[] FaceNormals(Vector2[] profile, float minSegmentLength)
        {
            var normals = new Vector2[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                var from = profile[i];
                var to = profile[(i + 1) % profile.Length];
                normals[i] = Normalized(new Vector2(to.y - from.y, from.x - to.x), minSegmentLength);
            }

            return normals;
        }

        private static float[] ArcLengths(Vector2[] profile)
        {
            var lengths = new float[profile.Length + 1];
            for (int i = 0; i < profile.Length; i++)
            {
                var from = profile[i];
                var to = profile[(i + 1) % profile.Length];
                float dx = to.x - from.x;
                float dy = to.y - from.y;
                lengths[i + 1] = lengths[i] + Mathf.Sqrt(dx * dx + dy * dy);
            }

            return lengths;
        }

        private static void EdgeNormals(Vector2[] faceNormals, out Vector2[] incoming,
            out Vector2[] outgoing)
        {
            int count = faceNormals.Length;
            incoming = new Vector2[count];
            outgoing = new Vector2[count];
            float hardEdgeCos = Mathf.Cos(HardEdgeAngleDeg * Mathf.Deg2Rad);

            for (int i = 0; i < count; i++)
            {
                var before = faceNormals[(i - 1 + count) % count];
                var after = faceNormals[i];
                bool smooth = before.x * after.x + before.y * after.y > hardEdgeCos;
                var blended = smooth
                    ? Normalized(new Vector2(before.x + after.x, before.y + after.y), 0f)
                    : Vector2.zero;

                incoming[i] = smooth ? blended : before;
                outgoing[i] = smooth ? blended : after;
            }
        }

        private static Vector2 Normalized(Vector2 v, float minLength)
        {
            float lengthSqr = v.x * v.x + v.y * v.y;
            if (lengthSqr < minLength * minLength) return Vector2.zero;
            float length = Mathf.Sqrt(lengthSqr);
            return new Vector2(v.x / length, v.y / length);
        }

        private static Vector2 FootprintUv(Vector2 point, float uvWidth, float uvDepth)
            => new Vector2(point.x / uvWidth + 0.5f, point.y / uvDepth + 0.5f);

        private static void AddCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector2[] profile, float y, bool up, float uvWidth, float uvDepth)
        {
            var normal = up ? Vector3.up : Vector3.down;
            float cx = 0f, cz = 0f;
            foreach (var p in profile) { cx += p.x; cz += p.y; }
            cx /= profile.Length;
            cz /= profile.Length;

            int centreIndex = vertices.Count;
            vertices.Add(new Vector3(cx, y, cz));
            normals.Add(normal);
            uvs.Add(FootprintUv(new Vector2(cx, cz), uvWidth, uvDepth));

            int start = vertices.Count;
            foreach (var p in profile)
            {
                vertices.Add(new Vector3(p.x, y, p.y));
                normals.Add(normal);
                uvs.Add(FootprintUv(p, uvWidth, uvDepth));
            }

            for (int i = 0; i < profile.Length; i++)
            {
                int next = (i + 1) % profile.Length;
                triangles.Add(centreIndex);
                triangles.Add(start + (up ? next : i));
                triangles.Add(start + (up ? i : next));
            }
        }

        private static void AddWall(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector2 from, Vector2 to, Vector2 normalFrom, Vector2 normalTo,
            float uFrom, float uTo, float bottomY, float topY, float topV)
        {
            int start = vertices.Count;

            vertices.Add(new Vector3(from.x, bottomY, from.y));
            normals.Add(new Vector3(normalFrom.x, 0f, normalFrom.y));
            uvs.Add(new Vector2(uFrom, 0f));

            vertices.Add(new Vector3(from.x, topY, from.y));
            normals.Add(new Vector3(normalFrom.x, 0f, normalFrom.y));
            uvs.Add(new Vector2(uFrom, topV));

            vertices.Add(new Vector3(to.x, bottomY, to.y));
            normals.Add(new Vector3(normalTo.x, 0f, normalTo.y));
            uvs.Add(new Vector2(uTo, 0f));

            vertices.Add(new Vector3(to.x, topY, to.y));
            normals.Add(new Vector3(normalTo.x, 0f, normalTo.y));
            uvs.Add(new Vector2(uTo, topV));

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);

            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 3);
        }
    }
}
