using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class RowBadge
    {
        public const string NodePrefix = "Badge_";

        public static RectTransform Create(RectTransform row, string key, string text, Color textColor, Color fill)
        {
            var rect = UIFactory.CreateRect(NodePrefix + key, row);
            var image = rect.gameObject.AddComponent<Image>();
            RoundedRectSprites.Apply(image, RoundedRectSprites.Fill(UIStyle.BadgeRadius));
            image.color = fill;
            image.raycastTarget = false;

            var label = UIFactory.CreateLabel("Text", rect, text, UIStyle.FontCaption, Vector2.zero,
                new Vector2(0f, UIStyle.BadgeH), TextAnchor.MiddleCenter);
            label.color = textColor;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;

            float width = Mathf.Ceil(label.GetPreferredValues(text).x) + 2f * UIStyle.BadgePadX;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, UIStyle.BadgeH);
            return rect;
        }
    }
}
