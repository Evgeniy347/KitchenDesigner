namespace KitchenDesigner.Core.UI
{
    internal sealed class SofaFieldsEditor : NumberFieldsEditor
    {
        public const string CornerRadiusNode = "СкруглениеДивана";
        public const string SeatHeightNode = "ВысотаОснованияДивана";

        public SofaFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is SofaElement;

        public override void Build()
        {
            var visibility = RowVisibility.For(ElementFacet.Sofa);
            Bind(Rows.NumberField("Скругление", visibility, "мм", CornerRadiusNode),
                RadiusOf, SetRadius, "0");
            Bind(Rows.NumberField("Высота основания", visibility, "мм", SeatHeightNode),
                SeatHeightOf, SetSeatHeight, "0");
        }

        private static int RadiusOf(KitchenElement element) =>
            element is SofaElement sofa ? sofa.CornerRadiusMM : 0;

        private static void SetRadius(KitchenElement element, int value)
        {
            if (element is SofaElement sofa) sofa.CornerRadiusMM = value;
        }

        private static int SeatHeightOf(KitchenElement element) =>
            element is SofaElement sofa ? sofa.SeatHeightMM : 0;

        private static void SetSeatHeight(KitchenElement element, int value)
        {
            if (element is SofaElement sofa) sofa.SeatHeightMM = value;
        }
    }
}
