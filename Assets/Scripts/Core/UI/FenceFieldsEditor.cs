using System.Collections.Generic;
using TMPro;
using KitchenDesigner.Core.Construction;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class FenceFieldsEditor : NumberFieldsEditor
    {
        public const string PostSectionNode = "CtxFencePostSection";
        public const string PostStepNode = "CtxFencePostStep";
        public const string PitDepthNode = "CtxFencePitDepth";
        public const string SheetMarkNode = "CtxFenceSheetMark";

        private TMP_Dropdown? _sheetMark;

        public FenceFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is FenceElement;

        public override bool DepthEditable => false;

        public override void Build()
        {
            var isFence = RowVisibility.When(() => Host.Target is FenceElement);

            var sectionRow = Rows.NumberField("Сечение столба", isFence, "мм", PostSectionNode,
                hint: "element.fence.postSection");
            Bind<FenceElement>(sectionRow, f => f.PostSectionMm, (f, v) => f.PostSectionMm = v,
                FenceDefaults.PostSectionMm.ToString());

            var stepRow = Rows.NumberField("Шаг столбов", isFence, "мм", PostStepNode,
                hint: "element.fence.postStep");
            Bind<FenceElement>(stepRow, f => f.PostStepMm, (f, v) => f.PostStepMm = v,
                FenceDefaults.PostStepMm.ToString());

            var pitRow = Rows.NumberField("Глубина ямы", isFence, "мм", PitDepthNode,
                hint: "element.fence.pitDepth");
            Bind<FenceElement>(pitRow, f => f.PitDepthMm, (f, v) => f.PitDepthMm = v,
                FenceDefaults.PitDepthMm.ToString());

            _sheetMark = Rows.Dropdown("Марка листа", new List<string>(FenceSheetMarkTitles.All),
                OnSheetMarkSelected, isFence, SheetMarkNode, hint: "element.fence.sheetMark");
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
            if (!(element is FenceElement fence)) return;
            _sheetMark?.SetValueWithoutNotify((int)fence.SheetMark);
            _sheetMark?.RefreshShownValue();
        }

        private void OnSheetMarkSelected(int index)
        {
            if (Host.Target is FenceElement fence)
                ChoiceRowUndo.Commit(fence, () => fence.SheetMark = (FenceSheetMark)index);
        }
    }
}
