using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class PouffeFieldsEditor : ElementFieldsEditor
    {
        public const string CornerRadiusNode = "СкруглениеПуфика";
        public const string SeatThicknessNode = "СидушкаПуфика";
        public const string CornerRadiusLabel = "Скругление";
        public const string SeatThicknessLabel = "Толщина сидушки";

        private TMP_InputField? _cornerRadius;
        private TMP_InputField? _seatThickness;

        public PouffeFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is PouffeElement;

        public override void Build()
        {
            var visibility = RowVisibility.For(ElementFacet.Pouffe);
            _cornerRadius = Rows.NumberField(CornerRadiusLabel, visibility, "мм",
                CornerRadiusNode);
            _seatThickness = Rows.NumberField(SeatThicknessLabel, visibility, "мм",
                SeatThicknessNode);
        }

        public override System.Collections.Generic.IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _cornerRadius;
            yield return _seatThickness;
        }

        public override void Show(KitchenElement element)
        {
            if (!Handles(element)) return;
            if (_cornerRadius != null) _cornerRadius.text = RadiusOf(element).ToString();
            if (_seatThickness != null) _seatThickness.text = ThicknessOf(element).ToString();
        }

        public override void Refresh(KitchenElement element)
        {
            if (!Handles(element)) return;
            Fields.RefreshUnfocused(_cornerRadius, RadiusOf(element).ToString());
            Fields.RefreshUnfocused(_seatThickness, ThicknessOf(element).ToString());
        }

        public override void Apply(KitchenElement element)
        {
            if (element is not PouffeElement pouffe) return;
            pouffe.SeatThicknessMM = Fields.ParseInt(_seatThickness, pouffe.SeatThicknessMM);
            pouffe.CornerRadiusMM = Fields.ParseInt(_cornerRadius, pouffe.CornerRadiusMM);
        }

        public override void AfterApply(KitchenElement element)
        {
            if (!Handles(element)) return;
            if (_cornerRadius != null) _cornerRadius.text = RadiusOf(element).ToString();
            if (_seatThickness != null) _seatThickness.text = ThicknessOf(element).ToString();
        }

        public override void Track(KitchenElement element)
        {
            bool mine = Handles(element);
            Fields.Track(_cornerRadius, mine ? RadiusOf(element).ToString() : "0");
            Fields.Track(_seatThickness, mine
                ? ThicknessOf(element).ToString()
                : PouffeLayout.DefaultSeatThicknessMM.ToString());
        }

        private static int RadiusOf(KitchenElement element) =>
            element is PouffeElement pouffe ? pouffe.CornerRadiusMM : 0;

        private static int ThicknessOf(KitchenElement element) =>
            element is PouffeElement pouffe
                ? pouffe.SeatThicknessMM
                : PouffeLayout.DefaultSeatThicknessMM;
    }
}
