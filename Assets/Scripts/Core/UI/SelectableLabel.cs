using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class SelectableLabel
    {
        public static TMP_InputField Create(string name, Transform parent, string text, int fontSize,
            Vector2 anchoredPos, Vector2 size, Color color)
        {
            var input = UIFactory.CreateInputField(name, parent, text, anchoredPos, size);
            input.readOnly = true;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.GetComponent<Image>().color = UIStyle.RaycastOnly;

            var caption = input.textComponent;
            caption.fontSize = fontSize;
            caption.color = color;
            caption.alignment = TextAlignmentOptions.Center;
            return input;
        }
    }
}
