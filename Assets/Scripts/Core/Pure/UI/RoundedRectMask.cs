using System;

namespace KitchenDesigner.Core.UI
{
    public static class RoundedRectMask
    {
        public static float SignedDistance(float x, float y, float width, float height, float radius)
        {
            float r = Math.Max(0f, Math.Min(radius, Math.Min(width, height) * 0.5f));
            float qx = Math.Abs(x - width * 0.5f) - (width * 0.5f - r);
            float qy = Math.Abs(y - height * 0.5f) - (height * 0.5f - r);
            float outside = (float)Math.Sqrt(Sqr(Math.Max(qx, 0f)) + Sqr(Math.Max(qy, 0f)));
            float inside = Math.Min(Math.Max(qx, qy), 0f);
            return outside + inside - r;
        }

        public static float Fill(float x, float y, float width, float height, float radius) =>
            Coverage(SignedDistance(x, y, width, height, radius));

        public static float Ring(float x, float y, float width, float height, float radius, float thickness)
        {
            float d = SignedDistance(x, y, width, height, radius);
            return Math.Max(0f, Coverage(d) - Coverage(d + thickness));
        }

        public static float Shadow(float x, float y, float width, float height, float radius, float blur)
        {
            if (blur <= 0f) return Fill(x, y, width, height, radius);
            float d = SignedDistance(x, y, width, height, radius);
            float t = Clamp01(0.5f - d / blur);
            return t * t * (3f - 2f * t);
        }

        private static float Coverage(float signedDistance) => Clamp01(0.5f - signedDistance);

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static float Sqr(float v) => v * v;
    }
}
