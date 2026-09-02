namespace KitchenDesigner.Core.UI
{
    internal sealed class TableLegFieldsEditor : NumberFieldsEditor
    {
        private const int FallbackInsetMM = 100;

        public TableLegFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) =>
            element is TableElement || element is RadiusTableElement;

        public override void Build() =>
            Bind(Rows.NumberField("Сдвиг опор", RowVisibility.For(ElementFacet.Table)),
                InsetOf, SetInset, FallbackInsetMM.ToString());

        private static int InsetOf(KitchenElement element) =>
            element is TableElement table ? table.LegInsetMM
                : element is RadiusTableElement radiusTable ? radiusTable.LegInsetMM
                : FallbackInsetMM;

        private static void SetInset(KitchenElement element, int value)
        {
            if (element is TableElement table) table.LegInsetMM = value;
            else if (element is RadiusTableElement radiusTable) radiusTable.LegInsetMM = value;
        }
    }
}
