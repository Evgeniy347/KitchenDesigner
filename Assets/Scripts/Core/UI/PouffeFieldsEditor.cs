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

            Bind<PouffeElement>(seatThicknessRow, pouffe => pouffe.SeatThicknessMM,
                (pouffe, value) => pouffe.SeatThicknessMM = value,
                PouffeLayout.DefaultSeatThicknessMM.ToString());
            Bind<PouffeElement>(cornerRadiusRow, pouffe => pouffe.CornerRadiusMM,
                (pouffe, value) => pouffe.CornerRadiusMM = value, "0");
        }
    }
}
