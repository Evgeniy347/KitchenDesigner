using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SelectionTintMath
    {
        public static readonly Color SingleTint = new Color(1f, 0.95f, 0.6f, 1f);
        public static readonly Color MultiTint = new Color(1f, 0.97f, 0.7f, 1f);
        public static readonly Color GlowTint = new Color(0.8f, 0.7f, 0.1f, 1f);

        public const float SingleGlow = 0.5f;
        public const float MultiGlow = 0.3f;
        public const float DarkLift = 0.1f;
        public const float DarkGlowShare = 0.07f;
        public const float MinShift = 0.08f;
        public const float ShiftStep = 0.02f;

        public static Color TintOf(bool multi) => multi ? MultiTint : SingleTint;

        public static float GlowOf(bool multi) => multi ? MultiGlow : SingleGlow;

        public static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        public static float MaxShift(Color a, Color b) =>
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

        public static Color Base(Color own, bool multi)
        {
            var tint = TintOf(multi);
            var product = new Color(own.r * tint.r, own.g * tint.g, own.b * tint.b, own.a);
            float lift = DarkLift * (1f - Mathf.Clamp01(Luma(own)));
            var shifted = Mix(product, tint, lift, own.a);

            for (float pull = ShiftStep; pull <= 1f && MaxShift(own, shifted) < MinShift; pull += ShiftStep)
                shifted = Mix(Mix(product, tint, lift, own.a), tint, pull, own.a);
            return shifted;
        }

        public static Color Emission(Color own, bool multi)
        {
            float share = Mathf.Lerp(DarkGlowShare, 1f, Mathf.Clamp01(Luma(own)));
            return GlowTint * (GlowOf(multi) * share);
        }

        private static Color Mix(Color from, Color to, float t, float alpha) => new Color(
            Mathf.Lerp(from.r, to.r, t),
            Mathf.Lerp(from.g, to.g, t),
            Mathf.Lerp(from.b, to.b, t),
            alpha);
    }
}
