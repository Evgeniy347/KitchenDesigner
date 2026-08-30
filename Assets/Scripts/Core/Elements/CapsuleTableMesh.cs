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

            AddTopCap(vertices, normals, uvs, triangles, profile, thicknessUnits * 0.5f + yOffset);
            AddBottomCap(vertices, normals, uvs, triangles, profile, -thicknessUnits * 0.5f + yOffset);
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

        private static void AddTopCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, List<Vector2> profile, float y)
        {
            int centerIdx = vertices.Count;
            float cx = 0f, cz = 0f;
            foreach (var p in profile) { cx += p.x; cz += p.y; }
            cx /= profile.Count; cz /= profile.Count;

            vertices.Add(new Vector3(cx, y, cz));
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int start = vertices.Count;
            foreach (var p in profile)
            {
                vertices.Add(new Vector3(p.x, y, p.y));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(p.x * 0.5f + 0.5f, p.y * 0.5f + 0.5f));
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
            List<int> triangles, List<Vector2> profile, float y)
        {
            int centerIdx = vertices.Count;
            float cx = 0f, cz = 0f;
            foreach (var p in profile) { cx += p.x; cz += p.y; }
            cx /= profile.Count; cz /= profile.Count;

            vertices.Add(new Vector3(cx, y, cz));
            normals.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int start = vertices.Count;
            foreach (var p in profile)
            {
                vertices.Add(new Vector3(p.x, y, p.y));
                normals.Add(Vector3.down);
                uvs.Add(new Vector2(p.x * 0.5f + 0.5f, p.y * 0.5f + 0.5f));
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

        public static Vector3[] GetLegPositions(float widthMM, float heightMM, float depthMM, float legCenterY, int legInsetMM = 0)
        {
            float toU = AppConstants.MM_TO_UNITS;
            float w = widthMM * toU;
            float d = depthMM * toU;
            float inset = legInsetMM * toU;

            bool alongZ = w <= d;
            float R = alongZ ? w * 0.5f : d * 0.5f;
            float halfStraight = alongZ ? (d - w) * 0.5f : (w - d) * 0.5f;

            float c45 = R * Mathf.Cos(Mathf.PI * 0.25f);
            float s45 = R * Mathf.Sin(Mathf.PI * 0.25f);

            float insetX = inset * Mathf.Cos(Mathf.PI * 0.25f);
            float insetZ = inset * Mathf.Sin(Mathf.PI * 0.25f);

            Vector3[] legs;
            if (alongZ)
            {
                legs = new Vector3[]
                {
                    new Vector3( c45 - insetX, legCenterY,  halfStraight + s45 - insetZ),
                    new Vector3(-c45 + insetX, legCenterY,  halfStraight + s45 - insetZ),
                    new Vector3( c45 - insetX, legCenterY, -(halfStraight + s45 - insetZ)),
                    new Vector3(-c45 + insetX, legCenterY, -(halfStraight + s45 - insetZ))
                };
            }
            else
            {
                legs = new Vector3[]
                {
                    new Vector3( halfStraight + s45 - insetZ, legCenterY,  c45 - insetX),
                    new Vector3( halfStraight + s45 - insetZ, legCenterY, -c45 + insetX),
                    new Vector3(-(halfStraight + s45 - insetZ), legCenterY,  c45 - insetX),
                    new Vector3(-(halfStraight + s45 - insetZ), legCenterY, -c45 + insetX)
                };
            }

            return legs;
        }
    }
}
