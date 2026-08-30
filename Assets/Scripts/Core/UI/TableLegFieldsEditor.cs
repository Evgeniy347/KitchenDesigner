using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class TableLegFieldsEditor : ElementFieldsEditor
    {
        private const int FallbackInsetMM = 100;

        private TMP_InputField? _legInset;

        public TableLegFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) =>
            element is TableElement || element is RadiusTableElement;

        public override void Build() =>
            _legInset = Rows.NumberField("Сдвиг опор", RowVisibility.For(ElementFacet.Table));

        public override System.Collections.Generic.IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _legInset;
        }

        public override void Show(KitchenElement element)
        {
            if (_legInset == null || !Handles(element)) return;
            _legInset.text = InsetOf(element).ToString();
        }

        public override void Refresh(KitchenElement element)
        {
            if (Handles(element)) Fields.RefreshUnfocused(_legInset, InsetOf(element).ToString());
        }

        public override void Apply(KitchenElement element)
        {
            if (_legInset == null) return;
            if (element is TableElement table)
                table.LegInsetMM = Fields.ParseInt(_legInset, table.LegInsetMM);
            else if (element is RadiusTableElement radiusTable)
                radiusTable.LegInsetMM = Fields.ParseInt(_legInset, radiusTable.LegInsetMM);
        }

        public override void AfterApply(KitchenElement element)
        {
            if (_legInset != null && Handles(element))
                _legInset.text = InsetOf(element).ToString();
        }

        public override void Track(KitchenElement element) =>
            Fields.Track(_legInset,
                Handles(element) ? InsetOf(element).ToString() : FallbackInsetMM.ToString());

        private static int InsetOf(KitchenElement element) =>
            element is TableElement table ? table.LegInsetMM
                : element is RadiusTableElement radiusTable ? radiusTable.LegInsetMM
                : FallbackInsetMM;
    }
}
