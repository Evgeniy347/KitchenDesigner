using TMPro;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class ButtonRoles
    {
        public static void Paint(Button button, ButtonRole role)
        {
            var image = button.targetGraphic as Image;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            switch (role)
            {
                case ButtonRole.Primary:
                    if (image != null) image.color = UIStyle.Accent;
                    if (label != null) label.color = UIStyle.TextOnAccent;
                    break;
                case ButtonRole.Danger:
                    if (image != null) image.color = UIStyle.Danger;
                    if (label != null) label.color = UIStyle.TextOnAccent;
                    break;
                case ButtonRole.Link:
                    QuietButton.Apply(button);
                    if (label != null) label.color = UIStyle.AccentText;
                    break;
                default:
                    if (image != null) image.color = UIStyle.Surface;
                    if (label != null) label.color = UIStyle.Text;
                    break;
            }
        }
    }
}
