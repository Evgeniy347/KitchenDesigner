namespace KitchenDesigner.Core
{
    public static class NeighbourLevelsModeTitles
    {
        private static readonly LocalizedCache<string[]> AllCache =
            new LocalizedCache<string[]>(() => new string[] { Loc.T("level.neighbours.show"), Loc.T("level.neighbours.dim"), Loc.T("level.neighbours.hide") });

        public static string[] All => AllCache.Value;

        public static string Of(NeighbourLevelsMode mode)
        {
            int index = (int)mode;
            return index >= 0 && index < All.Length ? All[index] : All[(int)NeighbourLevelsMode.Show];
        }
    }
}
