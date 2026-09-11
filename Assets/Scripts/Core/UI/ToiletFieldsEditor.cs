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

            var seatRow = Rows.NumberField(SeatHeightLabel, anyToilet, "мм", SeatHeightNode,
                hint: "element.toilet.seatHeight");
            var plateRow = Rows.NumberField(FlushPlateHeightLabel, wallHungOnly, "мм",
                FlushPlateHeightNode, hint: "element.toilet.flushPlate");

            Bind<ToiletElement>(seatRow, toilet => toilet.SeatHeightMM,
                    (toilet, value) => toilet.SeatHeightMM = value,
                    ToiletLayout.DefaultSeatHeightMM.ToString())
                .Or<WallHungToiletElement>(wallHung => wallHung.SeatHeightMM,
                    (wallHung, value) => wallHung.SeatHeightMM = value);
            Bind<WallHungToiletElement>(plateRow, wallHung => wallHung.FlushPlateHeightMM,
                (wallHung, value) => wallHung.FlushPlateHeightMM = value,
                WallHungToiletLayout.DefaultPlateBottomMM.ToString());
        }
    }
}
