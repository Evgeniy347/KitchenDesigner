using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class DrawerBoxFieldsEditor : ElementFieldsEditor
    {
        private const int MinInternalWidthMM = 100;
        private const int FallbackBoxWidthMM = 400;

        private TMP_InputField? _boxWidth;

        public DrawerBoxFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is DrawerElement;

        public override DimensionPolicy Dimensions => DimensionPolicy.Computed;

        public override bool WidthEditable => false;

        public override bool HeightEditable => false;

        public override bool DepthEditable => false;

        public override void Build() =>
            _boxWidth = Rows.NumberField("Ширина короба", RowVisibility.For(ElementFacet.Drawer));

        public override IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _boxWidth;
        }

        public override void Show(KitchenElement element)
        {
            if (_boxWidth != null && element is DrawerElement drawer)
                _boxWidth.text = drawer.BoxWidth.ToString();
        }

        public override void Refresh(KitchenElement element)
        {
            if (element is DrawerElement drawer)
                Fields.RefreshUnfocused(_boxWidth, drawer.BoxWidth.ToString());
        }

        public override void Apply(KitchenElement element)
        {
            if (_boxWidth == null || !(element is DrawerElement drawer)) return;
            int boxWidth = Fields.ParseInt(_boxWidth, drawer.BoxWidth);
            int inset = drawer.System == DrawerSystem.Movento
                ? DrawerConstants.MOVENTO_WIDTH_INSET : 0;
            drawer.InternalWidth = Mathf.Max(MinInternalWidthMM, boxWidth + inset);
        }

        public override void Track(KitchenElement element) =>
            Fields.Track(_boxWidth, element is DrawerElement drawer
                ? drawer.BoxWidth.ToString()
                : FallbackBoxWidthMM.ToString());
    }
}
