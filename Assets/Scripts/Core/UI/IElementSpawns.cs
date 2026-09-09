namespace KitchenDesigner.Core.UI
{
    /// <summary>Единственная дверь от строки каталога до фабрики. Раньше здесь
    /// был метод на каждый вид (SpawnBoard, SpawnFacade, SpawnDrawer, ...) —
    /// новый параметр у одного пресета (например, ещё одна опция ящика) менял
    /// сигнатуру и здесь, и в SidebarSpawnRouter, и в ОБЕИХ реализациях сразу.
    /// Теперь реализация получает пресет целиком (<see cref="SidebarCatalog.Item"/>)
    /// и сама решает, какие поля ей нужны — новое значение существующего поля
    /// пресета не трогает эту сигнатуру вовсе.</summary>
    internal interface IElementSpawns
    {
        void Spawn(SidebarCatalog.Item item);
    }
}
