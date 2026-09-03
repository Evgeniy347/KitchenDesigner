using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class TubeMesh
    {
        public static void Append(MeshAccumulator target, IReadOnlyList<Vector3> points,
            IReadOnlyList<float> radii, int radialSegments)
        {
            if (points.Count < 2) return;

            int sides = Mathf.Max(3, radialSegments);
            var tangents = Tangents(points);
            var normals = Frames(tangents);
            int firstRing = target.VertexCount;

            for (int i = 0; i < points.Count; i++)
                AddRing(target, points[i], tangents[i], normals[i], radii[i],
                    i / (float)(points.Count - 1), sides);

            for (int i = 0; i + 1 < points.Count; i++)
                StitchRings(target, firstRing + i * (sides + 1),
                    firstRing + (i + 1) * (sides + 1), sides);

            AddCap(target, points[0], tangents[0], normals[0], radii[0], false, sides);
            int last = points.Count - 1;
            AddCap(target, points[last], tangents[last], normals[last], radii[last], true, sides);
        }

        public static void AppendSegment(MeshAccumulator target, Vector3 from, Vector3 to,
            float fromRadius, float toRadius, int radialSegments) =>
            Append(target, new[] { from, to }, new[] { fromRadius, toRadius }, radialSegments);

        private static Vector3[] Tangents(IReadOnlyList<Vector3> points)
        {
            var tangents = new Vector3[points.Count];
            int last = points.Count - 1;

            tangents[0] = Safe(points[1] - points[0], Vector3.up);
            tangents[last] = Safe(points[last] - points[last - 1], tangents[0]);
            for (int i = 1; i < last; i++)
                tangents[i] = Safe(points[i + 1] - points[i - 1], tangents[i - 1]);

            return tangents;
        }

        private static Vector3[] Frames(Vector3[] tangents)
        {
            var normals = new Vector3[tangents.Length];
            normals[0] = Perpendicular(tangents[0]);

            for (int i = 1; i < tangents.Length; i++)
            {
                var carried = normals[i - 1] - tangents[i] * Vector3.Dot(normals[i - 1], tangents[i]);
                normals[i] = carried.sqrMagnitude > Tolerance.EpsilonSqr
                    ? carried.normalized
                    : Perpendicular(tangents[i]);
            }

            return normals;
        }

        private static Vector3 Perpendicular(Vector3 tangent)
        {
            var reference = Mathf.Abs(tangent.y) < Tolerance.UpDotThreshold
                ? Vector3.up
                : Vector3.forward;
            return Vector3.Cross(reference, tangent).normalized;
        }

        private static Vector3 Safe(Vector3 direction, Vector3 fallback)
        {
            return direction.sqrMagnitude > Tolerance.EpsilonSqr
                ? direction.normalized
                : fallback;
        }

        private static void AddRing(MeshAccumulator target, Vector3 centre, Vector3 tangent,
            Vector3 normal, float radius, float v, int sides)
        {
            var binormal = Vector3.Cross(tangent, normal);

            for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                var outward = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                target.AddVertex(centre + outward * radius, outward,
                    new Vector2(i / (float)sides, v));
            }
        }

        private static void StitchRings(MeshAccumulator target, int lower, int upper, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                target.AddTriangle(lower + i, upper + i, lower + i + 1);
                target.AddTriangle(lower + i + 1, upper + i, upper + i + 1);
            }
        }

        private static void AddCap(MeshAccumulator target, Vector3 centre, Vector3 tangent,
            Vector3 normal, float radius, bool forward, int sides)
        {
            var facing = forward ? tangent : -tangent;
            var binormal = Vector3.Cross(tangent, normal);

            int centreIndex = target.VertexCount;
            target.AddVertex(centre, facing, new Vector2(0.5f, 0.5f));

            int ring = target.VertexCount;
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                target.AddVertex(centre + (normal * cos + binormal * sin) * radius, facing,
                    new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
            }

            for (int i = 0; i < sides; i++)
                if (forward)
                    target.AddTriangle(centreIndex, ring + i + 1, ring + i);
                else
                    target.AddTriangle(centreIndex, ring + i, ring + i + 1);
        }
    }
}
