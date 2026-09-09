namespace KitchenDesigner.Core.UI
{
    /// <summary>Не сохраняет обратную совместимость случайно — сохраняет её
    /// НАМЕРЕННО. SidebarUI.cs (соседняя граница) зовёт ровно эту сигнатуру;
    /// вся ветвящаяся логика «какой пресет какому спауну» с 2026-09-10 живёт
    /// внутри самой реализации <see cref="IElementSpawns"/> (см. ElementSpawner
    /// и SidebarThumbnailSpawns), а не здесь — второго дублирующего свитча тут
    /// больше нет.</summary>
    internal static class SidebarSpawnRouter
    {
        public static void Route(SidebarCatalog.Item item, IElementSpawns spawner) =>
            spawner.Spawn(item);
    }
}
