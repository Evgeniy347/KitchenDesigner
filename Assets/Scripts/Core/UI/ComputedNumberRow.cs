using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ComputedNumberRow
    {
        private readonly TMP_Text _value;
        private readonly TMP_Text _computedLabel;
        private readonly string _unit;
        private string _shown = "";

        internal ComputedNumberRow(TMP_Text label, TMP_InputField field, TMP_Text computedLabel,
            TMP_Text value, string unit)
        {
            Label = label;
            Field = field;
            _computedLabel = computedLabel;
            _value = value;
            _unit = unit;
        }

        public TMP_Text Label { get; }

        public TMP_InputField Field { get; }

        public bool Locked => !UIRowEnabled.IsEnabled(Field);

        public void SetCaption(string caption)
        {
            Label.text = caption;
            _computedLabel.text = caption;
        }

        public void Sync()
        {
            if (!Locked || _shown == Field.text) return;
            _shown = Field.text;
            _value.text = NumberFormat.WithUnit(_shown, _unit);
        }
    }
}
