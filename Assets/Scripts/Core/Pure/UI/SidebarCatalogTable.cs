namespace KitchenDesigner.Core.UI
{
    public enum SidebarGroupKey
    {
        Board,
        Facade,
        Drawer,
        Furniture,
        Appliance,
        Sanitary,
        Room,
    }

    public static class SidebarPresetResolution
    {
        public const string DefaultDrawerSystem = "gtv";
        public const string MoventoDrawerSystem = "movento";

        public static DrawerSystem DrawerSystemOf(string drawerSystem) =>
            drawerSystem == MoventoDrawerSystem ? DrawerSystem.Movento : DrawerSystem.Gtv;

        public static DrawerType DrawerTypeOf(string drawerType) => drawerType switch
        {
            "B" => DrawerType.B,
            "C" => DrawerType.C,
            "D" => DrawerType.D,
            _ => DrawerType.A,
        };

        public static DrawerColor DrawerColorOf(string colorName) => colorName.ToLowerInvariant() switch
        {
            "white" => DrawerColor.White,
            "black" => DrawerColor.Black,
            _ => DrawerColor.Anthracite,
        };
    }
}
