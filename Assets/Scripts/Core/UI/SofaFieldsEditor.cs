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
            BindCornerRadius<SofaElement>(visibility, sofa => sofa.CornerRadiusMM,
                (sofa, value) => sofa.CornerRadiusMM = value, CornerRadiusNode);
            Bind<SofaElement>(
                Rows.NumberField("Высота основания", visibility, "мм", SeatHeightNode),
                sofa => sofa.SeatHeightMM, (sofa, value) => sofa.SeatHeightMM = value, "0");
        }
    }
}
