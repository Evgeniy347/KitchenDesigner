namespace KitchenDesigner.Core.UI
{
    internal sealed class SofaFieldsEditor : NumberFieldsEditor
    {
        public const string CornerRadiusNode = "СкруглениеДивана";
        public const string SeatHeightNode = "ВысотаОснованияДивана";
        public const string EdgeRadiusNode = "SofaEdgeRadius";

        public SofaFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is SofaElement;

        public override void Build()
        {
            var visibility = RowVisibility.For(ElementFacet.Sofa);
            BindCornerRadius<SofaElement>(visibility, sofa => sofa.CornerRadiusMM,
                (sofa, value) => sofa.CornerRadiusMM = value, CornerRadiusNode);
            Bind<SofaElement>(
                Rows.NumberField(Loc.T("element.sofa.baseHeight"), visibility, Loc.T("unit.mm"), SeatHeightNode,
                    hint: "element.seat.seatHeight"),
                sofa => sofa.SeatHeightMM, (sofa, value) => sofa.SeatHeightMM = value, "0");
            Bind<SofaElement>(
                Rows.NumberField(Loc.T("element.sofa.edgeRadius"), visibility, Loc.T("unit.mm"),
                    EdgeRadiusNode, hint: "element.sofa.edgeRadius"),
                sofa => sofa.EdgeRadiusMM, (sofa, value) => sofa.EdgeRadiusMM = value, "0");
        }
    }
}
