namespace KitchenDesigner.Core.UI
{
    internal sealed class PouffeFieldsEditor : NumberFieldsEditor
    {
        public const string CornerRadiusNode = "СкруглениеПуфика";
        public const string SeatThicknessNode = "СидушкаПуфика";
        public const string CornerRadiusLabel = "Скругление";
        public const string SeatThicknessLabel = "Толщина сидушки";

        public PouffeFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is PouffeElement;

        public override void Build()
        {
            var visibility = RowVisibility.For(ElementFacet.Pouffe);
            var cornerRadiusRow = Rows.NumberField(CornerRadiusLabel, visibility, "мм",
                CornerRadiusNode);
            var seatThicknessRow = Rows.NumberField(SeatThicknessLabel, visibility, "мм",
                SeatThicknessNode);

            Bind(seatThicknessRow, ThicknessOf, SetThickness,
                PouffeLayout.DefaultSeatThicknessMM.ToString());
            Bind(cornerRadiusRow, RadiusOf, SetRadius, "0");
        }

        private static int RadiusOf(KitchenElement element) =>
            element is PouffeElement pouffe ? pouffe.CornerRadiusMM : 0;

        private static void SetRadius(KitchenElement element, int value)
        {
            if (element is PouffeElement pouffe) pouffe.CornerRadiusMM = value;
        }

        private static int ThicknessOf(KitchenElement element) =>
            element is PouffeElement pouffe
                ? pouffe.SeatThicknessMM
                : PouffeLayout.DefaultSeatThicknessMM;

        private static void SetThickness(KitchenElement element, int value)
        {
            if (element is PouffeElement pouffe) pouffe.SeatThicknessMM = value;
        }
    }
}
