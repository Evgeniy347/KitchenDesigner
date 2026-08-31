using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class StoolFieldsEditor : ElementFieldsEditor
    {
        private TMP_InputField? _cornerRadius;

        public StoolFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is StoolElement;

        public override void Build() =>
            _cornerRadius = Rows.NumberField("Скругление", RowVisibility.For(ElementFacet.Stool));

        public override System.Collections.Generic.IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _cornerRadius;
        }

        public override void Show(KitchenElement element)
        {
            if (_cornerRadius == null || !Handles(element)) return;
            _cornerRadius.text = RadiusOf(element).ToString();
        }

        public override void Refresh(KitchenElement element)
        {
            if (Handles(element))
                Fields.RefreshUnfocused(_cornerRadius, RadiusOf(element).ToString());
        }

        public override void Apply(KitchenElement element)
        {
            if (element is StoolElement stool)
                stool.CornerRadiusMM = Fields.ParseInt(_cornerRadius, stool.CornerRadiusMM);
        }

        public override void AfterApply(KitchenElement element)
        {
            if (_cornerRadius != null && Handles(element))
                _cornerRadius.text = RadiusOf(element).ToString();
        }

        public override void Track(KitchenElement element) =>
            Fields.Track(_cornerRadius, Handles(element) ? RadiusOf(element).ToString() : "0");

        private static int RadiusOf(KitchenElement element) =>
            element is StoolElement stool ? stool.CornerRadiusMM : 0;
    }
}
