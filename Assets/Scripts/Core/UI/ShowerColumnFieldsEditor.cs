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
            var offsetRow = Rows.NumberField(WallOffsetLabel, visibility, "мм", WallOffsetNode,
                hint: "element.showerColumn.wallOffset");
            var reachRow = Rows.NumberField(ArmReachLabel, visibility, "мм", ArmReachNode,
                hint: "element.showerColumn.armReach");
            var headRow = Rows.NumberField(HeadDiameterLabel, visibility, "мм", HeadDiameterNode);
            var thicknessRow = Rows.NumberField(HeadThicknessLabel, visibility, "мм",
                HeadThicknessNode);
            var handRow = Rows.NumberField(HandDiameterLabel, visibility, "мм",
                HandDiameterNode);
            var hoseRow = Rows.NumberField(HoseLengthLabel, visibility, "мм", HoseLengthNode);

            Bind<ShowerColumnElement>(riserRow, column => column.RiserDiameterMM,
                (column, value) => column.RiserDiameterMM = value,
                ShowerColumnSpec.DefaultRiserDiameterMM.ToString());
            Bind<ShowerColumnElement>(offsetRow, column => column.WallOffsetMM,
                (column, value) => column.WallOffsetMM = value,
                ShowerColumnSpec.DefaultWallOffsetMM.ToString());
            Bind<ShowerColumnElement>(heightRow, column => column.ColumnHeightMM,
                (column, value) => column.ColumnHeightMM = value,
                ShowerColumnSpec.DefaultColumnHeightMM.ToString());
            Bind<ShowerColumnElement>(reachRow, column => column.ArmReachMM,
                (column, value) => column.ArmReachMM = value,
                ShowerColumnSpec.DefaultArmReachMM.ToString());
            Bind<ShowerColumnElement>(headRow, column => column.HeadDiameterMM,
                (column, value) => column.HeadDiameterMM = value,
                ShowerColumnSpec.DefaultHeadDiameterMM.ToString());
            Bind<ShowerColumnElement>(thicknessRow, column => column.HeadThicknessMM,
                (column, value) => column.HeadThicknessMM = value,
                ShowerColumnSpec.DefaultHeadThicknessMM.ToString());
            Bind<ShowerColumnElement>(handRow, column => column.HandShowerDiameterMM,
                (column, value) => column.HandShowerDiameterMM = value,
                ShowerColumnSpec.DefaultHandShowerDiameterMM.ToString());
            Bind<ShowerColumnElement>(hoseRow, column => column.HoseLengthMM,
                (column, value) => column.HoseLengthMM = value,
                ShowerColumnSpec.DefaultHoseLengthMM.ToString());
        }
    }
}
