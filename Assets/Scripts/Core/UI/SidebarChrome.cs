using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal static class SidebarChrome
    {
        public const string HeaderCountNode = "Count";
        public const string HeaderTitleNode = "Title";
        public const string TileRimNode = "Rim";

        public static void AddRightRule(RectTransform panel)
        {
            var rule = UIFactory.CreatePanel("RightRule", panel, Vector2.zero, Vector2.zero, UIStyle.Divider);
            rule.raycastTarget = false;
            var rect = rule.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(UIStyle.DividerPx, 0f);
            rect.anchoredPosition = Vector2.zero;
        }

        public static Button CreateQuietIcon(Transform parent, string name, string iconName, System.Action onClick)
        {
            var button = UIFactory.CreateIconButton(name, parent, OutlineIcons.Get(iconName), Vector2.zero,
                new Vector2(SidebarLayout.ControlH, SidebarLayout.ControlH), onClick,
                SidebarLayout.ControlH - UIStyle.IconSize);
            QuietButton.Apply(button);
            return button;
        }

        public static Button CreateQuietGlyph(Transform parent, string name, string glyph, System.Action onClick)
        {
            var button = UIFactory.CreateButton(name, parent, glyph, Vector2.zero,
                new Vector2(SidebarLayout.ControlH, SidebarLayout.ControlH), onClick);
            QuietButton.Apply(button);
            return button;
        }

        public static void PlaceTopLeft(RectTransform rect, float x, float y)
        {
            UIFactory.AnchorTopLeft(rect);
            rect.anchoredPosition = new Vector2(x, y);
        }

        public static TMP_Text AddHeaderCount(RectTransform header)
        {
            var count = UIFactory.CreateLabel(HeaderCountNode, header, string.Empty, UIStyle.FontCaption,
                Vector2.zero, Vector2.zero, TextAnchor.MiddleRight);
            count.color = UIStyle.TextSecondary;
            count.raycastTarget = false;
            count.enableWordWrapping = false;
            var rect = count.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(SidebarLayout.Pad, 0f);
            rect.offsetMax = new Vector2(-SidebarLayout.Pad, 0f);
            return count;
        }

        public static TMP_Text AddHeaderTitle(RectTransform header, string title)
        {
            var label = UIFactory.CreateLabel(HeaderTitleNode, header, title, UIStyle.FontBody,
                Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft);
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(SidebarLayout.Pad + UIStyle.ChevronW + UIStyle.Space2, 0f);
            rect.offsetMax = new Vector2(-(SidebarLayout.Pad + UIStyle.Space6 + UIStyle.Space2), 0f);
            return label;
        }

        public static void StyleHeaderGlyph(TMP_Text glyph)
        {
            glyph.alignment = TextAlignmentOptions.Left;
            glyph.fontSize = UIStyle.FontCaption;
            glyph.color = UIStyle.TextSecondary;
            glyph.enableWordWrapping = false;
            glyph.raycastTarget = false;
            var rect = glyph.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(SidebarLayout.Pad, 0f);
            rect.offsetMax = new Vector2(-SidebarLayout.Pad, 0f);
        }

        public static Image AddTileRim(RectTransform tile)
        {
            var rect = UIFactory.CreateRect(TileRimNode, tile);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var rim = rect.gameObject.AddComponent<Image>();
            RoundedRectSprites.Apply(rim, RoundedRectSprites.ControlStroke);
            rim.color = UIStyle.FieldStroke;
            rim.raycastTarget = false;
            rim.gameObject.SetActive(false);
            return rim;
        }
    }
}
