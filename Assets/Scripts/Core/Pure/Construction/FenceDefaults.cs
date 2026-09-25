namespace KitchenDesigner.Core.Construction
{
    public static class FenceDefaults
    {
        public const int PostSectionMm = 60; // ГОСТ 8639-82
        public const int MinPostSectionMm = 40;
        public const int MaxPostSectionMm = 120;

        public const int PostStepMm = 2500;
        public const int MinPostStepMm = 2000;
        public const int MaxPostStepMm = 3000;

        public const int PitDepthMm = 1200; // СП 22.13330.2016
        public const int MinPitDepthMm = 600;
        public const int MaxPitDepthMm = 2000;

        public const FenceSheetMark SheetMark = FenceSheetMark.C8; // ГОСТ 24045-2016
        public const int SheetWorkingWidthMm = 1150; // ГОСТ 24045-2016
        public const int SheetThicknessMm = 8;
    }
}
