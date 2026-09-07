using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class DimensionFields
    {
        public TMP_InputField? Width { get; set; }

        public TMP_InputField? Height { get; set; }

        public TMP_InputField? Depth { get; set; }

        public void SetEditable(TMP_InputField? field, bool editable) =>
            UIRowEnabled.SetControlEnabled(field, editable);
    }
}
