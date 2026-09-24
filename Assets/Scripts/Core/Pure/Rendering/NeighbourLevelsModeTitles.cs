namespace KitchenDesigner.Core
{
    public static class NeighbourLevelsModeTitles
    {
        public static readonly string[] All = { "Показывать", "Приглушать", "Скрывать" };

        public static string Of(NeighbourLevelsMode mode)
        {
            int index = (int)mode;
            return index >= 0 && index < All.Length ? All[index] : All[(int)NeighbourLevelsMode.Show];
        }
    }
}
