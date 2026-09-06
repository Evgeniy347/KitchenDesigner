namespace KitchenDesigner.Core.UI
{
    internal sealed class StoolFieldsEditor : NumberFieldsEditor
    {
        public StoolFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is StoolElement;

        public override void Build() =>
            BindCornerRadius<StoolElement>(RowVisibility.For(ElementFacet.Stool),
                stool => stool.CornerRadiusMM, (stool, value) => stool.CornerRadiusMM = value);
    }
}
