using System;
using System.Collections.Generic;
using KitchenDesigner.Core.Update;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class ToastLevelStyle
    {
        private const int Side = 64;
        private const float DiscRadius = 30f;
        private const float StrokeHalf = 3.5f;

        private static readonly Dictionary<StatusLevel, Sprite> Icons = new();

        public static Color ColorFor(StatusLevel level) => level switch
        {
            StatusLevel.Success => UIStyle.TextSuccess,
            StatusLevel.Warning => UIStyle.TextWarning,
            StatusLevel.Error => UIStyle.TextError,
            _ => UIStyle.AccentText,
        };

        public static Sprite IconFor(StatusLevel level)
        {
            if (Icons.TryGetValue(level, out var cached) && cached != null) return cached;
            var sprite = level == StatusLevel.Warning ? IconFactory.Warning : Build(level);
            Icons[level] = sprite;
            return sprite;
        }

        private static Sprite Build(StatusLevel level)
        {
            var px = new Color32[Side * Side];
            for (int y = 0; y < Side; y++)
            for (int x = 0; x < Side; x++)
            {
                float d = GlyphDistance(level, x + 0.5f, y + 0.5f);
                float disc = DiscRadius - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Center);
                float alpha = Mathf.Clamp01(disc + 0.5f) * Mathf.Clamp01(d + 0.5f);
                Color32 ink = UIStyle.NoTint;
                ink.a = (byte)Mathf.RoundToInt(alpha * 255f);
                px[y * Side + x] = ink;
            }

            var tex = new Texture2D(Side, Side, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ToastLevel" + level,
            };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Side, Side), new Vector2(0.5f, 0.5f), Side);
        }

        private static readonly Vector2 Center = new Vector2(Side * 0.5f, Side * 0.5f);

        private static float GlyphDistance(StatusLevel level, float x, float y)
        {
            var p = new Vector2(x, y);
            return level switch
            {
                StatusLevel.Success => Math.Min(SegmentDistance(p, new Vector2(18f, 33f), new Vector2(28f, 23f)),
                    SegmentDistance(p, new Vector2(28f, 23f), new Vector2(47f, 43f))) - StrokeHalf,
                StatusLevel.Error => Math.Min(SegmentDistance(p, new Vector2(21f, 21f), new Vector2(43f, 43f)),
                    SegmentDistance(p, new Vector2(21f, 43f), new Vector2(43f, 21f))) - StrokeHalf,
                _ => Math.Min(Vector2.Distance(p, new Vector2(32f, 46f)) - StrokeHalf - 1f,
                    SegmentDistance(p, new Vector2(32f, 17f), new Vector2(32f, 33f)) - StrokeHalf),
            };
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
