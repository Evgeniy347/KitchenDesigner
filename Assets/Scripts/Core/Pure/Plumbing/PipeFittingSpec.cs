using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeFittingSpec
    {
        public const float BodyDiameterFactor = 1.25f;
        public const float LegLengthFactor = 1.5f;
        public const float FlangeThicknessMm = 6f;
        public const float FlangeDiameterFactor = 2f;

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

        public static float BodyDiameterMm(string? sizeId) =>
            PipeSpec.Get(sizeId).OuterDiameterMm * BodyDiameterFactor;

        public static float FlangeDiameterMm(string? sizeId) =>
            BodyDiameterMm(sizeId) * FlangeDiameterFactor;

        public static float LegLengthMm(string? sizeId) =>
            PipeSpec.Get(sizeId).OuterDiameterMm * LegLengthFactor;

        public static int PortCount(PipeNodeKind kind) => Legs(kind).Count;

        public static PointMm HubOffsetMm(PipeNodeKind kind, string? frameSizeId)
        {
            float leg = LegLengthMm(frameSizeId);
            float inset = HubInsetMm(frameSizeId);

            return kind switch
            {
                PipeNodeKind.Elbow => new PointMm(-inset, inset, 0f),
                PipeNodeKind.Tee => new PointMm(-inset, 0f, 0f),
                PipeNodeKind.Cap => new PointMm(0f, -leg * 0.5f, 0f),
                PipeNodeKind.Supply => FlangedHubMm(leg),
                PipeNodeKind.Return => FlangedHubMm(leg),
                _ => new PointMm(0f, 0f, 0f),
            };
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
            string? boreSizeId = null)
        {
            float leg = LegLengthMm(frameSizeId);
            float inset = HubInsetMm(frameSizeId);
            float radius = BodyRadiusMm(frameSizeId, boreSizeId);
            return kind switch
            {
                PipeNodeKind.Elbow => Cover(inset + radius, leg - inset),
                PipeNodeKind.Tee => Cover(inset + radius, leg - inset),
                PipeNodeKind.Supply => FlangeDiameterMm(boreSizeId ?? frameSizeId),
                PipeNodeKind.Return => FlangeDiameterMm(boreSizeId ?? frameSizeId),
                _ => radius * 2f,
            };
        }

        public static float DepthMm(PipeNodeKind kind, string? frameSizeId,
            string? boreSizeId = null) => kind switch
        {
            PipeNodeKind.Supply => FlangeDiameterMm(boreSizeId ?? frameSizeId),
            PipeNodeKind.Return => FlangeDiameterMm(boreSizeId ?? frameSizeId),
            _ => BodyRadiusMm(frameSizeId, boreSizeId) * 2f,
        };

        public static float HeightMm(PipeNodeKind kind, string? frameSizeId,
            string? boreSizeId = null)
        {
            float leg = LegLengthMm(frameSizeId);
            float inset = HubInsetMm(frameSizeId);
            float radius = BodyRadiusMm(frameSizeId, boreSizeId);
            return kind switch
            {
                PipeNodeKind.Elbow => Cover(leg - inset, inset + radius),
                PipeNodeKind.Coupling => leg * 2f,
                PipeNodeKind.Tee => Cover(leg, radius),
                PipeNodeKind.Cap => leg,
                PipeNodeKind.Supply => leg + FlangeThicknessMm,
                PipeNodeKind.Return => leg + FlangeThicknessMm,
                _ => leg,
            };
        }

        public static int RoundedMm(float valueMm) =>
            (int)Math.Round(valueMm, MidpointRounding.AwayFromZero);

        private static float HubInsetMm(string? frameSizeId) =>
            (LegLengthMm(frameSizeId) - BodyDiameterMm(frameSizeId) * 0.5f) * 0.5f;

        private static float BodyRadiusMm(string? frameSizeId, string? boreSizeId) =>
            BodyDiameterMm(boreSizeId ?? frameSizeId) * 0.5f;

        private static float Cover(float reachA, float reachB) =>
            2f * Math.Max(reachA, reachB);

        private static PointMm FlangedHubMm(float legLengthMm) =>
            new PointMm(0f, -(legLengthMm + FlangeThicknessMm) * 0.5f + FlangeThicknessMm, 0f);
    }
}
