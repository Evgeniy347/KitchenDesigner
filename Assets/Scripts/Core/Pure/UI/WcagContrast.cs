using System;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class WcagContrast
    {
        public const float TextAA = 4.5f;
        public const float NonTextAA = 3f;
        public const float SrgbLinearKnee = 0.03928f;

        public static float Ratio(Color foreground, Color background)
        {
            var bg = Opaque(background);
            var fg = Over(foreground, bg);
            float a = Luminance(fg), b = Luminance(bg);
            return (Math.Max(a, b) + 0.05f) / (Math.Min(a, b) + 0.05f);
        }

        public static Color Over(Color top, Color opaqueBottom) => new Color(
            top.r * top.a + opaqueBottom.r * (1f - top.a),
            top.g * top.a + opaqueBottom.g * (1f - top.a),
            top.b * top.a + opaqueBottom.b * (1f - top.a),
            1f);

        public static float Luminance(Color c) =>
            0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);

        private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        private static float Linear(float channel) =>
            channel <= SrgbLinearKnee ? channel / 12.92f : (float)Math.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }
}
