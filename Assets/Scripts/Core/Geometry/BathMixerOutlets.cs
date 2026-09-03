using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BathMixerOutlets
    {
        public const float StationRatio = 0.28f;
        public const float KnobRadiusRatio = 0.13f;
        public const float KnobSeatRatio = 0.5f;
        public const float NippleSeatRatio = 0.5f;
        public const float SpoutSeatRatio = 0.35f;
        public const float SpoutBaseRadiusRatio = 0.22f;
        public const float SpoutTipRadiusRatio = 0.17f;
        public const float SpoutMouthRadiusRatio = 0.19f;
        public const float SpoutRunReachRatio = 0.86f;
        public const float SpoutDropRatio = 0.34f;
        public const float MouthDropRatio = 0.16f;

        public const float KnobRiseMM = 30f;
        public const float NippleLengthMM = 26f;

        public static float StationXMM(BathMixerSpec spec) => spec.CentresMM * StationRatio;

        public static Vector3 SpoutRootMM(BathMixerSpec spec) =>
            new Vector3(0f, -BathMixerLayout.BarRadiusMM(spec) * SpoutSeatRatio,
                BathMixerLayout.BodyAxisZMM(spec));

        public static Vector3 SpoutElbowMM(BathMixerSpec spec) =>
            new Vector3(0f, -spec.SpoutLengthMM * SpoutDropRatio,
                BathMixerLayout.BodyAxisZMM(spec) + spec.SpoutLengthMM * SpoutRunReachRatio);

        public static Vector3 SpoutMouthMM(BathMixerSpec spec) =>
            new Vector3(0f, -spec.SpoutLengthMM * (SpoutDropRatio + MouthDropRatio),
                BathMixerLayout.BodyAxisZMM(spec) + spec.SpoutLengthMM);

        public static PipeSegment SpoutRun(BathMixerSpec spec) =>
            new PipeSegment(SpoutRootMM(spec), SpoutElbowMM(spec),
                spec.BodyDiameterMM * SpoutBaseRadiusRatio,
                spec.BodyDiameterMM * SpoutTipRadiusRatio);

        public static PipeSegment SpoutMouth(BathMixerSpec spec) =>
            new PipeSegment(SpoutElbowMM(spec), SpoutMouthMM(spec),
                spec.BodyDiameterMM * SpoutTipRadiusRatio,
                spec.BodyDiameterMM * SpoutMouthRadiusRatio);

        public static PipeSegment DiverterKnob(BathMixerSpec spec)
        {
            float x = StationXMM(spec);
            float seat = BathMixerLayout.BarRadiusMM(spec) * KnobSeatRatio;
            float z = BathMixerLayout.BodyAxisZMM(spec);

            return new PipeSegment(
                new Vector3(x, seat, z), new Vector3(x, seat + KnobRiseMM, z),
                spec.BodyDiameterMM * KnobRadiusRatio);
        }

        public static PipeSegment HoseNipple(BathMixerSpec spec)
        {
            float x = StationXMM(spec);
            float seat = -BathMixerLayout.BarRadiusMM(spec) * NippleSeatRatio;
            float z = BathMixerLayout.BodyAxisZMM(spec);

            return new PipeSegment(
                new Vector3(x, seat, z), new Vector3(x, seat - NippleLengthMM, z),
                spec.OutletDiameterMM * 0.5f);
        }
    }
}
