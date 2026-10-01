namespace KitchenDesigner.Core.Construction
{
    public enum FenceSheetMark
    {
        C8 = 0,
        C20 = 1,
        HC35 = 2,
    }

    public static class FenceSheetMarkTitles
    {
        private static readonly LocalizedCache<string[]> AllCache =
            new LocalizedCache<string[]>(() => new string[] { Loc.T("construction.fenceSheet.c8"), Loc.T("construction.fenceSheet.c20"), Loc.T("construction.fenceSheet.hc35") });

        public static string[] All => AllCache.Value;

        public static string Of(FenceSheetMark mark)
        {
            int index = (int)mark;
            return index >= 0 && index < All.Length ? All[index] : All[(int)FenceSheetMark.C8];
        }
    }
}
