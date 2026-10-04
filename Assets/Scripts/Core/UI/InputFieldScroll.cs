using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class InputFieldScroll
    {
        public static void Rest(TMP_InputField field)
        {
            if (field == null || field.textComponent == null) return;
            var rect = field.textComponent.rectTransform;
            if (rect.anchoredPosition != Vector2.zero) rect.anchoredPosition = Vector2.zero;
        }
    }
}
