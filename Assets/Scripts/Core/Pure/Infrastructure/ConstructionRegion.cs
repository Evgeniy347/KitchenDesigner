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
        private static readonly LocalizedCache<string[]> AllCache =
            new LocalizedCache<string[]>(() => new string[] {
            Loc.T("region.centre"), Loc.T("region.northWest"), Loc.T("region.south"), Loc.T("region.volga"), Loc.T("region.urals"), Loc.T("region.siberia"),
            Loc.T("region.farEast"), Loc.T("region.farNorth"),
        });

        public static string[] All => AllCache.Value;

        public static string Of(ConstructionRegion region)
        {
            int index = (int)region;
            return index >= 0 && index < All.Length ? All[index] : All[(int)ConstructionRegion.Urals];
        }
    }
}
