using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class PipeFieldsEditor : ElementFieldsEditor
    {
        private readonly PipeEndsDiagram _ends;

        private TMP_Dropdown? _size;
        private TMP_Text? _outer;
        private TMP_Text? _inner;
        private TMP_Text? _wall;

        public PipeFieldsEditor(IContextMenuHost host) : base(host) =>
            _ends = new PipeEndsDiagram(host, () => Host.Target as PipeElement);

        public override bool Handles(KitchenElement element) => element is PipeElement;

        public override bool WidthEditable => false;

        public override bool DepthEditable => false;

        public override string HeightLabel => Loc.T("element.pipe.length");

        public override void Build()
        {
            var visibility = RowVisibility.For(ElementFacet.Pipe);
            _size = Rows.Dropdown(Loc.T("element.pipe.nominalBore"),
                new List<string>(PipeElementSpec.Designations()), OnSizeSelected, visibility,
                "CtxPipeSize", hint: "element.pipe.nominalBore");
            _outer = ReadOnlyField(Loc.T("element.pipe.outerDiameter"), visibility, hint: "element.pipe.derived");
            _inner = ReadOnlyField(Loc.T("element.pipe.innerDiameter"), visibility, hint: "element.pipe.derived");
            _wall = ReadOnlyField(Loc.T("element.pipe.wallThickness"), visibility, hint: "element.pipe.derived");
            _ends.Build(Rows.Parent);
        }

        public override void Show(KitchenElement element)
        {
            if (!(element is PipeElement pipe)) return;
            _size?.SetValueWithoutNotify(PipeElementSpec.IndexOf(pipe.SizeId));
            WriteDerived(pipe);
            _ends.Show(pipe);
        }

        public override void Refresh(KitchenElement element)
        {
            if (!(element is PipeElement pipe)) return;
            WriteDerived(pipe);
            _ends.Tick(pipe);
        }

        public override void AfterApply(KitchenElement element)
        {
            if (!(element is PipeElement pipe)) return;
            WriteDerived(pipe);
            _ends.Show(pipe);
        }

        private TMP_Text ReadOnlyField(string label, RowVisibility visibility, string? hint = null) =>
            Rows.ReadOnlyField(label, visibility, hint);

        private static string Millimetres(string number) => NumberFormat.WithUnit(number, Loc.T("unit.mm"));

        private void WriteDerived(PipeElement pipe)
        {
            if (_outer != null) _outer.text = Millimetres(PipeElementSpec.OuterDiameterText(pipe.SizeId, NumberFormat.DecimalSeparator));
            if (_inner != null) _inner.text = Millimetres(PipeElementSpec.InnerDiameterText(pipe.SizeId, NumberFormat.DecimalSeparator));
            if (_wall != null) _wall.text = Millimetres(PipeElementSpec.WallThicknessText(pipe.SizeId, NumberFormat.DecimalSeparator));
        }

        private void OnSizeSelected(int index)
        {
            if (!(Host.Target is PipeElement pipe)) return;

            var before = UndoableProperties.Capture(pipe);
            pipe.SizeId = PipeElementSpec.SizeIdAt(index);
            var after = UndoableProperties.Capture(pipe);
            var command = SetPropertiesCommand.TryCreate(pipe, before, after);
            if (command != null) CommandStack.Execute(command);

            WriteDerived(pipe);
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.RefreshHighlight(pipe);
        }
    }
}
