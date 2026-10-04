using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class InputFieldPlaceholder
    {
        public const string NodeName = "Placeholder";

        public static TMP_Text Attach(TMP_InputField field, string text)
        {
            var source = field.textComponent!;
            var label = UIFactory.CreateLabel(NodeName, field.textViewport, text, (int)source.fontSize,
                Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            label.color = UIStyle.TextDisabled;
            label.alignment = source.alignment;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            field.placeholder = label;
            field.ForceLabelUpdate();
            return label;
        }
    }
}
