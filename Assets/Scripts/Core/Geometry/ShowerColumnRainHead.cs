using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ShowerColumnRainHead
    {
        public const float BossRadiusRatio = 1.4f;
        public const float BossLengthRatio = 0.35f;
        public const float RimRadiusRatio = 0.5f;
        public const float ChamferRadiusRatio = 0.46f;
        public const float ChamferRatio = 0.12f;

        public static float BossLengthMM(ShowerColumnSpec spec) =>
            spec.HeadThicknessMM * BossLengthRatio;

        public static float ChamferMM(ShowerColumnSpec spec) =>
            spec.HeadThicknessMM * ChamferRatio;

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
                new Vector3(0f, DiscTopYMM(spec) - ChamferMM(spec), spec.ArmReachMM),
                new Vector3(0f, DiscTopYMM(spec), spec.ArmReachMM),
                spec.HeadDiameterMM * RimRadiusRatio,
                spec.HeadDiameterMM * ChamferRadiusRatio);

        public static PipeSegment Rim(ShowerColumnSpec spec) =>
            new PipeSegment(
                new Vector3(0f, BottomYMM(spec) + ChamferMM(spec), spec.ArmReachMM),
                new Vector3(0f, DiscTopYMM(spec) - ChamferMM(spec), spec.ArmReachMM),
                spec.HeadDiameterMM * RimRadiusRatio);

        public static PipeSegment BottomChamfer(ShowerColumnSpec spec) =>
            new PipeSegment(
                new Vector3(0f, BottomYMM(spec), spec.ArmReachMM),
                new Vector3(0f, BottomYMM(spec) + ChamferMM(spec), spec.ArmReachMM),
                spec.HeadDiameterMM * ChamferRadiusRatio,
                spec.HeadDiameterMM * RimRadiusRatio);

        public static PipeSegment[] Parts(ShowerColumnSpec spec) =>
            new[] { BottomChamfer(spec), Rim(spec), TopChamfer(spec), Boss(spec) };
    }
}
