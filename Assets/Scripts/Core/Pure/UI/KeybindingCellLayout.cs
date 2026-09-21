namespace KitchenDesigner.Core.UI
{
    public static class KeybindingCellLayout
    {
        public const float DefaultRowWidth = 480f;
        public const float ClearWidth = 18f;
        public const float GapAfterLabel = 6f;
        public const float GapBeforeClear = 3f;
        public const float GapBetweenCells = 10f;
        public const float HintLaneWidth = 32f;
        public const float MarkerLaneWidth = 10f;

        public const float MinLabelWidth = 150f;
        public const float MinCellWidth = 60f;
        public const float CellPadding = 6f;

        public const int MinCaptionFontSize = 8;
        public const int MaxCaptionFontSize = 14;
        public const float CharWidthPerPoint = 0.5f;

        public static float WidthForCaption(int captionChars, int fontSize) =>
            captionChars * CharWidthPerPoint * fontSize;

        public static float NeededCellWidth(int captionChars) =>
            WidthForCaption(captionChars, MaxCaptionFontSize) + CellPadding;

        public static float SpareForCells(float rowWidth, float labelWidth) =>
            (rowWidth - GapAfterLabel - labelWidth
                - 2f * (MarkerLaneWidth + GapBeforeClear + ClearWidth) - GapBetweenCells) * 0.5f;

        public static float CellWidth(float rowWidth, int longestBoundCaptionChars)
        {
            float needed = NeededCellWidth(longestBoundCaptionChars);
            float ceiling = SpareForCells(rowWidth, MinLabelWidth);
            if (needed > ceiling) return ceiling;
            return needed < MinCellWidth ? MinCellWidth : needed;
        }

        public static float LabelWidth(float rowWidth, float cellWidth) =>
            rowWidth - GapAfterLabel - 2f * cellWidth
                - 2f * (MarkerLaneWidth + GapBeforeClear + ClearWidth) - GapBetweenCells;

        public static float LabelTextWidth(float labelWidth, bool hasHint) =>
            hasHint ? labelWidth - HintLaneWidth : labelWidth;

        public static float HintBadgeCentreX(float labelTextWidth) =>
            labelTextWidth * 0.5f + HintLaneWidth * 0.5f;

        public static bool MarkerFitsItsLane() =>
            WidthForCaption(KeybindingCaption.MarkerText.Length, MaxCaptionFontSize)
                <= MarkerLaneWidth;

        public static int FontSizeFor(int captionChars, float areaWidth)
        {
            if (captionChars <= 0) return MaxCaptionFontSize;

            int fits = (int)(areaWidth / (captionChars * CharWidthPerPoint));
            if (fits > MaxCaptionFontSize) return MaxCaptionFontSize;
            return fits < MinCaptionFontSize ? MinCaptionFontSize : fits;
        }

        public static bool Fits(string caption, float cellWidth) =>
            WidthForCaption(caption.Length, MinCaptionFontSize) <= cellWidth;

        public static bool FitsComfortably(string caption, float cellWidth) =>
            WidthForCaption(caption.Length, MaxCaptionFontSize) <= cellWidth;
    }
}
