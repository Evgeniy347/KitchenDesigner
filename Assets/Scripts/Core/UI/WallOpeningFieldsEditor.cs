using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class WallOpeningFieldsEditor : ElementFieldsEditor
    {
        private const int FallbackSillMM = 50;

        private TMP_InputField? _sillProtrusion;
        private TMP_Dropdown? _tint, _sashType, _openingMode;

        public WallOpeningFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) =>
            element is WindowElement || element is DoorElement;

        public override DimensionPolicy Dimensions => DimensionPolicy.KeepDepth;

        public override bool DepthEditable => false;

        public override void Build()
        {
            _tint = Rows.Dropdown("Стекло", new List<string> { "Прозрачное", "Тонированное" },
                OnTintSelected, RowVisibility.ForExcept(ElementFacet.Window, ElementFacet.Door),
                "CtxTint");

            _sillProtrusion = Rows.NumberField("Подоконник",
                RowVisibility.ForExcept(ElementFacet.Window, ElementFacet.Door));

            _sashType = Rows.Dropdown("Створка", new List<string> { "Стекло", "Глухая" },
                OnSashTypeSelected, RowVisibility.For(ElementFacet.Door), "CtxSashType");

            var modeOptions = new List<string>
            {
                FacadeDoor.Label(DoorMode.HingeFrontLeft),
                FacadeDoor.Label(DoorMode.HingeFrontRight),
                FacadeDoor.Label(DoorMode.HingeFrontTop),
                FacadeDoor.Label(DoorMode.HingeFrontBottom),
            };
            _openingMode = Rows.Dropdown("Открывание", modeOptions, OnOpeningModeSelected,
                RowVisibility.For(ElementFacet.Window), "CtxWinMode");
        }

        public override IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _sillProtrusion;
        }

        public override void Show(KitchenElement element)
        {
            if (element is WindowElement window)
            {
                if (_sillProtrusion != null)
                    _sillProtrusion.text = window.SillProtrusionMM.ToString();
                _tint?.SetValueWithoutNotify((int)window.Tint);
                _openingMode?.SetValueWithoutNotify((int)window.Mode);
            }
            else if (element is DoorElement door)
            {
                _sashType?.SetValueWithoutNotify((int)door.SashType);
                _openingMode?.SetValueWithoutNotify((int)door.Mode);
            }
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

        private void OnTintSelected(int index)
        {
            if (Host.Target is WindowElement window) window.Tint = (GlassTint)index;
        }

        private void OnSashTypeSelected(int index)
        {
            if (Host.Target is DoorElement door) door.SashType = (DoorSashType)index;
        }

        private void OnOpeningModeSelected(int index)
        {
            if (Host.Target is WindowElement window) window.Mode = (DoorMode)index;
            else if (Host.Target is DoorElement door) door.Mode = (DoorMode)index;
        }
    }
}
