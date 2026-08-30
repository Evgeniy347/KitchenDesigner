using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class DimensionFields
    {
        private static readonly Color Locked = new(0.55f, 0.55f, 0.55f, 1f);
        private readonly System.Collections.Generic.Dictionary<TMP_InputField, Color> _normal = new();

        public TMP_InputField? Width { get; set; }

        public TMP_InputField? Height { get; set; }

        public TMP_InputField? Depth { get; set; }

        public void SetEditable(TMP_InputField? field, bool editable)
        {
            if (field == null) return;
            var text = field.textComponent;
            if (text != null && !_normal.ContainsKey(field)) _normal[field] = text.color;
            field.interactable = editable;
            if (text != null) text.color = editable ? _normal[field] : Locked;
        }
    }
}
