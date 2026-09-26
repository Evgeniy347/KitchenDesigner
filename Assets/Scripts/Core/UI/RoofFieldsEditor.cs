using System.Collections.Generic;
using TMPro;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core.UI
{
    internal sealed class RoofFieldsEditor : NumberFieldsEditor
    {
        public const string TypeNode = "CtxRoofType";
        public const string RidgeAxisNode = "CtxRoofRidgeAxis";
        public const string PitchNode = "CtxRoofPitch";
        public const string OverhangNode = "CtxRoofOverhang";
        public const string RafterStepNode = "CtxRoofRafterStep";

        private TMP_Dropdown? _type;
        private TMP_Dropdown? _ridgeAxis;

        public RoofFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is RoofElement;

        public override bool WidthEditable => false;

        public override bool HeightEditable => false;

        public override bool DepthEditable => false;

        public override void Build()
        {
            var isRoof = RowVisibility.When(() => Host.Target is RoofElement);

            _type = Rows.Dropdown("Тип крыши",
                new List<string> { "Односкатная", "Двускатная", "Вальмовая" },
                OnTypeSelected, isRoof, TypeNode, hint: "element.roof.type");

            _ridgeAxis = Rows.Dropdown("Ось конька",
                new List<string> { "Авто", "X", "Z" },
                OnRidgeAxisSelected, isRoof, RidgeAxisNode, hint: "element.roof.ridgeAxis");

            var pitchRow = Rows.NumberField("Уклон", isRoof, "°", PitchNode,
                hint: "element.roof.pitchDeg");
            BindDecimal<RoofElement>(pitchRow, r => r.PitchDeg,
                (r, v) => r.PitchDeg = v, RoofDefaults.PitchDeg.ToString("F1"));

            var overhangRow = Rows.NumberField("Свес", isRoof, "мм", OverhangNode,
                hint: "element.roof.overhangMm");
            Bind<RoofElement>(overhangRow, r => r.OverhangMm, (r, v) => r.OverhangMm = v,
                RoofDefaults.OverhangMm.ToString());

            var rafterStepRow = Rows.NumberField("Шаг стропил", isRoof, "мм", RafterStepNode,
                hint: "element.roof.rafterStepMm");
            Bind<RoofElement>(rafterStepRow, r => r.RafterStepMm, (r, v) => r.RafterStepMm = v,
                RoofDefaults.RafterStepMm.ToString());
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
            if (!(element is RoofElement roof)) return;
            _type?.SetValueWithoutNotify((int)roof.Type);
            _type?.RefreshShownValue();
            _ridgeAxis?.SetValueWithoutNotify((int)roof.RidgeAxis);
            _ridgeAxis?.RefreshShownValue();
        }

        private void OnTypeSelected(int index)
        {
            if (Host.Target is RoofElement roof)
                ChoiceRowUndo.Commit(roof, () => roof.Type = (RoofType)index);
        }

        private void OnRidgeAxisSelected(int index)
        {
            if (Host.Target is RoofElement roof)
                ChoiceRowUndo.Commit(roof, () => roof.RidgeAxis = (RoofRidgeAxis)index);
        }
    }
}
