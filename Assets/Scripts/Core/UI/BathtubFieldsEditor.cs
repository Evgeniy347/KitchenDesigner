namespace KitchenDesigner.Core.UI
{
    internal sealed class BathtubFieldsEditor : NumberFieldsEditor
    {
        public const string RimWidthNode = "БортВанны";
        public const string BowlDepthNode = "ГлубинаЧашиВанны";
        public const string BowlRadiusNode = "РадиусЧашиВанны";
        public const string BowlFilletNode = "СкруглениеДнаВанны";

        public const string RimWidthLabel = "Ширина борта";
        public const string BowlDepthLabel = "Глубина чаши";
        public const string BowlRadiusLabel = "Радиус чаши";
        public const string BowlFilletLabel = "Скругление дна";

        public BathtubFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is BathtubElement;

        public override void Build()
        {
            var visibility = RowVisibility.When(() => Host.Target is BathtubElement);
            var rimRow = Rows.NumberField(RimWidthLabel, visibility, "мм", RimWidthNode,
                hint: "element.bathtub.rimWidth");
            var bowlDepthRow = Rows.NumberField(BowlDepthLabel, visibility, "мм", BowlDepthNode,
                hint: "element.bathtub.bowlDepth");
            var bowlRadiusRow = Rows.NumberField(BowlRadiusLabel, visibility, "мм",
                BowlRadiusNode);
            var bowlFilletRow = Rows.NumberField(BowlFilletLabel, visibility, "мм",
                BowlFilletNode);

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
