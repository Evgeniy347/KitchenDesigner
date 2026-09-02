namespace KitchenDesigner.Core.UI
{
    internal sealed class StoolFieldsEditor : NumberFieldsEditor
    {
        public StoolFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is StoolElement;

        public override void Build() =>
            Bind(Rows.NumberField("Скругление", RowVisibility.For(ElementFacet.Stool)),
                RadiusOf, SetRadius, "0");

        private static int RadiusOf(KitchenElement element) =>
            element is StoolElement stool ? stool.CornerRadiusMM : 0;

        private static void SetRadius(KitchenElement element, int value)
        {
            if (element is StoolElement stool) stool.CornerRadiusMM = value;
        }
    }
}
