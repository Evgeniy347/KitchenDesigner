namespace KitchenDesigner.Core.UI
{
    internal static class SidebarSpawnRouter
    {
        public static void Route(SidebarCatalog.Item item, IElementSpawns spawner) =>
            spawner.Spawn(item);
    }
}
