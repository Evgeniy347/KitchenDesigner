using KitchenDesigner.Core.Construction;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class WallLayerFieldsEditor : NumberFieldsEditor
    {
        public const string ThicknessNode = "CtxWallLayerThickness";
        public const string BattenStepNode = "CtxWallLayerBattenStep";

        public WallLayerFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is WallLayerElement;

        public override void Build()
        {
            var isWallLayer = RowVisibility.When(() => Host.Target is WallLayerElement);
            var isVentGap = RowVisibility.When(() => Host.Target is VentGapElement);

            var thicknessRow = Rows.NumberField(Loc.T("element.wallLayer.thickness"), isWallLayer, Loc.T("unit.mm"), ThicknessNode,
                hint: "element.wallLayer.thickness");
            Bind<WallLayerElement>(thicknessRow, w => w.ThicknessMm, (w, v) => w.ThicknessMm = v,
                WallLayerDefaults.InsulationThicknessMm.ToString());

            var stepRow = Rows.NumberField(Loc.T("element.wallLayer.battenStep"), isVentGap, Loc.T("unit.mm"), BattenStepNode,
                hint: "element.wallLayer.battenStep");
            Bind<VentGapElement>(stepRow, v => v.BattenStepMm, (v, val) => v.BattenStepMm = val,
                WallLayerDefaults.VentGapBattenStepMm.ToString());
        }
    }
}
