namespace KitchenDesigner.Core
{
    public enum SoilKind
    {
        Sand = 0,
        SandyLoam = 1,
        Loam = 2,
        Clay = 3,
        Peat = 4,
        Unknown = 5,
    }

    public static class SoilKindTitles
    {
        public static readonly string[] All =
        {
            "Песок", "Супесь", "Суглинок", "Глина", "Торф", "Неизвестно",
        };

        public static string Of(SoilKind soil)
        {
            int index = (int)soil;
            return index >= 0 && index < All.Length ? All[index] : All[(int)SoilKind.Unknown];
        }
    }
}
