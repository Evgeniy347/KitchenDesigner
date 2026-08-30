using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class WallOpeningFieldsEditor : ElementFieldsEditor
    {
        private const int FallbackSillMM = 50;

        private TMP_InputField? _sillProtrusion;

        public WallOpeningFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) =>
            element is WindowElement || element is DoorElement;

        public override DimensionPolicy Dimensions => DimensionPolicy.KeepDepth;

        public override bool DepthEditable => false;

        public override void Build() =>
            _sillProtrusion = Rows.NumberField("Подоконник",
                RowVisibility.For(ElementFacet.Window, () => !Host.TargetIsDoor));

        public override IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _sillProtrusion;
        }

        public override void Show(KitchenElement element)
        {
            if (_sillProtrusion != null && element is WindowElement window)
                _sillProtrusion.text = window.SillProtrusionMM.ToString();
        }

        public override void Refresh(KitchenElement element)
        {
            if (element is WindowElement window)
                Fields.RefreshUnfocused(_sillProtrusion, window.SillProtrusionMM.ToString());
        }

        public override void Apply(KitchenElement element)
        {
            if (_sillProtrusion != null && element is WindowElement window)
                window.SillProtrusionMM = Fields.ParseInt(_sillProtrusion, window.SillProtrusionMM);
        }

        public override void Track(KitchenElement element) =>
            Fields.Track(_sillProtrusion, element is WindowElement window
                ? window.SillProtrusionMM.ToString()
                : FallbackSillMM.ToString());
    }
}
