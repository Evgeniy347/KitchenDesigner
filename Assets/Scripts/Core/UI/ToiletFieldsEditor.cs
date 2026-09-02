namespace KitchenDesigner.Core.UI
{
    internal sealed class ToiletFieldsEditor : NumberFieldsEditor
    {
        public const string SeatHeightNode = "ВысотаЧашиУнитаза";
        public const string FlushPlateHeightNode = "ВысотаПанелиСмыва";

        public const string SeatHeightLabel = "Высота чаши";
        public const string FlushPlateHeightLabel = "Высота панели";

        public ToiletFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element)
            => element is ToiletElement || element is WallHungToiletElement;

        public override void Build()
        {
            var anyToilet = RowVisibility.When(() => Host.Target is ToiletElement
                || Host.Target is WallHungToiletElement);
            var wallHungOnly = RowVisibility.When(() => Host.Target is WallHungToiletElement);

            var seatRow = Rows.NumberField(SeatHeightLabel, anyToilet, "мм", SeatHeightNode);
            var plateRow = Rows.NumberField(FlushPlateHeightLabel, wallHungOnly, "мм",
                FlushPlateHeightNode);

            Bind(seatRow, SeatHeightOf, SetSeatHeight,
                ToiletLayout.DefaultSeatHeightMM.ToString());
            Bind(plateRow, FlushPlateHeightOf, SetFlushPlateHeight,
                WallHungToiletLayout.DefaultPlateBottomMM.ToString());
        }

        private static int SeatHeightOf(KitchenElement element) => element switch
        {
            ToiletElement toilet => toilet.SeatHeightMM,
            WallHungToiletElement wallHung => wallHung.SeatHeightMM,
            _ => ToiletLayout.DefaultSeatHeightMM,
        };

        private static void SetSeatHeight(KitchenElement element, int value)
        {
            if (element is ToiletElement toilet) toilet.SeatHeightMM = value;
            else if (element is WallHungToiletElement wallHung) wallHung.SeatHeightMM = value;
        }

        private static int FlushPlateHeightOf(KitchenElement element)
            => element is WallHungToiletElement wallHung
                ? wallHung.FlushPlateHeightMM
                : WallHungToiletLayout.DefaultPlateBottomMM;

        private static void SetFlushPlateHeight(KitchenElement element, int value)
        {
            if (element is WallHungToiletElement wallHung) wallHung.FlushPlateHeightMM = value;
        }
    }
}
