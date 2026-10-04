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
        public const float DarkLift = 0.4f;
        public const float DarkGlowShare = 0.35f;

        public static Color TintOf(bool multi) => multi ? MultiTint : SingleTint;

        public static float GlowOf(bool multi) => multi ? MultiGlow : SingleGlow;

        public static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        public static Color Base(Color own, bool multi)
        {
            var tint = TintOf(multi);
            var product = new Color(own.r * tint.r, own.g * tint.g, own.b * tint.b, own.a);
            float lift = DarkLift * (1f - Mathf.Clamp01(Luma(own)));
            return new Color(
                Mathf.Lerp(product.r, tint.r, lift),
                Mathf.Lerp(product.g, tint.g, lift),
                Mathf.Lerp(product.b, tint.b, lift),
                own.a);
        }

        public static Color Emission(Color own, bool multi)
        {
            float share = Mathf.Lerp(DarkGlowShare, 1f, Mathf.Clamp01(Luma(own)));
            return GlowTint * (GlowOf(multi) * share);
        }
    }
}
