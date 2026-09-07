using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ShowerColumnHandShower
    {
        public const float TiltDeg = 20f;

        /// <summary>Поворот головки поперёк рукоятки. Диск лейки сидит на шейке
        /// НЕ соосно рукоятке, а развёрнутым на прямой угол: лицо с форсунками
        /// смотрит от стены и чуть вниз, а не вверх вдоль палки.</summary>
        public const float HeadTurnDeg = 90f;

        public const float GripLengthRatio = 2.2f;
        public const float GripBelowHolderRatio = 0.4f;
        public const float GripBottomRadiusRatio = 0.12f;
        public const float GripTopRadiusRatio = 0.16f;
        public const float NeckRadiusRatio = 0.21f;
        public const float NeckLengthRatio = 0.12f;
        public const float HeadThicknessRatio = 0.26f;
        public const float FlareRatio = 0.34f;
        public const float ChamferRatio = 0.16f;
        public const float RimRadiusRatio = 0.5f;
        public const float FaceRadiusRatio = 0.44f;
        public const float MinHeadStoutnessRatio = 0.2f;

        public static Vector3 AxisDirection =>
            new Vector3(0f, Mathf.Cos(TiltDeg * Mathf.Deg2Rad), Mathf.Sin(TiltDeg * Mathf.Deg2Rad));

        /// <summary>Ось головки: <see cref="AxisDirection"/>, повёрнутая на
        /// <see cref="HeadTurnDeg"/> в плоскости наклона рукоятки.</summary>
        public static Vector3 HeadAxisDirection =>
            new Vector3(0f, -Mathf.Sin(TiltDeg * Mathf.Deg2Rad), Mathf.Cos(TiltDeg * Mathf.Deg2Rad));

        public static float GripLengthMM(ShowerColumnSpec spec) =>
            spec.HandShowerDiameterMM * GripLengthRatio;

        public static float HeadThicknessMM(ShowerColumnSpec spec) =>
            spec.HandShowerDiameterMM * HeadThicknessRatio;

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

        public static Vector3 HeadBackMM(ShowerColumnSpec spec) =>
            NeckTopMM(spec) - HeadAxisDirection * (HeadThicknessMM(spec) * FlareRatio);

        public static Vector3 HeadRimMM(ShowerColumnSpec spec) =>
            HeadBackMM(spec) + HeadAxisDirection * (HeadThicknessMM(spec) * FlareRatio);

        public static Vector3 HeadChamferMM(ShowerColumnSpec spec) =>
            HeadBackMM(spec)
            + HeadAxisDirection * (HeadThicknessMM(spec) * (1f - ChamferRatio));

        public static Vector3 HeadFaceMM(ShowerColumnSpec spec) =>
            HeadBackMM(spec) + HeadAxisDirection * HeadThicknessMM(spec);

        public static PipeSegment Grip(ShowerColumnSpec spec) =>
            new PipeSegment(GripBottomMM(spec), GripTopMM(spec),
                spec.HandShowerDiameterMM * GripBottomRadiusRatio,
                spec.HandShowerDiameterMM * GripTopRadiusRatio);

        public static PipeSegment Neck(ShowerColumnSpec spec) =>
            new PipeSegment(GripTopMM(spec), NeckTopMM(spec),
                spec.HandShowerDiameterMM * GripTopRadiusRatio,
                spec.HandShowerDiameterMM * NeckRadiusRatio);

        public static PipeSegment HeadFlare(ShowerColumnSpec spec) =>
            new PipeSegment(HeadBackMM(spec), HeadRimMM(spec),
                spec.HandShowerDiameterMM * NeckRadiusRatio,
                spec.HandShowerDiameterMM * RimRadiusRatio);

        public static PipeSegment HeadRim(ShowerColumnSpec spec) =>
            new PipeSegment(HeadRimMM(spec), HeadChamferMM(spec),
                spec.HandShowerDiameterMM * RimRadiusRatio);

        public static PipeSegment HeadFace(ShowerColumnSpec spec) =>
            new PipeSegment(HeadChamferMM(spec), HeadFaceMM(spec),
                spec.HandShowerDiameterMM * RimRadiusRatio,
                spec.HandShowerDiameterMM * FaceRadiusRatio);

        public static PipeSegment[] Parts(ShowerColumnSpec spec) =>
            new[] { Grip(spec), Neck(spec), HeadFlare(spec), HeadRim(spec), HeadFace(spec) };
    }
}
