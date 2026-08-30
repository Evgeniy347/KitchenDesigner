namespace KitchenDesigner.Core.UI
{
    internal static class ContextMenuMetrics
    {
        public const float PanelWidth = 364f;
        public const float PanelPadding = 10f;
        public const float ContentHalfWidth = PanelWidth / 2f - PanelPadding;
        public const float RowWidth = 332f;

        public const float LabelW = 140f;
        public const float LabelX = -80f;
        public const float FieldX = 100f;

        public const float NameLabelW = 76f;
        public const float NameLabelX = -ContentHalfWidth + NameLabelW / 2f;
        private const float NameFieldLeft = -ContentHalfWidth + NameLabelW;
        public const float NameFieldW = ContentHalfWidth - NameFieldLeft;
        public const float NameFieldX = (NameFieldLeft + ContentHalfWidth) / 2f;

        public const float LabelH = 24f;
        public const float FieldH = 24f;
        public const float RowH = 24f;
        public const float RowGap = 7f;
        public const float TitleH = 28f;
        public const float TitleGap = 8f;
        public const float RotLblH = 22f;
        public const float RotLblGap = 4f;
        public const float BtnH = 28f;
        public const float ActionGap = 8f;
        public const float TopPad = 12f;
        public const float BottomPad = 12f;

        public const float TriCol1 = -110f;
        public const float TriCol2 = 0f;
        public const float TriCol3 = 110f;
        public const float TriLabelW = 95f;
        public const float TriFieldW = 70f;
        public const float TriLabelH = 18f;
    }
}
