namespace KitchenDesigner.Core.UI
{
    internal static class SceneTreeMetrics
    {
        public const float PanelH = 560f;
        public const float MinPanelH = 240f;
        public const float SearchH = UIStyle.ControlHCompact;
        public const float MoveToW = 160f;
        public const float MenuSlotW = UIStyle.TreeRowH - UIStyle.Space1;
        public const float CountW = UIStyle.Space6;
        public const float RowEndPad = WindowBody.BarW;
        public const float FoldHitW = UIStyle.ChevronW + 2f * UIStyle.Space1;

        public static readonly TreeMetrics Layout = new TreeMetrics(UIStyle.Space1, UIStyle.TreeIndent,
            UIStyle.ChevronW, UIStyle.Space1, UIStyle.TreeIconSize, UIStyle.Space2);

        public static float SearchTop => UIStyle.TitleBarH + UIStyle.Space2;

        public static float TreeTop => SearchTop + SearchH + UIStyle.Space2;

        public static float RowReserve(TreeRowKind kind) => kind switch
        {
            TreeRowKind.Group => CountW + MenuSlotW + UIStyle.Space1 + RowEndPad,
            TreeRowKind.Level => CountW + RowEndPad,
            _ => RowEndPad,
        };
    }
}
