using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class PipeFieldsEditor : ElementFieldsEditor
    {
        private readonly PipeEndsDiagram _ends;

        private TMP_Dropdown? _size;
        private TMP_InputField? _outer;
        private TMP_InputField? _inner;
        private TMP_InputField? _wall;

        public PipeFieldsEditor(IContextMenuHost host) : base(host) =>
            _ends = new PipeEndsDiagram(host, () => Host.Target as PipeElement);

        public override bool Handles(KitchenElement element) => element is PipeElement;

        public override bool WidthEditable => false;

        public override bool DepthEditable => false;

        public override string HeightLabel => "Длина";

        public override void Build()
        {
            var visibility = RowVisibility.For(ElementFacet.Pipe);
            _size = Rows.Dropdown("Условный проход",
                new List<string>(PipeElementSpec.Designations()), OnSizeSelected, visibility,
                "CtxPipeSize");
            _outer = ReadOnlyField("Наружный Ø", visibility);
            _inner = ReadOnlyField("Внутренний Ø", visibility);
            _wall = ReadOnlyField("Толщина стенки", visibility);
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

        private TMP_InputField ReadOnlyField(string label, RowVisibility visibility)
        {
            var field = Rows.NumberField(label, visibility);
            UIRowEnabled.SetControlEnabled(field, false);
            return field;
        }

        private void WriteDerived(PipeElement pipe)
        {
            if (_outer != null) _outer.text = PipeElementSpec.OuterDiameterText(pipe.SizeId);
            if (_inner != null) _inner.text = PipeElementSpec.InnerDiameterText(pipe.SizeId);
            if (_wall != null) _wall.text = PipeElementSpec.WallThicknessText(pipe.SizeId);
        }

        private void OnSizeSelected(int index)
        {
            if (!(Host.Target is PipeElement pipe)) return;
            pipe.SizeId = PipeElementSpec.SizeIdAt(index);
            WriteDerived(pipe);
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.RefreshHighlight(pipe);
        }
    }
}
