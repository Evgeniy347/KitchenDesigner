namespace KitchenDesigner.Core.UI
{
    internal sealed class TableLegFieldsEditor : NumberFieldsEditor
    {
        private const int FallbackInsetMM = 100;

        public TableLegFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) =>
            element is TableElement || element is RadiusTableElement;

        public override void Build() =>
            Bind<TableElement>(
                Rows.NumberField("Сдвиг опор", RowVisibility.For(ElementFacet.Table)),
                table => table.LegInsetMM, (table, value) => table.LegInsetMM = value,
                FallbackInsetMM.ToString())
            .Or<RadiusTableElement>(table => table.LegInsetMM,
                (table, value) => table.LegInsetMM = value);
    }
}
