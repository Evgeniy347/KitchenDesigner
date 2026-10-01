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
        private static readonly LocalizedCache<string[]> AllCache =
            new LocalizedCache<string[]>(() => new string[] {
            Loc.T("soil.sand"), Loc.T("soil.sandyLoam"), Loc.T("soil.loam"), Loc.T("soil.clay"), Loc.T("soil.peat"), Loc.T("soil.unknown"),
        });

        public static string[] All => AllCache.Value;

        public static string Of(SoilKind soil)
        {
            int index = (int)soil;
            return index >= 0 && index < All.Length ? All[index] : All[(int)SoilKind.Unknown];
        }
    }
}
