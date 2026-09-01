using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class RoundedRectProfile
    {
        public const int DefaultSegments = 16;

        public const float MinPointSpacingRatio = 1e-6f;

        public static float MinPointSpacing(float width, float depth)
            => Mathf.Max(Mathf.Abs(width), Mathf.Abs(depth)) * MinPointSpacingRatio;

        public static Vector2[] Uniform(float width, float depth, float radius, int segments)
            => Build(width, depth, CornerRadii.Uniform(radius), segments);

        public static Vector2[] Build(float width, float depth,
            float radiusMinusXMinusZ, float radiusPlusXMinusZ,
            float radiusPlusXPlusZ, float radiusMinusXPlusZ, int segments)
            => Build(width, depth, new CornerRadii(radiusMinusXMinusZ, radiusPlusXMinusZ,
                radiusPlusXPlusZ, radiusMinusXPlusZ), segments);

        public static Vector2[] Build(float width, float depth, CornerRadii radii, int segments)
        {
            float w = Mathf.Max(float.Epsilon, width);
            float d = Mathf.Max(float.Epsilon, depth);
            float spacing = MinPointSpacing(w, d);
            int arcSegments = Mathf.Max(1, segments);

            var fitted = Fit(w, d, radii);

            float halfW = w * 0.5f;
            float halfD = d * 0.5f;
            var centres = new[]
            {
                new Vector2(-halfW + fitted[0], -halfD + fitted[0]),
                new Vector2(halfW - fitted[1], -halfD + fitted[1]),
                new Vector2(halfW - fitted[2], halfD - fitted[2]),
                new Vector2(-halfW + fitted[3], halfD - fitted[3]),
            };
            var startAngles = new[] { Mathf.PI, -Mathf.PI * 0.5f, 0f, Mathf.PI * 0.5f };

            var points = new List<Vector2>();
            for (int corner = 0; corner < CornerRadii.Count; corner++)
            {
                float r = fitted[corner];
                if (r <= 0f)
                {
                    Append(points, centres[corner], spacing);
                    continue;
                }

                for (int i = 0; i <= arcSegments; i++)
                {
                    float angle = startAngles[corner] + Mathf.PI * 0.5f * i / arcSegments;
                    Append(points, new Vector2(
                        centres[corner].x + Mathf.Cos(angle) * r,
                        centres[corner].y + Mathf.Sin(angle) * r), spacing);
                }
            }

            while (points.Count > 3 && Coincide(points[0], points[points.Count - 1], spacing))
                points.RemoveAt(points.Count - 1);

            return points.ToArray();
        }

        public static float SignedDistance(Vector2 point, float width, float depth, float radius)
            => SignedDistance(point, width, depth, CornerRadii.Uniform(radius));

        public static float SignedDistance(Vector2 point, float width, float depth,
            CornerRadii asked)
        {
            float w = Mathf.Max(float.Epsilon, width);
            float d = Mathf.Max(float.Epsilon, depth);
            float halfW = w * 0.5f;
            float halfD = d * 0.5f;
            var radii = Fit(w, d, asked);

            for (int corner = 0; corner < CornerRadii.Count; corner++)
            {
                float r = radii[corner];
                float qx = TowardsX(corner) * point.x - (halfW - r);
                float qy = TowardsZ(corner) * point.y - (halfD - r);
                if (qx > 0f && qy > 0f) return new Vector2(qx, qy).magnitude - r;
            }

            return Mathf.Max(Mathf.Abs(point.x) - halfW, Mathf.Abs(point.y) - halfD);
        }

        public static CornerRadii Fit(float width, float depth, CornerRadii asked)
        {
            float r0 = Mathf.Max(0f, asked.MinusXMinusZ);
            float r1 = Mathf.Max(0f, asked.PlusXMinusZ);
            float r2 = Mathf.Max(0f, asked.PlusXPlusZ);
            float r3 = Mathf.Max(0f, asked.MinusXPlusZ);

            float fit = 1f;
            fit = Mathf.Min(fit, EdgeFit(width, r0, r1));
            fit = Mathf.Min(fit, EdgeFit(depth, r1, r2));
            fit = Mathf.Min(fit, EdgeFit(width, r2, r3));
            fit = Mathf.Min(fit, EdgeFit(depth, r3, r0));

            return new CornerRadii(r0 * fit, r1 * fit, r2 * fit, r3 * fit);
        }

        public static float SignedArea(Vector2[] points)
        {
            if (points == null || points.Length < 3) return 0f;

            float sum = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                sum += a.x * b.y - b.x * a.y;
            }

            return sum * 0.5f;
        }

        private static float TowardsX(int corner) => corner == 1 || corner == 2 ? 1f : -1f;

        private static float TowardsZ(int corner) => corner >= 2 ? 1f : -1f;

        private static float EdgeFit(float edgeLength, float first, float second)
            => first + second <= edgeLength ? 1f : edgeLength / (first + second);

        private static void Append(List<Vector2> points, Vector2 point, float spacing)
        {
            if (points.Count > 0 && Coincide(points[points.Count - 1], point, spacing)) return;
            points.Add(point);
        }

        private static bool Coincide(Vector2 a, Vector2 b, float spacing)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            return dx * dx + dy * dy < spacing * spacing;
        }
    }
}
