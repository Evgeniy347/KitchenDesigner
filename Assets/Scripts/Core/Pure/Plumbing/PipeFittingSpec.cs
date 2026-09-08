using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeFittingSpec
    {
        public const float BodyDiameterFactor = 1.25f;
        public const float LegLengthFactor = 1.5f;
        public const float ElbowBendRadiusFactor = 1f;
        public const float FlangeThicknessMm = 6f;
        public const float FlangeDiameterFactor = 2f;

        public const float CapCrownDiameterFactor = 1.3f;
        public const float CapCollarThicknessMm = 6f;
        public const float CapDomeRiseFactor = 0.45f;
        public const float CapDomeTipFactor = 0.35f;

        public const float FlowArrowLengthFactor = 0.7f;
        public const float FlowArrowHeadFactor = 0.4f;
        public const float FlowArrowShaftFactor = 0.4f;
        public const float FlowArrowGapFillFactor = 0.9f;

        private static readonly PipeAxis[] TwoWayStraight = { PipeAxis.Down, PipeAxis.Up };
        private static readonly PipeAxis[] TwoWayCorner = { PipeAxis.Down, PipeAxis.Right };
        private static readonly PipeAxis[] ThreeWay = { PipeAxis.Down, PipeAxis.Up, PipeAxis.Right };
        private static readonly PipeAxis[] OneWay = { PipeAxis.Up };
        private static readonly PipeAxis[] None = new PipeAxis[0];

        public static bool IsFitting(PipeNodeKind kind) => kind != PipeNodeKind.Pipe;

        public static IReadOnlyList<PipeAxis> Legs(PipeNodeKind kind) => kind switch
        {
            PipeNodeKind.Elbow => TwoWayCorner,
            PipeNodeKind.Coupling => TwoWayStraight,
            PipeNodeKind.Tee => ThreeWay,
            PipeNodeKind.Cap => OneWay,
            PipeNodeKind.Supply => OneWay,
            PipeNodeKind.Return => OneWay,
            _ => None,
        };

        public static bool HasFlange(PipeNodeKind kind) =>
            kind == PipeNodeKind.Supply || kind == PipeNodeKind.Return;

        public static float FlowDirection(PipeNodeKind kind) => kind switch
        {
            PipeNodeKind.Supply => 1f,
            PipeNodeKind.Return => -1f,
            _ => 0f,
        };

        public static float BodyDiameterMm(string? sizeId) =>
            PipeSpec.Get(sizeId).OuterDiameterMm * BodyDiameterFactor;

        public static float FlangeDiameterMm(string? sizeId) =>
            BodyDiameterMm(sizeId) * FlangeDiameterFactor;

        public static float CapCrownRadiusMm(string? sizeId) =>
            BodyDiameterMm(sizeId) * 0.5f * CapCrownDiameterFactor;

        public static float LegLengthMm(string? sizeId) =>
            PipeSpec.Get(sizeId).OuterDiameterMm * LegLengthFactor;

        public static float ElbowBendRadiusMm(string? sizeId) =>
            PipeSpec.Get(sizeId).OuterDiameterMm * ElbowBendRadiusFactor;

        public static int PortCount(PipeNodeKind kind) => Legs(kind).Count;

        public static PointMm HubOffsetMm(PipeNodeKind kind, string? frameSizeId)
        {
            var hub = PipeFittingLayout.HubMM(kind, frameSizeId);
            return new PointMm(hub.x, hub.y, hub.z);
        }

        public static PipeAxis PortAxis(PipeNodeKind kind, int portIndex)
        {
            var legs = Legs(kind);
            return portIndex >= 0 && portIndex < legs.Count ? legs[portIndex] : PipeAxis.Up;
        }

        public static PointMm PortOffsetMm(PipeNodeKind kind, string? frameSizeId, int portIndex) =>
            HubOffsetMm(kind, frameSizeId)
                .Shifted(PortAxis(kind, portIndex), LegLengthMm(frameSizeId));

        public static PipeFittingLeg[] LegsMm(PipeNodeKind kind, string? frameSizeId)
        {
            var axes = Legs(kind);
            float leg = LegLengthMm(frameSizeId);
            var result = new PipeFittingLeg[axes.Count];
            for (int i = 0; i < axes.Count; i++)
                result[i] = new PipeFittingLeg(axes[i], leg);
            return result;
        }

        public static float WidthMm(PipeNodeKind kind, string? frameSizeId,
            string? boreSizeId = null) => CoverMm(kind, frameSizeId, boreSizeId).x;

        public static float HeightMm(PipeNodeKind kind, string? frameSizeId,
            string? boreSizeId = null) => CoverMm(kind, frameSizeId, boreSizeId).y;

        public static float DepthMm(PipeNodeKind kind, string? frameSizeId,
            string? boreSizeId = null) => CoverMm(kind, frameSizeId, boreSizeId).z;

        public static int RoundedMm(float valueMm) =>
            (int)Math.Round(valueMm, MidpointRounding.AwayFromZero);

        private static UnityEngine.Vector3 CoverMm(PipeNodeKind kind, string? frameSizeId,
            string? boreSizeId) => PipeFittingLayout.CoverSizeMM(kind, frameSizeId,
                PipeFittingLayout.UniformBores(boreSizeId ?? frameSizeId));
    }
}
