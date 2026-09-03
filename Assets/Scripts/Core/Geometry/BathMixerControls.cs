using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BathMixerControls
    {
        public const float ShoulderRadiusRatio = 0.455f;
        public const float CapRadiusRatio = 0.4f;
        public const float CapLengthRatio = 0.14f;
        public const float LeverRadiusRatio = 0.075f;
        public const float LeverRootRiseRatio = 0.1f;
        public const float LeverRootForwardRatio = 0.28f;
        public const float LeverTipDropRatio = 0.34f;
        public const float LeverTipForwardRatio = 0.62f;
        public const float ScaleCollarRadiusRatio = 0.56f;
        public const float ScaleCollarStartRatio = 0.18f;
        public const float ScaleCollarEndRatio = 0.5f;
        public const float ButtonRadiusRatio = 0.12f;
        public const float ButtonStationRatio = 0.72f;

        public const float ButtonRiseMM = 6f;
        public const float ButtonSinkMM = 4f;

        public static float HandleMidXMM(BathMixerSpec spec, float side) =>
            (BathMixerLayout.GrooveOuterXMM(spec, side) + side * spec.BodyLengthMM * 0.5f) * 0.5f;

        public static float CapLengthMM(BathMixerSpec spec) =>
            BathMixerLayout.HeadLengthMM(spec) * CapLengthRatio;

        public static PipeSegment Shoulder(BathMixerSpec spec, float side) =>
            new PipeSegment(
                new Vector3(BathMixerLayout.GrooveOuterXMM(spec, side), 0f,
                    BathMixerLayout.BodyAxisZMM(spec)),
                new Vector3(side * (spec.BodyLengthMM * 0.5f - CapLengthMM(spec)), 0f,
                    BathMixerLayout.BodyAxisZMM(spec)),
                spec.BodyDiameterMM * ShoulderRadiusRatio, BathMixerLayout.BodyRadiusMM(spec));

        public static PipeSegment Cap(BathMixerSpec spec, float side) =>
            new PipeSegment(
                new Vector3(side * (spec.BodyLengthMM * 0.5f - CapLengthMM(spec)), 0f,
                    BathMixerLayout.BodyAxisZMM(spec)),
                new Vector3(side * spec.BodyLengthMM * 0.5f, 0f,
                    BathMixerLayout.BodyAxisZMM(spec)),
                BathMixerLayout.BodyRadiusMM(spec), spec.BodyDiameterMM * CapRadiusRatio);

        public static PipeSegment Lever(BathMixerSpec spec)
        {
            float x = HandleMidXMM(spec, -1f);
            float z = BathMixerLayout.BodyAxisZMM(spec);

            return new PipeSegment(
                new Vector3(x, -spec.BodyDiameterMM * LeverRootRiseRatio,
                    z + spec.BodyDiameterMM * LeverRootForwardRatio),
                new Vector3(x, -spec.BodyDiameterMM * LeverTipDropRatio,
                    z + spec.BodyDiameterMM * LeverTipForwardRatio),
                spec.BodyDiameterMM * LeverRadiusRatio);
        }

        public static PipeSegment ScaleCollar(BathMixerSpec spec)
        {
            float root = BathMixerLayout.GrooveOuterXMM(spec, 1f);
            float head = BathMixerLayout.HeadLengthMM(spec);

            return new PipeSegment(
                new Vector3(root + head * ScaleCollarStartRatio, 0f,
                    BathMixerLayout.BodyAxisZMM(spec)),
                new Vector3(root + head * ScaleCollarEndRatio, 0f,
                    BathMixerLayout.BodyAxisZMM(spec)),
                spec.BodyDiameterMM * ScaleCollarRadiusRatio);
        }

        public static PipeSegment LimitButton(BathMixerSpec spec)
        {
            float root = BathMixerLayout.GrooveOuterXMM(spec, 1f);
            float x = root + BathMixerLayout.HeadLengthMM(spec) * ButtonStationRatio;
            float seat = BathMixerLayout.BodyRadiusMM(spec);

            return new PipeSegment(
                new Vector3(x, seat - ButtonSinkMM, BathMixerLayout.BodyAxisZMM(spec)),
                new Vector3(x, seat + ButtonRiseMM, BathMixerLayout.BodyAxisZMM(spec)),
                spec.BodyDiameterMM * ButtonRadiusRatio);
        }

        public static PipeSegment[] FlowHandleParts(BathMixerSpec spec) =>
            new[] { Shoulder(spec, -1f), Cap(spec, -1f), Lever(spec) };

        public static PipeSegment[] ThermostatParts(BathMixerSpec spec) =>
            new[] { Shoulder(spec, 1f), Cap(spec, 1f), ScaleCollar(spec), LimitButton(spec) };
    }
}
