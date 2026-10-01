namespace KitchenDesigner.Core.UI
{
    internal sealed class BathtubFieldsEditor : NumberFieldsEditor
    {
        public const string RimWidthNode = "БортВанны";
        public const string BowlDepthNode = "ГлубинаЧашиВанны";
        public const string BowlRadiusNode = "РадиусЧашиВанны";
        public const string BowlFilletNode = "СкруглениеДнаВанны";

        public static string RimWidthLabel => Loc.T("element.bathtub.rimWidth");
        public static string BowlDepthLabel => Loc.T("element.bathtub.bowlDepth");
        public static string BowlRadiusLabel => Loc.T("element.bathtub.bowlRadius");
        public static string BowlFilletLabel => Loc.T("element.bathtub.bowlFillet");

        public BathtubFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is BathtubElement;

        public override void Build()
        {
            var visibility = RowVisibility.When(() => Host.Target is BathtubElement);
            var rimRow = Rows.NumberField(RimWidthLabel, visibility, Loc.T("unit.mm"), RimWidthNode,
                hint: "element.bathtub.rimWidth");
            var bowlDepthRow = Rows.NumberField(BowlDepthLabel, visibility, Loc.T("unit.mm"), BowlDepthNode,
                hint: "element.bathtub.bowlDepth");
            var bowlRadiusRow = Rows.NumberField(BowlRadiusLabel, visibility, Loc.T("unit.mm"),
                BowlRadiusNode, hint: "element.bathtub.bowlRadius");
            var bowlFilletRow = Rows.NumberField(BowlFilletLabel, visibility, Loc.T("unit.mm"),
                BowlFilletNode, hint: "element.bathtub.bowlFillet");

            Bind<BathtubElement>(rimRow, tub => tub.RimWidthMM,
                (tub, value) => tub.RimWidthMM = value,
                BathtubLayout.DefaultRimWidthMM.ToString());
            Bind<BathtubElement>(bowlDepthRow, tub => tub.BowlDepthMM,
                (tub, value) => tub.BowlDepthMM = value,
                BathtubLayout.DefaultBowlDepthMM.ToString());
            Bind<BathtubElement>(bowlRadiusRow, tub => tub.BowlRadiusMM,
                (tub, value) => tub.BowlRadiusMM = value,
                BathtubLayout.DefaultBowlRadiusMM.ToString());
            Bind<BathtubElement>(bowlFilletRow, tub => tub.BowlFilletMM,
                (tub, value) => tub.BowlFilletMM = value,
                BathtubLayout.DefaultBowlFilletMM.ToString());
        }
    }
}
