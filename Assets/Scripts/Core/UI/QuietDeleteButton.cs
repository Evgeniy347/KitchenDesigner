using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class QuietDeleteButton
    {
        public static Button Create(string name, Transform parent, float size, string tooltip, Action onConfirm)
        {
            var button = UIFactory.CreateButton(name, parent, UIStyle.GlyphClose, Vector2.zero,
                new Vector2(size, size), null);
            QuietButton.Apply(button);
            TooltipUI.Attach(button.gameObject, tooltip);
            var confirm = ConfirmDeleteButton.Attach(button, onConfirm);
            confirm.ArmedChanged += armed => Paint(button, armed);
            return button;
        }

        public static void SetInteractable(Button button, bool interactable)
        {
            button.interactable = interactable;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = interactable ? UIStyle.TextSecondary : UIStyle.TextDisabled;
        }

        private static void Paint(Button button, bool armed)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (!armed)
            {
                QuietButton.Apply(button);
                if (label != null) label.fontSize = UIStyle.FontBody;
                return;
            }

            button.colors = UIFactory.InteractiveColors();
            if (button.targetGraphic is Image image)
            {
                image.color = UIStyle.Danger;
                image.CrossFadeColor(button.colors.normalColor, 0f, true, true);
            }
            if (label == null) return;
            label.color = UIStyle.TextOnAccent;
            label.fontSize = UIStyle.FontCaption;
        }
    }
}
