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
            var rimRow = Rows.NumberField(RimWidthLabel, visibility, "мм", RimWidthNode);
            var bowlDepthRow = Rows.NumberField(BowlDepthLabel, visibility, "мм", BowlDepthNode);
            var bowlRadiusRow = Rows.NumberField(BowlRadiusLabel, visibility, "мм",
                BowlRadiusNode);
            var bowlFilletRow = Rows.NumberField(BowlFilletLabel, visibility, "мм",
                BowlFilletNode);

            Bind(rimRow, RimOf, SetRim, BathtubLayout.DefaultRimWidthMM.ToString());
            Bind(bowlDepthRow, BowlDepthOf, SetBowlDepth,
                BathtubLayout.DefaultBowlDepthMM.ToString());
            Bind(bowlRadiusRow, BowlRadiusOf, SetBowlRadius,
                BathtubLayout.DefaultBowlRadiusMM.ToString());
            Bind(bowlFilletRow, BowlFilletOf, SetBowlFillet,
                BathtubLayout.DefaultBowlFilletMM.ToString());
        }

        private static int RimOf(KitchenElement element) =>
            element is BathtubElement tub ? tub.RimWidthMM : BathtubLayout.DefaultRimWidthMM;

        private static void SetRim(KitchenElement element, int value)
        {
            if (element is BathtubElement tub) tub.RimWidthMM = value;
        }

        private static int BowlDepthOf(KitchenElement element) =>
            element is BathtubElement tub ? tub.BowlDepthMM : BathtubLayout.DefaultBowlDepthMM;

        private static void SetBowlDepth(KitchenElement element, int value)
        {
            if (element is BathtubElement tub) tub.BowlDepthMM = value;
        }

        private static int BowlRadiusOf(KitchenElement element) =>
            element is BathtubElement tub ? tub.BowlRadiusMM : BathtubLayout.DefaultBowlRadiusMM;

        private static void SetBowlRadius(KitchenElement element, int value)
        {
            if (element is BathtubElement tub) tub.BowlRadiusMM = value;
        }

        private static int BowlFilletOf(KitchenElement element) =>
            element is BathtubElement tub ? tub.BowlFilletMM : BathtubLayout.DefaultBowlFilletMM;

        private static void SetBowlFillet(KitchenElement element, int value)
        {
            if (element is BathtubElement tub) tub.BowlFilletMM = value;
        }
    }
}
