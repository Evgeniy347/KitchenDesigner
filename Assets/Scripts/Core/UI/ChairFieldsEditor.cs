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
            BindCornerRadius<ChairElement>(visibility, chair => chair.CornerRadiusMM,
                (chair, value) => chair.CornerRadiusMM = value, CornerRadiusNode);
            Bind<ChairElement>(
                Rows.NumberField("Высота сиденья", visibility, "мм", SeatHeightNode),
                chair => chair.SeatHeightMM, (chair, value) => chair.SeatHeightMM = value, "0");
        }
    }
}
