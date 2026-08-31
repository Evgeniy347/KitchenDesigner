using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class CapsuleTableMesh
    {
        private const int Segments = 16;

        public static Mesh Build(float widthUnits, float thicknessUnits, float depthUnits, float yOffset = 0f)
        {
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            bool alongZ = widthUnits <= depthUnits;
            float R = alongZ ? widthUnits * 0.5f : depthUnits * 0.5f;
            float halfStraight = alongZ ? (depthUnits - widthUnits) * 0.5f : (widthUnits - depthUnits) * 0.5f;

            var profile = BuildProfile(R, halfStraight, alongZ);

            AddTopCap(vertices, normals, uvs, triangles, profile, thicknessUnits * 0.5f + yOffset, widthUnits, depthUnits);
            AddBottomCap(vertices, normals, uvs, triangles, profile, -thicknessUnits * 0.5f + yOffset, widthUnits, depthUnits);
            AddSides(vertices, normals, uvs, triangles, profile, thicknessUnits, yOffset);

            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<Vector2> BuildProfile(float R, float halfStraight, bool alongZ)
        {
            var points = new List<Vector2>();

            if (alongZ)
            {
                for (int i = 0; i <= Segments; i++)
                {
                    float angle = Mathf.PI * i / Segments;
                    float px = Mathf.Cos(angle) * R;
                    float pz = halfStraight + Mathf.Sin(angle) * R;
                    points.Add(new Vector2(px, pz));
                }

                points.Add(new Vector2(-R, -halfStraight));

                for (int i = 0; i <= Segments; i++)
                {
                    float angle = Mathf.PI + Mathf.PI * i / Segments;
                    float px = Mathf.Cos(angle) * R;
                    float pz = -halfStraight + Mathf.Sin(angle) * R;
                    points.Add(new Vector2(px, pz));
                }

                points.Add(new Vector2(R, -halfStraight));
            }
            else
            {
                for (int i = 0; i <= Segments; i++)
                {
                    float angle = Mathf.PI * i / Segments;
                    float pz = Mathf.Cos(angle) * R;
                    float px = halfStraight + Mathf.Sin(angle) * R;
                    points.Add(new Vector2(px, pz));
                }

                points.Add(new Vector2(-halfStraight, -R));

                for (int i = 0; i <= Segments; i++)
                {
                    float angle = Mathf.PI + Mathf.PI * i / Segments;
                    float pz = Mathf.Cos(angle) * R;
                    float px = -halfStraight + Mathf.Sin(angle) * R;
                    points.Add(new Vector2(px, pz));
                }

                points.Add(new Vector2(-halfStraight, R));
            }

            return points;
        }

        private static Vector2 FootprintUv(Vector2 point, float widthUnits, float depthUnits)
            => new Vector2(point.x / Mathf.Max(Tolerance.EpsilonUnits, widthUnits) + 0.5f,
                           point.y / Mathf.Max(Tolerance.EpsilonUnits, depthUnits) + 0.5f);

        private static void AddTopCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, List<Vector2> profile, float y, float widthUnits, float depthUnits)
        {
            int centerIdx = vertices.Count;
            float cx = 0f, cz = 0f;
            foreach (var p in profile) { cx += p.x; cz += p.y; }
            cx /= profile.Count; cz /= profile.Count;

            vertices.Add(new Vector3(cx, y, cz));
            normals.Add(Vector3.up);
            uvs.Add(FootprintUv(new Vector2(cx, cz), widthUnits, depthUnits));

            int start = vertices.Count;
            foreach (var p in profile)
            {
                vertices.Add(new Vector3(p.x, y, p.y));
                normals.Add(Vector3.up);
                uvs.Add(FootprintUv(p, widthUnits, depthUnits));
            }

            for (int i = 0; i < profile.Count; i++)
            {
                int next = (i + 1) % profile.Count;
                triangles.Add(centerIdx);
                triangles.Add(start + next);
                triangles.Add(start + i);
            }
        }

        private static void AddBottomCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, List<Vector2> profile, float y, float widthUnits, float depthUnits)
        {
            int centerIdx = vertices.Count;
            float cx = 0f, cz = 0f;
            foreach (var p in profile) { cx += p.x; cz += p.y; }
            cx /= profile.Count; cz /= profile.Count;

            vertices.Add(new Vector3(cx, y, cz));
            normals.Add(Vector3.down);
            uvs.Add(FootprintUv(new Vector2(cx, cz), widthUnits, depthUnits));

            int start = vertices.Count;
            foreach (var p in profile)
            {
                vertices.Add(new Vector3(p.x, y, p.y));
                normals.Add(Vector3.down);
                uvs.Add(FootprintUv(p, widthUnits, depthUnits));
            }

            for (int i = 0; i < profile.Count; i++)
            {
                int next = (i + 1) % profile.Count;
                triangles.Add(centerIdx);
                triangles.Add(start + i);
                triangles.Add(start + next);
            }
        }

        private static void AddSides(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, List<Vector2> profile, float thickness, float yOffset)
        {
            float half = thickness * 0.5f;
            int start = vertices.Count;

            for (int i = 0; i < profile.Count; i++)
            {
                var p = profile[i];
                var next = profile[(i + 1) % profile.Count];
                var dir = next - p;
                var normal = new Vector2(dir.y, -dir.x).normalized;

                vertices.Add(new Vector3(p.x, -half + yOffset, p.y));
                normals.Add(new Vector3(normal.x, 0, normal.y));
                uvs.Add(new Vector2(i / (float)profile.Count, 0));

                vertices.Add(new Vector3(p.x, half + yOffset, p.y));
                normals.Add(new Vector3(normal.x, 0, normal.y));
                uvs.Add(new Vector2(i / (float)profile.Count, 1));
            }

            for (int i = 0; i < profile.Count; i++)
            {
                int next = (i + 1) % profile.Count;
                int a = start + i * 2;
                int b = start + i * 2 + 1;
                int c = start + next * 2;
                int d = start + next * 2 + 1;

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);

                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(d);
            }
        }

        public const int MinLegInsetFromContourMM = 50;

        public static Vector3[] GetLegPositions(float widthMM, float heightMM, float depthMM,
            float legCenterY, int legInsetMM = 0,
            int legCrossSectionMM = RadiusTableElement.LegCrossSectionMM)
        {
            float toU = AppConstants.MM_TO_UNITS;
            float a = Mathf.Max(Tolerance.EpsilonUnits, widthMM * toU * 0.5f);
            float b = Mathf.Max(Tolerance.EpsilonUnits, depthMM * toU * 0.5f);

            float insetU = Mathf.Max(legInsetMM, MinLegInsetFromContourMM) * toU;
            float legCircumradiusU = legCrossSectionMM * toU * Mathf.Sqrt(2f) * 0.5f;
            float pullU = insetU + legCircumradiusU;

            var quadrants = new[]
            {
                new Vector2( 1f,  1f),
                new Vector2( 1f, -1f),
                new Vector2(-1f,  1f),
                new Vector2(-1f, -1f),
            };

            float cos45 = Mathf.Cos(Mathf.PI * 0.25f);
            float sin45 = Mathf.Sin(Mathf.PI * 0.25f);

            var legs = new Vector3[quadrants.Length];
            for (int i = 0; i < quadrants.Length; i++)
            {
                float sx = quadrants[i].x, sz = quadrants[i].y;
                var onContour = new Vector2(a * cos45 * sx, b * sin45 * sz);
                var inward = new Vector2(-cos45 * sx / a, -sin45 * sz / b).normalized;
                float pull = Mathf.Min(pullU, onContour.magnitude);
                var centre = onContour + inward * pull;
                legs[i] = new Vector3(centre.x, legCenterY, centre.y);
            }

            return legs;
        }
    }
}
