namespace KitchenDesigner.Core.UI
{
    public readonly struct KeybindingRowRuler
    {
        public readonly float RowWidth;
        public readonly float LabelWidth;
        public readonly float CellWidth;

        private KeybindingRowRuler(float rowWidth, float labelWidth, float cellWidth)
        {
            RowWidth = rowWidth;
            LabelWidth = labelWidth;
            CellWidth = cellWidth;
        }

        public static KeybindingRowRuler For(float rowWidth, float longestCaptionWidth)
        {
            float cell = KeybindingCellLayout.CellWidth(rowWidth, longestCaptionWidth);
            return new KeybindingRowRuler(rowWidth,
                KeybindingCellLayout.LabelWidth(rowWidth, cell), cell);
        }

        public float MarkerWidth => KeybindingCellLayout.MarkerLaneWidth;

        public float ClearWidth => KeybindingCellLayout.ClearWidth;

        public float LabelLeft => -RowWidth * 0.5f;

        public float PrimaryMarkerLeft =>
            LabelLeft + LabelWidth + KeybindingCellLayout.GapAfterLabel;

        public float PrimaryCellLeft => PrimaryMarkerLeft + MarkerWidth;

        public float PrimaryClearLeft =>
            PrimaryCellLeft + CellWidth + KeybindingCellLayout.GapBeforeClear;

        public float AltMarkerLeft =>
            PrimaryClearLeft + ClearWidth + KeybindingCellLayout.GapBetweenCells;

        public float AltCellLeft => AltMarkerLeft + MarkerWidth;

        public float AltClearLeft =>
            AltCellLeft + CellWidth + KeybindingCellLayout.GapBeforeClear;

        public float RightEdge => AltClearLeft + ClearWidth;

        public float MarkerLeft(bool primary) => primary ? PrimaryMarkerLeft : AltMarkerLeft;

        public float CellLeft(bool primary) => primary ? PrimaryCellLeft : AltCellLeft;

        public float ClearLeft(bool primary) => primary ? PrimaryClearLeft : AltClearLeft;

        public float ColumnHeaderLeft(bool primary) => MarkerLeft(primary);

        public float ColumnHeaderWidth =>
            MarkerWidth + CellWidth + KeybindingCellLayout.GapBeforeClear + ClearWidth;
    }
}
