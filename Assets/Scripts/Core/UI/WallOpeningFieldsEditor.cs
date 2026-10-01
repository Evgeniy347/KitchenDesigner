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
            _tint = Rows.Dropdown(Loc.T("element.window.tint"), new List<string> { Loc.T("element.window.tintClear"), Loc.T("element.window.tintTinted") },
                OnTintSelected, RowVisibility.ForExcept(ElementFacet.Window, ElementFacet.Door),
                "CtxTint", hint: "element.window.tint");

            _sillProtrusion = Rows.NumberField(Loc.T("element.window.sill"),
                RowVisibility.ForExcept(ElementFacet.Window, ElementFacet.Door),
                hint: "element.window.sill");

            _sashType = Rows.Dropdown(Loc.T("element.door.sash"), new List<string> { Loc.T("element.door.sashGlazed"), Loc.T("element.door.sashSolid") },
                OnSashTypeSelected, RowVisibility.For(ElementFacet.Door), "CtxSashType",
                hint: "element.door.sash");

            var modeOptions = new List<string>
            {
                FacadeDoor.Label(DoorMode.HingeFrontLeft),
                FacadeDoor.Label(DoorMode.HingeFrontRight),
                FacadeDoor.Label(DoorMode.HingeFrontTop),
                FacadeDoor.Label(DoorMode.HingeFrontBottom),
            };
            _openingMode = Rows.Dropdown(Loc.T("element.window.openingMode"), modeOptions, OnOpeningModeSelected,
                RowVisibility.For(ElementFacet.Window), "CtxWinMode",
                hint: "element.window.openingMode");
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
            if (Host.Target is WindowElement window)
                ChoiceRowUndo.Commit(window, () => window.Tint = (GlassTint)index);
        }

        private void OnSashTypeSelected(int index)
        {
            if (Host.Target is DoorElement door)
                ChoiceRowUndo.Commit(door, () => door.SashType = (DoorSashType)index);
        }

        private void OnOpeningModeSelected(int index)
        {
            if (Host.Target is WindowElement window)
                ChoiceRowUndo.Commit(window, () => window.Mode = (DoorMode)index);
            else if (Host.Target is DoorElement door)
                ChoiceRowUndo.Commit(door, () => door.Mode = (DoorMode)index);
        }
    }
}
