using System.Collections.Generic;
using TMPro;
using KitchenDesigner.Core.Analysis;

namespace KitchenDesigner.Core.UI
{
    internal sealed class PipeFittingFieldsEditor : ElementFieldsEditor
    {
        private readonly TMP_Text?[] _bores = new TMP_Text?[3];
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
            _bores[0] = ReadOnlyField(Loc.T("element.pipeFitting.bore1"), ElementFacet.PipeFitting,
                hint: "element.pipeFitting.bore");
            _bores[1] = ReadOnlyField(Loc.T("element.pipeFitting.bore2"), ElementFacet.PipeFittingSecondPort,
                hint: "element.pipeFitting.bore");
            _bores[2] = ReadOnlyField(Loc.T("element.pipeFitting.bore3"), ElementFacet.PipeFittingThirdPort,
                hint: "element.pipeFitting.bore");
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

        private TMP_Text ReadOnlyField(string label, ElementFacet facet, string? hint = null) =>
            Rows.ReadOnlyField(label, RowVisibility.For(facet), hint);

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
