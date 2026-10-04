using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class InputFieldScroll
    {
        public static void RestOnEndEdit(TMP_InputField field)
        {
            field.onEndEdit.AddListener(_ => Rest(field));
        }

        public static void Rest(TMP_InputField field)
        {
            if (field == null || field.textComponent == null) return;
            field.textComponent.rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
