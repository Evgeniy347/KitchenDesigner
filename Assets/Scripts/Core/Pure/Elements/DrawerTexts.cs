namespace KitchenDesigner.Core
{
    public static class DrawerTexts
    {
        public static string TypeLabel(DrawerType type) => type switch
        {
            DrawerType.A => Loc.T("drawer.type.a"),
            DrawerType.B => Loc.T("drawer.type.b"),
            DrawerType.C => Loc.T("drawer.type.c"),
            DrawerType.D => Loc.T("drawer.type.d"),
            _ => type.ToString(),
        };

        public static string DefaultName(DrawerSystem system) => system == DrawerSystem.Movento
            ? Loc.T("elementType.drawerMovento")
            : Loc.T("elementType.drawerGtv");

        public static string ColorName(DrawerColor color) => color switch
        {
            DrawerColor.Anthracite => Loc.T("drawer.color.anthracite"),
            DrawerColor.White => Loc.T("drawer.color.white"),
            DrawerColor.Black => Loc.T("drawer.color.black"),
            _ => color.ToString(),
        };

        public static string CycleButtonLabel(DoubleDrawerState state) => state switch
        {
            DoubleDrawerState.Closed => Loc.T("drawer.double.openBoth"),
            DoubleDrawerState.BothOpen => Loc.T("drawer.double.closeUpper"),
            DoubleDrawerState.LowerOnly => Loc.T("drawer.double.closeAll"),
            _ => state.ToString(),
        };
    }
}
