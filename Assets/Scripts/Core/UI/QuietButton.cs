using TMPro;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class QuietButton
    {
        public static void Apply(Button button)
        {
            var image = button.targetGraphic as Image;
            if (image != null) image.color = UIStyle.SurfaceHover;
            var colors = button.colors;
            colors.normalColor = UIStyle.TintHidden;
            colors.selectedColor = UIStyle.TintHidden;
            colors.disabledColor = UIStyle.TintHidden;
            colors.highlightedColor = UIStyle.NoTint;
            colors.pressedColor = UIStyle.TintPressed;
            button.colors = colors;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = UIStyle.TextSecondary;
            foreach (var icon in button.GetComponentsInChildren<Image>(true))
                if (icon != image) icon.color = UIStyle.TextSecondary;
        }
    }
}
