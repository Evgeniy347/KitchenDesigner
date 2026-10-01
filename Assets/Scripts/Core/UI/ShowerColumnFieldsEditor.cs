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

        public static string ColumnHeightLabel => Loc.T("element.showerColumn.columnHeight");
        public static string RiserDiameterLabel => Loc.T("element.showerColumn.riserDiameter");
        public static string HeadDiameterLabel => Loc.T("element.showerColumn.headDiameter");
        public static string HeadThicknessLabel => Loc.T("element.showerColumn.headThickness");
        public static string ArmReachLabel => Loc.T("element.showerColumn.armReach");
        public static string WallOffsetLabel => Loc.T("element.showerColumn.wallOffset");
        public static string HandDiameterLabel => Loc.T("element.showerColumn.handDiameter");
        public static string HoseLengthLabel => Loc.T("element.showerColumn.hoseLength");

        public ShowerColumnFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is ShowerColumnElement;

        public override DimensionPolicy Dimensions => DimensionPolicy.Computed;

        public override void Build()
        {
            var visibility = RowVisibility.When(() => Host.Target is ShowerColumnElement);

            var heightRow = Rows.NumberField(ColumnHeightLabel, visibility, Loc.T("unit.mm"),
                ColumnHeightNode, hint: "element.showerColumn.columnHeight");
            var riserRow = Rows.NumberField(RiserDiameterLabel, visibility, Loc.T("unit.mm"),
                RiserDiameterNode, hint: "element.showerColumn.riserDiameter");
            var offsetRow = Rows.NumberField(WallOffsetLabel, visibility, Loc.T("unit.mm"), WallOffsetNode,
                hint: "element.showerColumn.wallOffset");
            var reachRow = Rows.NumberField(ArmReachLabel, visibility, Loc.T("unit.mm"), ArmReachNode,
                hint: "element.showerColumn.armReach");
            var headRow = Rows.NumberField(HeadDiameterLabel, visibility, Loc.T("unit.mm"), HeadDiameterNode,
                hint: "element.showerColumn.headDiameter");
            var thicknessRow = Rows.NumberField(HeadThicknessLabel, visibility, Loc.T("unit.mm"),
                HeadThicknessNode, hint: "element.showerColumn.headThickness");
            var handRow = Rows.NumberField(HandDiameterLabel, visibility, Loc.T("unit.mm"),
                HandDiameterNode, hint: "element.showerColumn.handDiameter");
            var hoseRow = Rows.NumberField(HoseLengthLabel, visibility, Loc.T("unit.mm"), HoseLengthNode,
                hint: "element.showerColumn.hoseLength");

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
