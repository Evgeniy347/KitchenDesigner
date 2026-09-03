using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BathMixerLayout
    {
        public const float EscutcheonRadiusRatio = 0.45f;
        public const float EscutcheonDiscRatio = 0.55f;
        public const float EscutcheonThroatRatio = 0.24f;
        public const float InletRadiusRatio = 0.15f;
        public const float BarRadiusRatio = 0.3f;
        public const float HeadRadiusRatio = 0.5f;
        public const float HeadShoulderRadiusRatio = 0.44f;
        public const float ScaleCollarRadiusRatio = 0.53f;
        public const float ScaleCollarStartRatio = 0.22f;
        public const float ScaleCollarEndRatio = 0.55f;
        public const float ButtonRadiusRatio = 0.12f;
        public const float ButtonStationRatio = 0.3f;

        public const float BodyAxisAboveFloorMM = 700f;

        public const float ButtonRiseMM = 5f;
        public const float ButtonSinkMM = 4f;

        public static float BodyRadiusMM(BathMixerSpec spec) =>
            spec.BodyDiameterMM * HeadRadiusRatio;

        public static float BarRadiusMM(BathMixerSpec spec) =>
            spec.BodyDiameterMM * BarRadiusRatio;

        public static float HeadLengthMM(BathMixerSpec spec) =>
            (spec.BodyLengthMM - spec.CentresMM) * 0.5f;

        public static float BodyAxisZMM(BathMixerSpec spec) =>
            spec.EscutcheonReachMM + BodyRadiusMM(spec);

        public static float EscutcheonRadiusMM(BathMixerSpec spec) =>
            spec.BodyDiameterMM * EscutcheonRadiusRatio;

        public static float InletRadiusMM(BathMixerSpec spec) =>
            spec.BodyDiameterMM * InletRadiusRatio;

        public static float InletXMM(BathMixerSpec spec, float side) =>
            side * spec.CentresMM * 0.5f;

        public static PipeSegment EscutcheonDisc(BathMixerSpec spec, float side) =>
            new PipeSegment(
                new Vector3(InletXMM(spec, side), 0f, 0f),
                new Vector3(InletXMM(spec, side), 0f,
                    spec.EscutcheonReachMM * EscutcheonDiscRatio),
                EscutcheonRadiusMM(spec));

        public static PipeSegment EscutcheonShoulder(BathMixerSpec spec, float side) =>
            new PipeSegment(
                new Vector3(InletXMM(spec, side), 0f,
                    spec.EscutcheonReachMM * EscutcheonDiscRatio),
                new Vector3(InletXMM(spec, side), 0f, spec.EscutcheonReachMM),
                EscutcheonRadiusMM(spec), spec.BodyDiameterMM * EscutcheonThroatRatio);

        public static PipeSegment Inlet(BathMixerSpec spec, float side) =>
            new PipeSegment(
                new Vector3(InletXMM(spec, side), 0f, spec.EscutcheonReachMM),
                new Vector3(InletXMM(spec, side), 0f, BodyAxisZMM(spec)),
                InletRadiusMM(spec));

        public static PipeSegment Bar(BathMixerSpec spec) =>
            new PipeSegment(
                new Vector3(InletXMM(spec, -1f), 0f, BodyAxisZMM(spec)),
                new Vector3(InletXMM(spec, 1f), 0f, BodyAxisZMM(spec)),
                BarRadiusMM(spec));

        public static PipeSegment FlowHead(BathMixerSpec spec) =>
            new PipeSegment(
                new Vector3(-spec.BodyLengthMM * 0.5f, 0f, BodyAxisZMM(spec)),
                new Vector3(InletXMM(spec, -1f), 0f, BodyAxisZMM(spec)),
                BodyRadiusMM(spec), spec.BodyDiameterMM * HeadShoulderRadiusRatio);

        public static PipeSegment ThermostatHead(BathMixerSpec spec) =>
            new PipeSegment(
                new Vector3(InletXMM(spec, 1f), 0f, BodyAxisZMM(spec)),
                new Vector3(spec.BodyLengthMM * 0.5f, 0f, BodyAxisZMM(spec)),
                spec.BodyDiameterMM * HeadShoulderRadiusRatio, BodyRadiusMM(spec));

        public static PipeSegment ScaleCollar(BathMixerSpec spec)
        {
            float root = InletXMM(spec, 1f);
            float head = HeadLengthMM(spec);

            return new PipeSegment(
                new Vector3(root + head * ScaleCollarStartRatio, 0f, BodyAxisZMM(spec)),
                new Vector3(root + head * ScaleCollarEndRatio, 0f, BodyAxisZMM(spec)),
                spec.BodyDiameterMM * ScaleCollarRadiusRatio);
        }

        public static PipeSegment LimitButton(BathMixerSpec spec)
        {
            float x = spec.BodyLengthMM * 0.5f - HeadLengthMM(spec) * ButtonStationRatio;
            float seat = BodyRadiusMM(spec);

            return new PipeSegment(
                new Vector3(x, seat - ButtonSinkMM, BodyAxisZMM(spec)),
                new Vector3(x, seat + ButtonRiseMM, BodyAxisZMM(spec)),
                spec.BodyDiameterMM * ButtonRadiusRatio);
        }

        public static PipeSegment[] Parts(BathMixerSpec spec) =>
            new[]
            {
                EscutcheonDisc(spec, -1f),
                EscutcheonShoulder(spec, -1f),
                Inlet(spec, -1f),
                EscutcheonDisc(spec, 1f),
                EscutcheonShoulder(spec, 1f),
                Inlet(spec, 1f),
                Bar(spec),
                FlowHead(spec),
                ThermostatHead(spec),
                ScaleCollar(spec),
                LimitButton(spec),
                BathMixerOutlets.DiverterKnob(spec),
                BathMixerOutlets.SpoutRun(spec),
                BathMixerOutlets.SpoutMouth(spec),
                BathMixerOutlets.HoseNipple(spec),
            };

        public static Bounds BoundsMM(BathMixerSpec spec) => PipeBounds.Of(Parts(spec));

        public static float CentreAboveFloorMM(BathMixerSpec spec) =>
            BodyAxisAboveFloorMM + BoundsMM(spec).center.y;

        public static Vector3Int DimensionsMM(BathMixerSpec spec)
        {
            var size = BoundsMM(spec).size;
            return new Vector3Int(
                Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), Mathf.RoundToInt(size.z));
        }
    }
}
