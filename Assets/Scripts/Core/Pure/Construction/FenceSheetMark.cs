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
        public static readonly string[] All = { "С8", "С20", "НС35" };

        public static string Of(FenceSheetMark mark)
        {
            int index = (int)mark;
            return index >= 0 && index < All.Length ? All[index] : All[(int)FenceSheetMark.C8];
        }
    }
}
