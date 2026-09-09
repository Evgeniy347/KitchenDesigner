namespace KitchenDesigner.Core.UI
{
    public readonly struct SidebarCatalogRow
    {
        public readonly SidebarGroupKey Group;
        public readonly string? TileTitle;
        public readonly SidebarCatalog.Item Item;

        private SidebarCatalogRow(SidebarGroupKey group, string? tileTitle, SidebarCatalog.Item item)
        {
            Group = group;
            TileTitle = tileTitle;
            Item = item;
        }

        public static SidebarCatalogRow TypeRow(SidebarGroupKey group, string title,
            SidebarCatalog.Item defaultPreset) =>
            new SidebarCatalogRow(group, title, defaultPreset);

        public static SidebarCatalogRow PresetRow(SidebarGroupKey group, SidebarCatalog.Item preset) =>
            new SidebarCatalogRow(group, null, preset);

        public bool IsTypeRow => TileTitle != null;
    }
}
