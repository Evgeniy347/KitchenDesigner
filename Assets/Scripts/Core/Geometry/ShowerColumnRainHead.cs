using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ShowerColumnRainHead
    {
        public const float BossRadiusRatio = 1.4f;
        public const float BossLengthRatio = 0.5f;
        public const float RimRadiusRatio = 0.5f;
        public const float TopChamferRadiusRatio = 0.485f;
        public const float BottomChamferRadiusRatio = 0.44f;
        public const float TopChamferRatio = 0.06f;
        public const float BottomChamferRatio = 0.16f;

        public static float BossLengthMM(ShowerColumnSpec spec) =>
            spec.HeadThicknessMM * BossLengthRatio;

        public static float TopChamferMM(ShowerColumnSpec spec) =>
            spec.HeadThicknessMM * TopChamferRatio;

        public static float BottomChamferMM(ShowerColumnSpec spec) =>
            spec.HeadThicknessMM * BottomChamferRatio;

        public static float TopYMM(ShowerColumnSpec spec) =>
            ShowerColumnLayout.ArmAxisYMM(spec) - ShowerColumnLayout.RiserRadiusMM(spec);

        public static float DiscTopYMM(ShowerColumnSpec spec) =>
            TopYMM(spec) - BossLengthMM(spec);

        public static float BottomYMM(ShowerColumnSpec spec) =>
            DiscTopYMM(spec) - spec.HeadThicknessMM;

        public static PipeSegment Boss(ShowerColumnSpec spec) =>
            new PipeSegment(
                new Vector3(0f, DiscTopYMM(spec), spec.ArmReachMM),
                new Vector3(0f, TopYMM(spec), spec.ArmReachMM),
                ShowerColumnLayout.RiserRadiusMM(spec) * BossRadiusRatio);

        public static PipeSegment TopChamfer(ShowerColumnSpec spec) =>
            new PipeSegment(
                new Vector3(0f, DiscTopYMM(spec) - TopChamferMM(spec), spec.ArmReachMM),
                new Vector3(0f, DiscTopYMM(spec), spec.ArmReachMM),
                spec.HeadDiameterMM * RimRadiusRatio,
                spec.HeadDiameterMM * TopChamferRadiusRatio);

        public static PipeSegment Rim(ShowerColumnSpec spec) =>
            new PipeSegment(
                new Vector3(0f, BottomYMM(spec) + BottomChamferMM(spec), spec.ArmReachMM),
                new Vector3(0f, DiscTopYMM(spec) - TopChamferMM(spec), spec.ArmReachMM),
                spec.HeadDiameterMM * RimRadiusRatio);

        public static PipeSegment BottomChamfer(ShowerColumnSpec spec) =>
            new PipeSegment(
                new Vector3(0f, BottomYMM(spec), spec.ArmReachMM),
                new Vector3(0f, BottomYMM(spec) + BottomChamferMM(spec), spec.ArmReachMM),
                spec.HeadDiameterMM * BottomChamferRadiusRatio,
                spec.HeadDiameterMM * RimRadiusRatio);

        public static PipeSegment[] Parts(ShowerColumnSpec spec) =>
            new[] { BottomChamfer(spec), Rim(spec), TopChamfer(spec), Boss(spec) };
    }
}
