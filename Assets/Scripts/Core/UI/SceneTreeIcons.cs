using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class SceneTreeIcons
    {
        private const int Size = 64;
        private const int Stroke = 7;
        private const int Corner = 10;
        private const int DashLength = 12;
        private const int DashGap = 8;
        private const int PlusArm = 22;
        private const int PlusStroke = 7;
        private const int DotRadius = 6;
        private const int DotSpacing = 20;

        private static Sprite? _element, _group, _plus, _dots;

        public static Sprite Element => _element ??= Build(DrawOutline(dashed: false));

        public static Sprite Group => _group ??= Build(DrawOutline(dashed: true));

        public static Sprite Plus => _plus ??= Build(DrawPlus());

        public static Sprite Dots => _dots ??= Build(DrawDots());

        private static Color32[] DrawOutline(bool dashed)
        {
            var px = new Color32[Size * Size];
            int lo = 3, hi = Size - 4;
            for (int y = lo; y <= hi; y++)
                for (int x = lo; x <= hi; x++)
                {
                    if (!OnRim(x, y, lo, hi)) continue;
                    if (dashed && !InDash(x, y, lo, hi)) continue;
                    px[y * Size + x] = UIStyle.NoTint;
                }
            return px;
        }

        private static bool OnRim(int x, int y, int lo, int hi)
        {
            bool inner = x >= lo + Stroke && x <= hi - Stroke && y >= lo + Stroke && y <= hi - Stroke;
            if (inner) return false;
            int cx = x < lo + Corner ? lo + Corner : x > hi - Corner ? hi - Corner : x;
            int cy = y < lo + Corner ? lo + Corner : y > hi - Corner ? hi - Corner : y;
            int dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= Corner * Corner;
        }

        private static bool InDash(int x, int y, int lo, int hi)
        {
            bool horizontal = Mathf.Min(y - lo, hi - y) < Mathf.Min(x - lo, hi - x);
            int along = horizontal ? x : y;
            return (along - lo) % (DashLength + DashGap) < DashLength;
        }

        private static Color32[] DrawPlus()
        {
            var px = new Color32[Size * Size];
            int c = Size / 2;
            Fill(px, c - PlusArm, c - PlusStroke / 2, c + PlusArm, c + PlusStroke / 2 + 1);
            Fill(px, c - PlusStroke / 2, c - PlusArm, c + PlusStroke / 2 + 1, c + PlusArm);
            return px;
        }

        private static Color32[] DrawDots()
        {
            var px = new Color32[Size * Size];
            int c = Size / 2;
            for (int i = -1; i <= 1; i++) Disc(px, c + i * DotSpacing, c, DotRadius);
            return px;
        }

        private static void Fill(Color32[] px, int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    px[y * Size + x] = UIStyle.NoTint;
        }

        private static void Disc(Color32[] px, int cx, int cy, int r)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) px[y * Size + x] = UIStyle.NoTint;
        }

        private static Sprite Build(Color32[] px)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
