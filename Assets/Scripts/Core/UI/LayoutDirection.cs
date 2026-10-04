using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class LayoutDirection
    {
        public static bool IsRtl => Loc.IsRightToLeft;

        public static string CollapsedGlyph => IsRtl ? UIStyle.GlyphCollapsedRtl : UIStyle.GlyphCollapsed;

        public static float StartX(float span, float x, float width) => IsRtl ? span - x - width : x;

        public static void PinTopEnd(RectTransform rt, float fromEnd, float fromTop)
        {
            bool rtl = IsRtl;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(rtl ? 0f : 1f, 1f);
            rt.anchoredPosition = new Vector2(rtl ? fromEnd : -fromEnd, -fromTop);
        }

        public static void PinToStartEdge(RectTransform rt, float width)
        {
            float side = IsRtl ? 1f : 0f;
            rt.anchorMin = new Vector2(side, 0f);
            rt.anchorMax = new Vector2(side, 1f);
            rt.pivot = new Vector2(side, 0.5f);
            rt.sizeDelta = new Vector2(width, 0f);
            rt.anchoredPosition = Vector2.zero;
        }

        public static TextAnchor Mirror(TextAnchor align) => align switch
        {
            TextAnchor.UpperLeft => TextAnchor.UpperRight,
            TextAnchor.UpperRight => TextAnchor.UpperLeft,
            TextAnchor.MiddleLeft => TextAnchor.MiddleRight,
            TextAnchor.MiddleRight => TextAnchor.MiddleLeft,
            TextAnchor.LowerLeft => TextAnchor.LowerRight,
            TextAnchor.LowerRight => TextAnchor.LowerLeft,
            _ => align,
        };
    }
}
