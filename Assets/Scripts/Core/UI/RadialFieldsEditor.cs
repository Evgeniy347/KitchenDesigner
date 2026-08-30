using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class RadialFieldsEditor : ElementFieldsEditor
    {
        private TMP_InputField? _radius;

        public RadialFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is RadialShelfElement;

        public override void Build() =>
            _radius = Rows.NumberField("Радиус угла", RowVisibility.For(ElementFacet.Radial));

        public override System.Collections.Generic.IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _radius;
        }

        public override void Show(KitchenElement element)
        {
            if (_radius == null) return;
            _radius.text = RadiusOf(element).ToString();
        }

        public override void Refresh(KitchenElement element)
        {
            if (element is RadialShelfElement shelf)
                Fields.RefreshUnfocused(_radius, shelf.CornerRadius.ToString());
        }

        public override void Apply(KitchenElement element)
        {
            if (element is RadialShelfElement shelf)
                shelf.CornerRadius = Fields.ParseInt(_radius, shelf.CornerRadius);
        }

        public override void AfterApply(KitchenElement element)
        {
            if (element is RadialShelfElement shelf && _radius != null)
                _radius.text = shelf.CornerRadius.ToString();
        }

        public override void Track(KitchenElement element) =>
            Fields.Track(_radius, RadiusOf(element).ToString());

        private static int RadiusOf(KitchenElement element) =>
            element is RadialShelfElement shelf
                ? shelf.CornerRadius
                : AppConstants.RADIAL_CORNER_RADIUS_DEFAULT;
    }
}
