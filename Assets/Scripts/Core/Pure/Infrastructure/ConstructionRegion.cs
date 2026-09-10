namespace KitchenDesigner.Core
{
    public enum ConstructionRegion
    {
        Centre = 0,
        NorthWest = 1,
        South = 2,
        Volga = 3,
        Urals = 4,
        Siberia = 5,
        FarEast = 6,
        FarNorth = 7,
    }

    public static class ConstructionRegionTitles
    {
        public static readonly string[] All =
        {
            "Центр", "Северо-Запад", "Юг", "Поволжье", "Урал", "Сибирь",
            "Дальний Восток", "Крайний Север",
        };

        public static string Of(ConstructionRegion region)
        {
            int index = (int)region;
            return index >= 0 && index < All.Length ? All[index] : All[(int)ConstructionRegion.Urals];
        }
    }
}
