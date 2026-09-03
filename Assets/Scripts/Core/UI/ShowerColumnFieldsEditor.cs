namespace KitchenDesigner.Core.UI
{
    internal sealed class ShowerColumnFieldsEditor : NumberFieldsEditor
    {
        public const string ColumnHeightNode = "ВысотаДушевойСтойки";
        public const string RiserDiameterNode = "ДиаметрШтангиСтойки";
        public const string HeadDiameterNode = "ДиаметрЛейкиСтойки";
        public const string HeadThicknessNode = "ТолщинаЛейкиСтойки";
        public const string ArmReachNode = "ВыносГусакаСтойки";
        public const string WallOffsetNode = "ВылетСтойкиОтСтены";
        public const string HandDiameterNode = "ДиаметрРучнойЛейки";
        public const string HoseLengthNode = "ДлинаШлангаСтойки";

        public const string ColumnHeightLabel = "Высота стойки";
        public const string RiserDiameterLabel = "Ø штанги";
        public const string HeadDiameterLabel = "Ø лейки";
        public const string HeadThicknessLabel = "Толщина лейки";
        public const string ArmReachLabel = "Вынос лейки";
        public const string WallOffsetLabel = "Вылет от стены";
        public const string HandDiameterLabel = "Ø ручной лейки";
        public const string HoseLengthLabel = "Длина шланга";

        public ShowerColumnFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is ShowerColumnElement;

        public override DimensionPolicy Dimensions => DimensionPolicy.Computed;

        public override void Build()
        {
            var visibility = RowVisibility.When(() => Host.Target is ShowerColumnElement);

            var heightRow = Rows.NumberField(ColumnHeightLabel, visibility, "мм",
                ColumnHeightNode);
            var riserRow = Rows.NumberField(RiserDiameterLabel, visibility, "мм",
                RiserDiameterNode);
            var offsetRow = Rows.NumberField(WallOffsetLabel, visibility, "мм", WallOffsetNode);
            var reachRow = Rows.NumberField(ArmReachLabel, visibility, "мм", ArmReachNode);
            var headRow = Rows.NumberField(HeadDiameterLabel, visibility, "мм", HeadDiameterNode);
            var thicknessRow = Rows.NumberField(HeadThicknessLabel, visibility, "мм",
                HeadThicknessNode);
            var handRow = Rows.NumberField(HandDiameterLabel, visibility, "мм",
                HandDiameterNode);
            var hoseRow = Rows.NumberField(HoseLengthLabel, visibility, "мм", HoseLengthNode);

            Bind(riserRow, RiserOf, SetRiser, ShowerColumnSpec.DefaultRiserDiameterMM.ToString());
            Bind(offsetRow, OffsetOf, SetOffset,
                ShowerColumnSpec.DefaultWallOffsetMM.ToString());
            Bind(heightRow, HeightOf, SetHeight,
                ShowerColumnSpec.DefaultColumnHeightMM.ToString());
            Bind(reachRow, ReachOf, SetReach, ShowerColumnSpec.DefaultArmReachMM.ToString());
            Bind(headRow, HeadOf, SetHead, ShowerColumnSpec.DefaultHeadDiameterMM.ToString());
            Bind(thicknessRow, ThicknessOf, SetThickness,
                ShowerColumnSpec.DefaultHeadThicknessMM.ToString());
            Bind(handRow, HandOf, SetHand,
                ShowerColumnSpec.DefaultHandShowerDiameterMM.ToString());
            Bind(hoseRow, HoseOf, SetHose, ShowerColumnSpec.DefaultHoseLengthMM.ToString());
        }

        private static int HeightOf(KitchenElement element) =>
            element is ShowerColumnElement column
                ? column.ColumnHeightMM
                : ShowerColumnSpec.DefaultColumnHeightMM;

        private static void SetHeight(KitchenElement element, int value)
        {
            if (element is ShowerColumnElement column) column.ColumnHeightMM = value;
        }

        private static int RiserOf(KitchenElement element) =>
            element is ShowerColumnElement column
                ? column.RiserDiameterMM
                : ShowerColumnSpec.DefaultRiserDiameterMM;

        private static void SetRiser(KitchenElement element, int value)
        {
            if (element is ShowerColumnElement column) column.RiserDiameterMM = value;
        }

        private static int HeadOf(KitchenElement element) =>
            element is ShowerColumnElement column
                ? column.HeadDiameterMM
                : ShowerColumnSpec.DefaultHeadDiameterMM;

        private static void SetHead(KitchenElement element, int value)
        {
            if (element is ShowerColumnElement column) column.HeadDiameterMM = value;
        }

        private static int ThicknessOf(KitchenElement element) =>
            element is ShowerColumnElement column
                ? column.HeadThicknessMM
                : ShowerColumnSpec.DefaultHeadThicknessMM;

        private static void SetThickness(KitchenElement element, int value)
        {
            if (element is ShowerColumnElement column) column.HeadThicknessMM = value;
        }

        private static int ReachOf(KitchenElement element) =>
            element is ShowerColumnElement column
                ? column.ArmReachMM
                : ShowerColumnSpec.DefaultArmReachMM;

        private static void SetReach(KitchenElement element, int value)
        {
            if (element is ShowerColumnElement column) column.ArmReachMM = value;
        }

        private static int OffsetOf(KitchenElement element) =>
            element is ShowerColumnElement column
                ? column.WallOffsetMM
                : ShowerColumnSpec.DefaultWallOffsetMM;

        private static void SetOffset(KitchenElement element, int value)
        {
            if (element is ShowerColumnElement column) column.WallOffsetMM = value;
        }

        private static int HandOf(KitchenElement element) =>
            element is ShowerColumnElement column
                ? column.HandShowerDiameterMM
                : ShowerColumnSpec.DefaultHandShowerDiameterMM;

        private static void SetHand(KitchenElement element, int value)
        {
            if (element is ShowerColumnElement column) column.HandShowerDiameterMM = value;
        }

        private static int HoseOf(KitchenElement element) =>
            element is ShowerColumnElement column
                ? column.HoseLengthMM
                : ShowerColumnSpec.DefaultHoseLengthMM;

        private static void SetHose(KitchenElement element, int value)
        {
            if (element is ShowerColumnElement column) column.HoseLengthMM = value;
        }
    }
}
