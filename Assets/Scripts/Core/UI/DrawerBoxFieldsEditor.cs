using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class DrawerBoxFieldsEditor : ElementFieldsEditor
    {
        private const int MinInternalWidthMM = 100;
        private const int FallbackBoxWidthMM = 400;

        private readonly Action<KitchenElement> _reopen;

        private TMP_InputField? _boxWidth;
        private TMP_Dropdown? _type, _length, _color, _upperLength;

        public DrawerBoxFieldsEditor(IContextMenuHost host, Action<KitchenElement> reopen)
            : base(host) => _reopen = reopen;

        public override bool Handles(KitchenElement element) => element is DrawerElement;

        public override DimensionPolicy Dimensions => DimensionPolicy.Computed;

        public override bool WidthEditable => false;

        public override bool HeightEditable => false;

        public override bool DepthEditable => false;

        private DrawerElement? Drawer => Host.Target as DrawerElement;

        public override void Build()
        {
            var drawerOnly = RowVisibility.For(ElementFacet.Drawer);

            var typeNames = new List<string>
                { Loc.T("element.drawer.typeA"), Loc.T("element.drawer.typeB"), Loc.T("element.drawer.typeC"), Loc.T("element.drawer.typeD") };
            _type = Rows.Dropdown(Loc.T("element.drawer.type"), typeNames, OnTypeChanged, drawerOnly, "CtxDrawerType",
                hint: "element.drawer.type");

            var lengthNames = new List<string>();
            foreach (var l in DrawerConstants.ValidLengths) lengthNames.Add(Loc.F("unit.mmValue", l));
            _length = Rows.Dropdown(Loc.T("element.drawer.length"), lengthNames, OnLengthChanged, drawerOnly, "CtxDrawerLen",
                hint: "element.drawer.length");

            var colorNames = new List<string> { Loc.T("element.drawer.colorAnthracite"), Loc.T("element.drawer.colorWhite"), Loc.T("element.drawer.colorBlack") };
            _color = Rows.Dropdown(Loc.T("element.drawer.color"), colorNames, OnColorChanged, drawerOnly, "CtxDrawerColor",
                hint: "element.drawer.color");

            _boxWidth = Rows.NumberField(Loc.T("element.drawer.boxWidth"), drawerOnly,
                hint: "element.drawer.boxWidth");

            Rows.ValueButton("CtxDrawerDouble", Loc.T("element.drawer.double"), CreatePaired,
                RowVisibility.For(ElementFacet.Drawer, CanCreateDouble));

            var upperLenNames = new List<string>();
            foreach (var l in DrawerConstants.ValidLengths) upperLenNames.Add(Loc.F("unit.mmValue", l));
            var upperLenRow = Rows.NamedDropdown("CtxDrawerUpperLen", Loc.T("element.drawer.upperLength"),
                upperLenNames, OnUpperLengthChanged,
                RowVisibility.For(ElementFacet.Drawer, HasUpper));
            _upperLength = upperLenRow.dropdown;
            HintBadge.AttachAfterLabel(upperLenRow.label as TextMeshProUGUI,
                hint: "element.drawer.upperLength");

            Rows.ValueButton("CtxDrawerRemoveUpper", Loc.T("element.drawer.removeUpper"), RemoveUpper,
                RowVisibility.For(ElementFacet.Drawer, HasUpper));
        }

        public override IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _boxWidth;
        }

        public override void Show(KitchenElement element)
        {
            if (!(element is DrawerElement drawer)) return;
            if (_boxWidth != null) _boxWidth.text = drawer.BoxWidth.ToString();
            _type?.SetValueWithoutNotify(DrawerConstants.TypeIndex(drawer.Type));
            _length?.SetValueWithoutNotify(
                Array.IndexOf(DrawerConstants.ValidLengths, drawer.NominalLength));
            _color?.SetValueWithoutNotify((int)drawer.Color);
            var upper = drawer.FindPaired();
            if (_upperLength != null && upper != null)
                _upperLength.SetValueWithoutNotify(
                    Array.IndexOf(DrawerConstants.ValidLengths, upper.NominalLength));
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

        private void OnTypeChanged(int index)
        {
            var drawer = Drawer;
            if (drawer == null) return;
            ChoiceRowUndo.Commit(drawer, () => drawer.Type = DrawerConstants.TypeFromIndex(index),
                drawer.FindPaired());
        }

        private void OnLengthChanged(int index)
        {
            var drawer = Drawer;
            if (drawer == null || index < 0 || index >= DrawerConstants.ValidLengths.Length) return;
            ChoiceRowUndo.Commit(drawer,
                () => drawer.NominalLength = DrawerConstants.ValidLengths[index],
                drawer.FindPaired());
        }

        private void OnColorChanged(int index)
        {
            var drawer = Drawer;
            if (drawer == null || index < 0 || index > (int)DrawerColor.Black) return;
            ChoiceRowUndo.Commit(drawer, () => drawer.Color = (DrawerColor)index,
                drawer.FindPaired());
        }

        private void OnUpperLengthChanged(int index)
        {
            var drawer = Drawer;
            if (drawer == null || index < 0 || index >= DrawerConstants.ValidLengths.Length) return;
            var upper = drawer.FindPaired();
            if (upper == null) return;
            ChoiceRowUndo.Commit(upper,
                () => upper.NominalLength = DrawerConstants.ValidLengths[index], drawer);
        }

        private bool HasUpper()
        {
            var drawer = Drawer;
            return drawer != null && !drawer.IsUpperDrawer && drawer.FindPaired() != null;
        }

        private bool CanCreateDouble()
        {
            var drawer = Drawer;
            if (drawer == null || drawer.IsUpperDrawer || drawer.FindPaired() != null) return false;
            float freeMM = DrawerValidator.FreeHeightAboveMM(drawer, PartRegistry.GetAll());
            return freeMM >= DrawerConstants.GetMinOpeningHeight(DrawerConstants.UPPER_DRAWER_TYPE);
        }

        private void CreatePaired()
        {
            var drawer = Drawer;
            if (drawer == null) return;
            var pair = DrawerLinks.CreatePair(drawer);
            if (pair == null) return;
            CommandStack.Execute(new CreateCommand(pair.gameObject));
            ElementHighlighter.Instance?.RefreshHighlights();
            _reopen(drawer);
        }

        private void RemoveUpper()
        {
            var drawer = Drawer;
            if (drawer == null) return;
            var upperGo = DrawerLinks.DetachPair(drawer);
            if (upperGo != null) CommandStack.Execute(new DeleteCommand(upperGo));
            ElementHighlighter.Instance?.RefreshHighlights();
            _reopen(drawer);
        }
    }
}
