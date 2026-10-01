using System.Collections.Generic;
using TMPro;
using KitchenDesigner.Core.Construction;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class FloorSlabFieldsEditor : NumberFieldsEditor
    {
        public const string TechnologyNode = "CtxFloorSlabTechnology";
        public const string ConcreteNode = "CtxFloorSlabConcrete";
        public const string RebarDiameterNode = "CtxFloorSlabRebarDiameter";
        public const string RebarStepNode = "CtxFloorSlabRebarStep";

        private TMP_Dropdown? _technology;
        private TMP_Dropdown? _concrete;

        public FloorSlabFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is FloorSlabElement;

        public override string HeightLabel => Loc.T("element.floorSlab.thickness");

        public override void Build()
        {
            var isFloorSlab = RowVisibility.When(() => Host.Target is FloorSlabElement);
            var isSlabTechnology = RowVisibility.When(() =>
                Host.Target is FloorSlabElement s && s.Technology == SlabTechnology.Slab);

            _technology = Rows.Dropdown(Loc.T("element.floorSlab.technology"), new List<string> { Loc.T("element.floorSlab.monolithic"), Loc.T("element.floorSlab.beams") },
                OnTechnologySelected, isFloorSlab, TechnologyNode, hint: "element.floorSlab.technology");

            _concrete = Rows.Dropdown(Loc.T("element.floorSlab.concrete"), new List<string>(ConcreteGradeTitles.All),
                OnConcreteSelected, isSlabTechnology, ConcreteNode, hint: "element.floorSlab.concrete");

            var diameterRow = Rows.NumberField(Loc.T("element.floorSlab.rebarDiameter"), isSlabTechnology, Loc.T("unit.mm"), RebarDiameterNode,
                hint: "element.floorSlab.rebarDiameter");
            Bind<FloorSlabElement>(diameterRow, s => s.RebarDiameterMm, (s, v) => s.RebarDiameterMm = v,
                FoundationRebarDefaults.DiameterMm.ToString());

            var stepRow = Rows.NumberField(Loc.T("element.floorSlab.rebarStep"), isSlabTechnology, Loc.T("unit.mm"), RebarStepNode,
                hint: "element.floorSlab.rebarStep");
            Bind<FloorSlabElement>(stepRow, s => s.RebarStepMm, (s, v) => s.RebarStepMm = v,
                FoundationRebarDefaults.StepMm.ToString());
        }

        public override void Show(KitchenElement element)
        {
            base.Show(element);
            WriteWidgets(element);
        }

        public override void Refresh(KitchenElement element)
        {
            base.Refresh(element);
            WriteWidgets(element);
        }

        public override void AfterApply(KitchenElement element)
        {
            base.AfterApply(element);
            WriteWidgets(element);
        }

        private void WriteWidgets(KitchenElement element)
        {
            if (!(element is FloorSlabElement slab)) return;

            _technology?.SetValueWithoutNotify((int)slab.Technology);
            _technology?.RefreshShownValue();
            _concrete?.SetValueWithoutNotify((int)slab.ConcreteGrade);
            _concrete?.RefreshShownValue();
        }

        private void OnTechnologySelected(int index)
        {
            if (!(Host.Target is FloorSlabElement slab)) return;
            ChoiceRowUndo.Commit(slab, () => slab.Technology = (SlabTechnology)index);
            WriteWidgets(slab);
        }

        private void OnConcreteSelected(int index)
        {
            if (Host.Target is FloorSlabElement slab)
                ChoiceRowUndo.Commit(slab, () => slab.ConcreteGrade = (ConcreteGrade)index);
        }
    }
}
