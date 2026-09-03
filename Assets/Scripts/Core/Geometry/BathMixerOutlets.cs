using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BathMixerOutlets
    {
        public const float StationRatio = 0.28f;
        public const float KnobRadiusRatio = 0.13f;
        public const float KnobSeatRatio = 0.5f;
        public const float NippleSeatRatio = 0.5f;
        public const float SpoutSeatRatio = 0.2f;
        public const float SpoutRootRadiusRatio = 0.26f;
        public const float SpoutElbowRadiusRatio = 0.21f;
        public const float SpoutTipRadiusRatio = 0.18f;
        public const float SpoutMouthRadiusRatio = 0.2f;
        public const float ElbowReachRatio = 0.4f;
        public const float ElbowDropRatio = 0.16f;
        public const float RunReachRatio = 0.8f;
        public const float RunDropRatio = 0.36f;
        public const float MouthDropRatio = 0.66f;
        public const float MinBranchRatio = 0.5f;

        public const float KnobRiseMM = 30f;
        public const float NippleLengthMM = 32f;

        public static float StationXMM(BathMixerSpec spec) => spec.CentresMM * StationRatio;

        public static Vector3 SpoutRootMM(BathMixerSpec spec) =>
            new Vector3(0f, -BathMixerLayout.BodyTubeRadiusMM(spec) * SpoutSeatRatio,
                BathMixerLayout.BodyAxisZMM(spec));

        public static Vector3 SpoutElbowMM(BathMixerSpec spec) =>
            new Vector3(0f, -spec.SpoutLengthMM * ElbowDropRatio,
                BathMixerLayout.BodyAxisZMM(spec) + spec.SpoutLengthMM * ElbowReachRatio);

        public static Vector3 SpoutTipMM(BathMixerSpec spec) =>
            new Vector3(0f, -spec.SpoutLengthMM * RunDropRatio,
                BathMixerLayout.BodyAxisZMM(spec) + spec.SpoutLengthMM * RunReachRatio);

        public static Vector3 SpoutMouthMM(BathMixerSpec spec) =>
            new Vector3(0f, -spec.SpoutLengthMM * MouthDropRatio,
                BathMixerLayout.BodyAxisZMM(spec) + spec.SpoutLengthMM);

        public static PipeSegment SpoutShoulder(BathMixerSpec spec) =>
            new PipeSegment(SpoutRootMM(spec), SpoutElbowMM(spec),
                spec.BodyDiameterMM * SpoutRootRadiusRatio,
                spec.BodyDiameterMM * SpoutElbowRadiusRatio);

        public static PipeSegment SpoutRun(BathMixerSpec spec) =>
            new PipeSegment(SpoutElbowMM(spec), SpoutTipMM(spec),
                spec.BodyDiameterMM * SpoutElbowRadiusRatio,
                spec.BodyDiameterMM * SpoutTipRadiusRatio);

        public static PipeSegment SpoutMouth(BathMixerSpec spec) =>
            new PipeSegment(SpoutTipMM(spec), SpoutMouthMM(spec),
                spec.BodyDiameterMM * SpoutTipRadiusRatio,
                spec.BodyDiameterMM * SpoutMouthRadiusRatio);

        public static PipeSegment DiverterKnob(BathMixerSpec spec)
        {
            float x = StationXMM(spec);
            float seat = BathMixerLayout.BodyTubeRadiusMM(spec) * KnobSeatRatio;
            float z = BathMixerLayout.BodyAxisZMM(spec);

            return new PipeSegment(
                new Vector3(x, seat, z), new Vector3(x, seat + KnobRiseMM, z),
                spec.BodyDiameterMM * KnobRadiusRatio);
        }

        public static PipeSegment HoseNipple(BathMixerSpec spec)
        {
            float x = StationXMM(spec);
            float seat = -BathMixerLayout.BodyTubeRadiusMM(spec) * NippleSeatRatio;
            float z = BathMixerLayout.BodyAxisZMM(spec);

            return new PipeSegment(
                new Vector3(x, seat, z), new Vector3(x, seat - NippleLengthMM, z),
                spec.OutletDiameterMM * 0.5f);
        }

        public static PipeSegment[] Parts(BathMixerSpec spec) =>
            new[]
            {
                SpoutShoulder(spec), SpoutRun(spec), SpoutMouth(spec),
                DiverterKnob(spec), HoseNipple(spec),
            };
    }
}
