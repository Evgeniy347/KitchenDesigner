using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class RoundedRectSprites
    {
        public const int Supersample = 2;
        public const float ReferencePixelsPerUnit = 100f;

        private enum Shape { Fill, Ring, Shadow }

        private static readonly Dictionary<(Shape shape, float radius, float extra), Sprite> Cache = new();

        public static Sprite ControlFill => Fill(UIStyle.RadiusControl);

        public static Sprite ControlStroke => Ring(UIStyle.RadiusControl, UIStyle.DividerPx);

        public static Sprite FocusRing => Ring(UIStyle.RadiusControl, UIStyle.FocusRingPx * 2f);

        public static Sprite WindowFill => Fill(UIStyle.RadiusWindow);

        public static Sprite WindowStroke => Ring(UIStyle.RadiusWindow, UIStyle.DividerPx);

        public static Sprite WindowShadow => Shadow(UIStyle.RadiusWindow, UIStyle.WindowShadowPx);

        public static Sprite Fill(float radius) => Get(Shape.Fill, radius, 0f);

        public static Sprite Ring(float radius, float thickness) => Get(Shape.Ring, radius, thickness);

        public static Sprite Shadow(float radius, float blur) => Get(Shape.Shadow, radius, blur);

        public static void Apply(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.fillCenter = true;
        }

        public static float SliceBorder(Sprite sprite) => sprite.border.x / Supersample;

        private static Sprite Get(Shape shape, float radius, float extra)
        {
            var key = (shape, radius, extra);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var sprite = Build(shape, radius, extra);
            Cache[key] = sprite;
            return sprite;
        }

        private static Sprite Build(Shape shape, float radius, float extra)
        {
            float margin = shape == Shape.Shadow ? extra : 0f;
            int borderRef = (int)Math.Ceiling(radius + margin) + 1;
            int sideRef = borderRef * 2 + 2;
            int side = sideRef * Supersample;
            float inner = (sideRef - margin * 2f) * Supersample;
            float offset = margin * Supersample;
            float r = radius * Supersample;
            float e = extra * Supersample;

            var px = new Color32[side * side];
            Color32 mask = UIStyle.NoTint;
            for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                float cx = x + 0.5f - offset, cy = y + 0.5f - offset;
                float a = shape switch
                {
                    Shape.Ring => RoundedRectMask.Ring(cx, cy, inner, inner, r, e),
                    Shape.Shadow => RoundedRectMask.Shadow(cx, cy, inner, inner, r, e),
                    _ => RoundedRectMask.Fill(cx, cy, inner, inner, r),
                };
                mask.a = (byte)Math.Round(a * 255f);
                px[y * side + x] = mask;
            }

            var tex = new Texture2D(side, side, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Rounded" + shape + radius + "_" + extra,
            };
            tex.SetPixels32(px);
            tex.Apply();

            float b = borderRef * Supersample;
            return Sprite.Create(tex, new Rect(0, 0, side, side), new Vector2(0.5f, 0.5f),
                ReferencePixelsPerUnit * Supersample, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }
    }
}
