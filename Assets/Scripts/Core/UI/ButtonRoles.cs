using TMPro;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class ButtonRoles
    {
        public const string OutlineNode = "Outline";

        public static void Paint(Button button, ButtonRole role)
        {
            var image = button.targetGraphic as Image;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            var outline = button.transform.Find(OutlineNode);
            if (outline != null) outline.gameObject.SetActive(role == ButtonRole.DangerOutline);
            if (role != ButtonRole.Link && role != ButtonRole.DangerOutline) button.colors = UIFactory.InteractiveColors();

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
                case ButtonRole.DangerOutline:
                    QuietButton.Apply(button);
                    if (label != null) label.color = UIStyle.DangerText;
                    if (outline == null)
                    {
                        var rim = UIFactory.AddFieldStroke((UnityEngine.RectTransform)button.transform);
                        rim.name = OutlineNode;
                        rim.color = UIStyle.Danger;
                    }
                    break;
                default:
                    if (image != null) image.color = UIStyle.Surface;
                    if (label != null) label.color = UIStyle.Text;
                    break;
            }
        }
    }
}
