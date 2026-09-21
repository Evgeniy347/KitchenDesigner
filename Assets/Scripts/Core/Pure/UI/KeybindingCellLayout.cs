namespace KitchenDesigner.Core.UI
{
    public static class KeybindingCellLayout
    {
        public const float DefaultRowWidth = 480f;
        public const float ClearWidth = 18f;
        public const float GapAfterLabel = 6f;
        public const float GapBeforeClear = 3f;
        public const float GapBetweenCells = 10f;
        public const float MinLabelWidth = 120f;

        public const int MinCaptionFontSize = 8;
        public const int MaxCaptionFontSize = 14;
        public const float CharWidthPerPoint = 0.5f;

        public static float WidthForCaption(int captionChars, int fontSize) =>
            captionChars * CharWidthPerPoint * fontSize;

        public static float CellWidth(int longestCaptionChars) =>
            WidthForCaption(longestCaptionChars, MinCaptionFontSize);

        public static float LabelWidth(float rowWidth, float cellWidth) =>
            rowWidth - GapAfterLabel - 2f * cellWidth
                - 2f * (GapBeforeClear + ClearWidth) - GapBetweenCells;

        public static bool Fits(string caption, float cellWidth) =>
            WidthForCaption(caption.Length, MinCaptionFontSize) <= cellWidth;
    }
}
