using System.Collections.Generic;
using TMPro;
using KitchenDesigner.Core.Analysis;

namespace KitchenDesigner.Core.UI
{
    internal sealed class PipeFittingFieldsEditor : ElementFieldsEditor
    {
        private readonly TMP_InputField?[] _bores = new TMP_InputField?[3];
        private readonly PipeFittingPortsDiagram _ports;

        public PipeFittingFieldsEditor(IContextMenuHost host) : base(host) =>
            _ports = new PipeFittingPortsDiagram(host, () => Host.Target as PipeFittingElement);

        public override bool Handles(KitchenElement element) => element is PipeFittingElement;

        public override DimensionPolicy Dimensions => DimensionPolicy.Computed;

        public override bool WidthEditable => false;

        public override bool HeightEditable => false;

        public override bool DepthEditable => false;

        public override void Build()
        {
            _bores[0] = ReadOnlyField("Диаметр 1", ElementFacet.PipeFitting,
                hint: "element.pipeFitting.bore");
            _bores[1] = ReadOnlyField("Диаметр 2", ElementFacet.PipeFittingSecondPort);
            _bores[2] = ReadOnlyField("Диаметр 3", ElementFacet.PipeFittingThirdPort);
            _ports.Build(Rows.Parent);
        }

        public override void Show(KitchenElement element)
        {
            WriteDerived(element);
            if (element is PipeFittingElement fitting) _ports.Show(fitting);
        }

        public override void Refresh(KitchenElement element)
        {
            WriteDerived(element);
            if (element is PipeFittingElement fitting) _ports.Tick(fitting);
        }

        public override void AfterApply(KitchenElement element)
        {
            WriteDerived(element);
            if (element is PipeFittingElement fitting) _ports.Show(fitting);
        }

        private TMP_InputField ReadOnlyField(string label, ElementFacet facet, string? hint = null)
        {
            var field = Rows.NumberField(label, RowVisibility.For(facet), "мм", null, hint);
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
