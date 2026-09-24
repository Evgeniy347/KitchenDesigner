using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class RoofPlaneMesh
    {
        public Vector3[] Positions { get; }
        public Vector3[] Normals { get; }
        public Vector2[] Uvs { get; }
        public int[] Triangles { get; }
        public Vector3 Normal { get; }

        public RoofPlaneMesh(IReadOnlyList<Vector3>? topBoundary, float thickness)
        {
            if (topBoundary == null || topBoundary.Count < 3)
            {
                Positions = Array.Empty<Vector3>();
                Normals = Array.Empty<Vector3>();
                Uvs = Array.Empty<Vector2>();
                Triangles = Array.Empty<int>();
                Normal = Vector3.zero;
                return;
            }

            int n = topBoundary.Count;
            var normal = ComputeNormal(topBoundary);
            Normal = normal;

            var drop = normal * Mathf.Max(0f, thickness);
            var bottomBoundary = new Vector3[n];
            for (int i = 0; i < n; i++) bottomBoundary[i] = topBoundary[i] - drop;

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            AddFan(positions, normals, uvs, triangles, topBoundary, normal, reversed: false);
            AddFan(positions, normals, uvs, triangles, bottomBoundary, -normal, reversed: true);

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                AddSideQuad(positions, normals, uvs, triangles,
                    topBoundary[i], topBoundary[next], bottomBoundary[i], bottomBoundary[next]);
            }

            Positions = positions.ToArray();
            Normals = normals.ToArray();
            Uvs = uvs.ToArray();
            Triangles = triangles.ToArray();
        }

        private static Vector3 ComputeNormal(IReadOnlyList<Vector3> boundary)
        {
            var a = boundary[1] - boundary[0];
            var b = boundary[2] - boundary[0];
            var n = Vector3.Cross(a, b);
            return n.sqrMagnitude < Tolerance.EpsilonSqr ? Vector3.up : n.normalized;
        }

        private static void AddFan(List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, IReadOnlyList<Vector3> boundary, Vector3 normal, bool reversed)
        {
            int start = positions.Count;
            var (u, v) = PlaneAxes(normal);

            for (int i = 0; i < boundary.Count; i++)
            {
                positions.Add(boundary[i]);
                normals.Add(normal);
                uvs.Add(new Vector2(Vector3.Dot(boundary[i], u), Vector3.Dot(boundary[i], v)));
            }

            for (int i = 1; i + 1 < boundary.Count; i++)
            {
                int b = start + i;
                int c = start + i + 1;
                triangles.Add(start);
                triangles.Add(reversed ? c : b);
                triangles.Add(reversed ? b : c);
            }
        }

        private static (Vector3, Vector3) PlaneAxes(Vector3 normal)
        {
            var reference = Mathf.Abs(normal.y) < Tolerance.UpDotThreshold ? Vector3.up : Vector3.right;
            var u = Vector3.Cross(reference, normal).normalized;
            var v = Vector3.Cross(normal, u);
            return (u, v);
        }

        private static void AddSideQuad(List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector3 topFrom, Vector3 topTo, Vector3 bottomFrom, Vector3 bottomTo)
        {
            int start = positions.Count;
            var edge = topTo - topFrom;
            var up = topFrom - bottomFrom;
            var sideNormal = Vector3.Cross(edge, up);
            sideNormal = sideNormal.sqrMagnitude < Tolerance.EpsilonSqr
                ? Vector3.zero
                : sideNormal.normalized;

            positions.Add(bottomFrom); normals.Add(sideNormal); uvs.Add(new Vector2(0f, 0f));
            positions.Add(topFrom); normals.Add(sideNormal); uvs.Add(new Vector2(0f, 1f));
            positions.Add(bottomTo); normals.Add(sideNormal); uvs.Add(new Vector2(1f, 0f));
            positions.Add(topTo); normals.Add(sideNormal); uvs.Add(new Vector2(1f, 1f));

            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);

            triangles.Add(start + 2);
            triangles.Add(start + 3);
            triangles.Add(start + 1);
        }
    }
}
