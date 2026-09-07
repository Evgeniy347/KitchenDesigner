using System.Collections.Generic;
using TMPro;
using KitchenDesigner.Core.Analysis;

namespace KitchenDesigner.Core.UI
{
    internal sealed class PipeFittingFieldsEditor : ElementFieldsEditor
    {
        private readonly TMP_InputField?[] _bores = new TMP_InputField?[3];

        public PipeFittingFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is PipeFittingElement;

        public override DimensionPolicy Dimensions => DimensionPolicy.Computed;

        public override bool WidthEditable => false;

        public override bool HeightEditable => false;

        public override bool DepthEditable => false;

        public override void Build()
        {
            _bores[0] = ReadOnlyField("Диаметр 1", ElementFacet.PipeFitting);
            _bores[1] = ReadOnlyField("Диаметр 2", ElementFacet.PipeFittingSecondPort);
            _bores[2] = ReadOnlyField("Диаметр 3", ElementFacet.PipeFittingThirdPort);
        }

        public override void Show(KitchenElement element) => WriteDerived(element);

        public override void Refresh(KitchenElement element) => WriteDerived(element);

        public override void AfterApply(KitchenElement element) => WriteDerived(element);

        private TMP_InputField ReadOnlyField(string label, ElementFacet facet)
        {
            var field = Rows.NumberField(label, RowVisibility.For(facet));
            UIRowEnabled.SetControlEnabled(field, false);
            return field;
        }

        private void WriteDerived(KitchenElement element)
        {
            IReadOnlyList<string?> sizes = ScenePipeSurvey.SizesOf(element);
            for (int i = 0; i < _bores.Length; i++)
            {
                var field = _bores[i];
                if (field != null) field.text = ScenePipeSurvey.DesignationAt(sizes, i);
            }
        }
    }
}
