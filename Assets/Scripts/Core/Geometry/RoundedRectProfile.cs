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
            => Build(width, depth, radius, radius, radius, radius, segments);

        public static Vector2[] Build(float width, float depth,
            float radiusMinusXMinusZ, float radiusPlusXMinusZ,
            float radiusPlusXPlusZ, float radiusMinusXPlusZ, int segments)
        {
            float w = Mathf.Max(float.Epsilon, width);
            float d = Mathf.Max(float.Epsilon, depth);
            float spacing = MinPointSpacing(w, d);
            int arcSegments = Mathf.Max(1, segments);

            var radii = Fit(w, d,
                radiusMinusXMinusZ, radiusPlusXMinusZ, radiusPlusXPlusZ, radiusMinusXPlusZ);

            float halfW = w * 0.5f;
            float halfD = d * 0.5f;
            var centres = new[]
            {
                new Vector2(-halfW + radii[0], -halfD + radii[0]),
                new Vector2(halfW - radii[1], -halfD + radii[1]),
                new Vector2(halfW - radii[2], halfD - radii[2]),
                new Vector2(-halfW + radii[3], halfD - radii[3]),
            };
            var startAngles = new[] { Mathf.PI, -Mathf.PI * 0.5f, 0f, Mathf.PI * 0.5f };

            var points = new List<Vector2>();
            for (int corner = 0; corner < 4; corner++)
            {
                float r = radii[corner];
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

        private static float[] Fit(float width, float depth,
            float rMinusXMinusZ, float rPlusXMinusZ, float rPlusXPlusZ, float rMinusXPlusZ)
        {
            var radii = new[]
            {
                Mathf.Max(0f, rMinusXMinusZ),
                Mathf.Max(0f, rPlusXMinusZ),
                Mathf.Max(0f, rPlusXPlusZ),
                Mathf.Max(0f, rMinusXPlusZ),
            };

            float fit = 1f;
            fit = Mathf.Min(fit, EdgeFit(width, radii[0], radii[1]));
            fit = Mathf.Min(fit, EdgeFit(depth, radii[1], radii[2]));
            fit = Mathf.Min(fit, EdgeFit(width, radii[2], radii[3]));
            fit = Mathf.Min(fit, EdgeFit(depth, radii[3], radii[0]));

            if (fit < 1f)
                for (int i = 0; i < radii.Length; i++)
                    radii[i] *= fit;

            return radii;
        }

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
