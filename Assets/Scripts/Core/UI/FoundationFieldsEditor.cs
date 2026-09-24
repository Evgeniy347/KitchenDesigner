using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using KitchenDesigner.Core.Construction;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class FoundationFieldsEditor : NumberFieldsEditor
    {
        public const string SoilNode = "CtxFoundationSoil";
        public const string ConcreteNode = "CtxFoundationConcrete";
        public const string CompactedNode = "CtxFoundationCompacted";
        public const string SandNode = "CtxFoundationSand";
        public const string GravelNode = "CtxFoundationGravel";
        public const string RebarDiameterNode = "CtxFoundationRebarDiameter";
        public const string RebarStepNode = "CtxFoundationRebarStep";
        public const string CoverNode = "CtxFoundationCover";
        public const string FrostDepthLabel = "Глубина промерзания";

        private TMP_Dropdown? _soil;
        private TMP_Dropdown? _concrete;
        private Toggle? _compacted;
        private TMP_InputField? _frostDepth;

        public FoundationFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is FoundationElement;

        public override bool DepthEditable => false;

        public override string HeightLabel => "Глубина";

        public override void Build()
        {
            var isFoundation = RowVisibility.When(() => Host.Target is FoundationElement);

            _soil = Rows.Dropdown("Грунт", new List<string>(SoilKindTitles.All), OnSoilSelected,
                isFoundation, SoilNode, hint: "element.foundation.soil");

            _frostDepth = ReadOnlyField(FrostDepthLabel, isFoundation,
                hint: "element.foundation.frostDepth");

            var sandRow = Rows.NumberField("Подушка: песок", isFoundation, "мм", SandNode,
                hint: "element.foundation.sand");
            Bind<FoundationElement>(sandRow, f => f.SandMm, (f, v) => f.SandMm = v,
                KitchenSettings.Instance.ConstructionSandMm.ToString());

            var gravelRow = Rows.NumberField("Подушка: щебень", isFoundation, "мм", GravelNode,
                hint: "element.foundation.gravel");
            Bind<FoundationElement>(gravelRow, f => f.GravelMm, (f, v) => f.GravelMm = v,
                KitchenSettings.Instance.ConstructionGravelMm.ToString());

            _compacted = Rows.Toggle(CompactedNode, "Трамбовка", true, OnCompactedToggled,
                isFoundation, RowGap, hint: "element.foundation.compacted");

            _concrete = Rows.Dropdown("Класс бетона", new List<string>(ConcreteGradeTitles.All),
                OnConcreteSelected, isFoundation, ConcreteNode, hint: "element.foundation.concrete");

            var diameterRow = Rows.NumberField("Арматура: Ø", isFoundation, "мм", RebarDiameterNode,
                hint: "element.foundation.rebarDiameter");
            Bind<FoundationElement>(diameterRow, f => f.RebarDiameterMm,
                (f, v) => f.RebarDiameterMm = v, FoundationRebarDefaults.DiameterMm.ToString());

            var stepRow = Rows.NumberField("Арматура: шаг", isFoundation, "мм", RebarStepNode,
                hint: "element.foundation.rebarStep");
            Bind<FoundationElement>(stepRow, f => f.RebarStepMm, (f, v) => f.RebarStepMm = v,
                FoundationRebarDefaults.StepMm.ToString());

            var coverRow = Rows.NumberField("Защитный слой", isFoundation, "мм", CoverNode,
                hint: "element.foundation.cover");
            Bind<FoundationElement>(coverRow, f => f.CoverMm, (f, v) => f.CoverMm = v,
                FoundationRebarDefaults.CoverMm.ToString());
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
            if (!(element is FoundationElement foundation)) return;

            _soil?.SetValueWithoutNotify((int)foundation.SoilKind);
            _soil?.RefreshShownValue();
            _concrete?.SetValueWithoutNotify((int)foundation.ConcreteGrade);
            _concrete?.RefreshShownValue();
            _compacted?.SetIsOnWithoutNotify(foundation.Compacted);
            if (_frostDepth != null) _frostDepth.text = foundation.FrostDepthText;
        }

        private TMP_InputField ReadOnlyField(string label, RowVisibility visibility, string hint)
        {
            var field = Rows.NumberField(label, visibility, "мм", null, hint);
            UIRowEnabled.SetControlEnabled(field, false);
            return field;
        }

        private void OnSoilSelected(int index)
        {
            if (!(Host.Target is FoundationElement foundation)) return;
            ChoiceRowUndo.Commit(foundation, () => foundation.SoilKind = (SoilKind)index);
            WriteWidgets(foundation);
        }

        private void OnConcreteSelected(int index)
        {
            if (Host.Target is FoundationElement foundation)
                ChoiceRowUndo.Commit(foundation, () => foundation.ConcreteGrade = (ConcreteGrade)index);
        }

        private void OnCompactedToggled(bool on)
        {
            if (Host.Target is FoundationElement foundation)
                ChoiceRowUndo.Commit(foundation, () => foundation.Compacted = on);
        }
    }
}
