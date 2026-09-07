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
            PipeNodeKind.Reducer => TwoWayStraight,
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

        public static PointMm HubOffsetMm(PipeNodeKind kind, string? sizeId)
        {
            float leg = LegLengthMm(sizeId);
            float radius = BodyDiameterMm(sizeId) * 0.5f;
            float inset = (leg - radius) * 0.5f;

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

        public static PointMm PortOffsetMm(PipeNodeKind kind, string? sizeId, int portIndex) =>
            HubOffsetMm(kind, sizeId).Shifted(PortAxis(kind, portIndex), LegLengthMm(sizeId));

        public static PipeFittingLeg[] LegsMm(PipeNodeKind kind, string? sizeId)
        {
            var axes = Legs(kind);
            float leg = LegLengthMm(sizeId);
            var result = new PipeFittingLeg[axes.Count];
            for (int i = 0; i < axes.Count; i++)
                result[i] = new PipeFittingLeg(axes[i], leg);
            return result;
        }

        public static float WidthMm(PipeNodeKind kind, string? sizeId)
        {
            float leg = LegLengthMm(sizeId);
            float radius = BodyDiameterMm(sizeId) * 0.5f;
            return kind switch
            {
                PipeNodeKind.Elbow => leg + radius,
                PipeNodeKind.Tee => leg + radius,
                PipeNodeKind.Supply => FlangeDiameterMm(sizeId),
                PipeNodeKind.Return => FlangeDiameterMm(sizeId),
                _ => radius * 2f,
            };
        }

        public static float DepthMm(PipeNodeKind kind, string? sizeId) => kind switch
        {
            PipeNodeKind.Supply => FlangeDiameterMm(sizeId),
            PipeNodeKind.Return => FlangeDiameterMm(sizeId),
            _ => BodyDiameterMm(sizeId),
        };

        public static float HeightMm(PipeNodeKind kind, string? sizeId)
        {
            float leg = LegLengthMm(sizeId);
            float radius = BodyDiameterMm(sizeId) * 0.5f;
            return kind switch
            {
                PipeNodeKind.Elbow => leg + radius,
                PipeNodeKind.Coupling => leg * 2f,
                PipeNodeKind.Reducer => leg * 2f,
                PipeNodeKind.Tee => leg * 2f,
                PipeNodeKind.Cap => leg,
                PipeNodeKind.Supply => leg + FlangeThicknessMm,
                PipeNodeKind.Return => leg + FlangeThicknessMm,
                _ => leg,
            };
        }

        public static int RoundedMm(float valueMm) =>
            (int)Math.Round(valueMm, MidpointRounding.AwayFromZero);

        private static PointMm FlangedHubMm(float legLengthMm) =>
            new PointMm(0f, -(legLengthMm + FlangeThicknessMm) * 0.5f + FlangeThicknessMm, 0f);
    }
}
