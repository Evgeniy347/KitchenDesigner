namespace KitchenDesigner.Core.Construction
{
    public static class RoofDefaults
    {
        public const RoofType Type = RoofType.Gable;
        public const RoofRidgeAxis RidgeAxis = RoofRidgeAxis.Auto;

        public const float PitchDeg = 30f;
        public const float MinPitchDeg = 5f;
        public const float MaxPitchDeg = 60f;

        public const int OverhangMm = 500;
        public const int MinOverhangMm = 0;
        public const int MaxOverhangMm = 1000;

        public const int RafterStepMm = 600;
        public const int MinRafterStepMm = 300;
        public const int MaxRafterStepMm = 1000;

        public const float CoveringWastePct = 10f;
    }
}
