using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class SofaFieldsEditor : ElementFieldsEditor
    {
        public const string CornerRadiusNode = "СкруглениеДивана";
        public const string SeatHeightNode = "ВысотаОснованияДивана";

        private TMP_InputField? _cornerRadius;
        private TMP_InputField? _seatHeight;

        public SofaFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is SofaElement;

        public override void Build()
        {
            _cornerRadius = Rows.NumberField("Скругление",
                RowVisibility.For(ElementFacet.Sofa), "мм", CornerRadiusNode);
            _seatHeight = Rows.NumberField("Высота основания",
                RowVisibility.For(ElementFacet.Sofa), "мм", SeatHeightNode);
        }

        public override System.Collections.Generic.IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _cornerRadius;
            yield return _seatHeight;
        }

        public override void Show(KitchenElement element)
        {
            if (!Handles(element)) return;
            if (_cornerRadius != null) _cornerRadius.text = RadiusOf(element).ToString();
            if (_seatHeight != null) _seatHeight.text = SeatHeightOf(element).ToString();
        }

        public override void Refresh(KitchenElement element)
        {
            if (!Handles(element)) return;
            Fields.RefreshUnfocused(_cornerRadius, RadiusOf(element).ToString());
            Fields.RefreshUnfocused(_seatHeight, SeatHeightOf(element).ToString());
        }

        public override void Apply(KitchenElement element)
        {
            if (element is not SofaElement sofa) return;
            sofa.CornerRadiusMM = Fields.ParseInt(_cornerRadius, sofa.CornerRadiusMM);
            sofa.SeatHeightMM = Fields.ParseInt(_seatHeight, sofa.SeatHeightMM);
        }

        public override void AfterApply(KitchenElement element)
        {
            if (!Handles(element)) return;
            if (_cornerRadius != null) _cornerRadius.text = RadiusOf(element).ToString();
            if (_seatHeight != null) _seatHeight.text = SeatHeightOf(element).ToString();
        }

        public override void Track(KitchenElement element)
        {
            bool mine = Handles(element);
            Fields.Track(_cornerRadius, mine ? RadiusOf(element).ToString() : "0");
            Fields.Track(_seatHeight, mine ? SeatHeightOf(element).ToString() : "0");
        }

        private static int RadiusOf(KitchenElement element) =>
            element is SofaElement sofa ? sofa.CornerRadiusMM : 0;

        private static int SeatHeightOf(KitchenElement element) =>
            element is SofaElement sofa ? sofa.SeatHeightMM : 0;
    }
}
