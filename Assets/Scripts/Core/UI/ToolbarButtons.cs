using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal static class ToolbarButtons
    {
        public static Button CreateIcon(Transform parent, string name, string iconName, string tooltip, Action onClick)
        {
            var button = UIFactory.CreateIconButton(name, parent, OutlineIcons.Get(iconName), Vector2.zero,
                new Vector2(ToolbarMetrics.Button, ToolbarMetrics.Button), onClick,
                ToolbarMetrics.Button - UIStyle.IconSize);
            QuietButton.Apply(button);
            SetIconColor(button, UIStyle.Text);
            TooltipUI.Attach(button.gameObject, tooltip);
            return button;
        }

        public static void SetPressed(Button button, bool on)
        {
            if (button.targetGraphic is Image background)
                background.color = on ? UIStyle.SurfaceActive : UIStyle.SurfaceHover;

            var colors = button.colors;
            var rest = on ? UIStyle.NoTint : UIStyle.TintHidden;
            colors.normalColor = rest;
            colors.selectedColor = rest;
            button.colors = colors;
        }

        public static void SetIconColor(Button button, Color color)
        {
            foreach (var icon in button.GetComponentsInChildren<Image>(true))
                if (icon != button.targetGraphic) icon.color = color;
        }

        public static Image? IconOf(Button button)
        {
            foreach (var icon in button.GetComponentsInChildren<Image>(true))
                if (icon != button.targetGraphic) return icon;
            return null;
        }

        public static void PlaceFromLeft(RectTransform rect, float x, float y)
        {
            UIFactory.AnchorTopLeft(rect);
            rect.anchoredPosition = new Vector2(x, y);
        }

        public static RectTransform CreateSeparator(Transform parent)
        {
            var line = UIFactory.CreatePanel("Separator", parent, Vector2.zero,
                new Vector2(ToolbarMetrics.SeparatorW, ToolbarMetrics.SeparatorH), UIStyle.Separator);
            line.raycastTarget = false;
            return line.rectTransform;
        }

        public static float MeasureCaption(Transform parent, string text)
        {
            var probe = UIFactory.CreateLabel("CaptionProbe", parent, text, UIStyle.FontBody,
                Vector2.zero, Vector2.zero);
            float width = probe.GetPreferredValues(text).x;
            UnityEngine.Object.DestroyImmediate(probe.gameObject);
            return width;
        }
    }
}
