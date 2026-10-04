using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class QuietButton
    {
        public static void Apply(Button button) => Apply(button, UIStyle.SurfaceHover);

        public static void Apply(Button button, Color hover)
        {
            var image = button.targetGraphic as Image;
            if (image != null) image.color = hover;
            var colors = button.colors;
            colors.normalColor = UIStyle.TintHidden;
            colors.selectedColor = UIStyle.TintHidden;
            colors.disabledColor = UIStyle.TintHidden;
            colors.highlightedColor = UIStyle.NoTint;
            colors.pressedColor = UIStyle.TintPressed;
            button.colors = colors;
            if (image != null) image.CrossFadeColor(colors.normalColor, 0f, true, true);

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = UIStyle.TextSecondary;
            foreach (var icon in button.GetComponentsInChildren<Image>(true))
                if (icon != image) icon.color = UIStyle.TextSecondary;
        }
    }
}
