using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ShowerColumnHandShower
    {
        public const float TiltDeg = 14f;
        public const float GripLengthRatio = 2.2f;
        public const float GripBelowHolderRatio = 0.4f;
        public const float GripBottomRadiusRatio = 0.12f;
        public const float GripTopRadiusRatio = 0.16f;
        public const float NeckRadiusRatio = 0.2f;
        public const float NeckLengthRatio = 0.09f;
        public const float HeadThicknessRatio = 0.2f;
        public const float HeadFlareRatio = 0.3f;
        public const float HeadRadiusRatio = 0.5f;
        public const float HeadFaceRadiusRatio = 0.46f;

        public static Vector3 AxisDirection =>
            new Vector3(0f, Mathf.Cos(TiltDeg * Mathf.Deg2Rad), Mathf.Sin(TiltDeg * Mathf.Deg2Rad));

        public static float GripLengthMM(ShowerColumnSpec spec) =>
            spec.HandShowerDiameterMM * GripLengthRatio;

        public static float GripRadiusAtHolderMM(ShowerColumnSpec spec) =>
            spec.HandShowerDiameterMM * Mathf.Lerp(GripBottomRadiusRatio, GripTopRadiusRatio,
                GripBelowHolderRatio);

        public static Vector3 GripBottomMM(ShowerColumnSpec spec) =>
            ShowerColumnLayout.HolderCentreMM(spec)
            - AxisDirection * (GripLengthMM(spec) * GripBelowHolderRatio);

        public static Vector3 GripTopMM(ShowerColumnSpec spec) =>
            ShowerColumnLayout.HolderCentreMM(spec)
            + AxisDirection * (GripLengthMM(spec) * (1f - GripBelowHolderRatio));

        public static Vector3 NeckTopMM(ShowerColumnSpec spec) =>
            GripTopMM(spec) + AxisDirection * (spec.HandShowerDiameterMM * NeckLengthRatio);

        public static Vector3 HeadRimMM(ShowerColumnSpec spec) =>
            NeckTopMM(spec) + AxisDirection
            * (spec.HandShowerDiameterMM * HeadThicknessRatio * HeadFlareRatio);

        public static Vector3 HeadFaceMM(ShowerColumnSpec spec) =>
            NeckTopMM(spec) + AxisDirection
            * (spec.HandShowerDiameterMM * HeadThicknessRatio);

        public static PipeSegment Grip(ShowerColumnSpec spec) =>
            new PipeSegment(GripBottomMM(spec), GripTopMM(spec),
                spec.HandShowerDiameterMM * GripBottomRadiusRatio,
                spec.HandShowerDiameterMM * GripTopRadiusRatio);

        public static PipeSegment Neck(ShowerColumnSpec spec) =>
            new PipeSegment(GripTopMM(spec), NeckTopMM(spec),
                spec.HandShowerDiameterMM * GripTopRadiusRatio,
                spec.HandShowerDiameterMM * NeckRadiusRatio);

        public static PipeSegment HeadFlare(ShowerColumnSpec spec) =>
            new PipeSegment(NeckTopMM(spec), HeadRimMM(spec),
                spec.HandShowerDiameterMM * NeckRadiusRatio,
                spec.HandShowerDiameterMM * HeadRadiusRatio);

        public static PipeSegment HeadFace(ShowerColumnSpec spec) =>
            new PipeSegment(HeadRimMM(spec), HeadFaceMM(spec),
                spec.HandShowerDiameterMM * HeadRadiusRatio,
                spec.HandShowerDiameterMM * HeadFaceRadiusRatio);

        public static PipeSegment[] Parts(ShowerColumnSpec spec) =>
            new[] { Grip(spec), Neck(spec), HeadFlare(spec), HeadFace(spec) };
    }
}
