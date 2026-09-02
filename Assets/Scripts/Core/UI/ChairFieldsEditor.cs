namespace KitchenDesigner.Core.UI
{
    internal sealed class ChairFieldsEditor : NumberFieldsEditor
    {
        public const string CornerRadiusNode = "СкруглениеСтула";
        public const string SeatHeightNode = "ВысотаСиденья";

        public ChairFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is ChairElement;

        public override void Build()
        {
            var visibility = RowVisibility.For(ElementFacet.Chair);
            Bind(Rows.NumberField("Скругление", visibility, "мм", CornerRadiusNode),
                RadiusOf, SetRadius, "0");
            Bind(Rows.NumberField("Высота сиденья", visibility, "мм", SeatHeightNode),
                SeatHeightOf, SetSeatHeight, "0");
        }

        private static int RadiusOf(KitchenElement element) =>
            element is ChairElement chair ? chair.CornerRadiusMM : 0;

        private static void SetRadius(KitchenElement element, int value)
        {
            if (element is ChairElement chair) chair.CornerRadiusMM = value;
        }

        private static int SeatHeightOf(KitchenElement element) =>
            element is ChairElement chair ? chair.SeatHeightMM : 0;

        private static void SetSeatHeight(KitchenElement element, int value)
        {
            if (element is ChairElement chair) chair.SeatHeightMM = value;
        }
    }
}
