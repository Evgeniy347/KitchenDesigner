using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BathMixerLayout
    {
        public const float EscutcheonRadiusRatio = 0.45f;
        public const float InletRadiusRatio = 0.15f;
        public const float EscutcheonThroatRatio = 1.5f;
        public const float EndCapLengthRatio = 1f / 6f;
        public const float EndCapRadiusRatio = 0.47f;
        public const float ButtonRadiusRatio = 0.12f;
        public const float SpoutBaseRadiusRatio = 0.2f;
        public const float SpoutTipRadiusRatio = 0.13f;
        public const float SpoutDropRatio = 0.4f;
        public const float OutletSeatRatio = 0.3f;
        public const float ButtonInsetRatio = 0.3f;

        public const float BodyAxisAboveFloorMM = 700f;

        public const float ButtonRiseMM = 5f;
        public const float OutletLengthMM = 18f;

        public static float BodyRadiusMM(BathMixerSpec spec) => spec.BodyDiameterMM * 0.5f;

        public static float BodyAxisZMM(BathMixerSpec spec) =>
            spec.EscutcheonReachMM + BodyRadiusMM(spec);

        public static float EscutcheonRadiusMM(BathMixerSpec spec) =>
            spec.BodyDiameterMM * EscutcheonRadiusRatio;

        public static float InletRadiusMM(BathMixerSpec spec) =>
            spec.BodyDiameterMM * InletRadiusRatio;

        public static float EndCapLengthMM(BathMixerSpec spec) =>
            spec.BodyLengthMM * EndCapLengthRatio;

        public static float OutletCentreXMM(BathMixerSpec spec) => spec.BodyLengthMM * 0.25f;

        public static PipeSegment[] Parts(BathMixerSpec spec)
        {
            float halfCentres = spec.CentresMM * 0.5f;
            float halfLength = spec.BodyLengthMM * 0.5f;
            float bodyR = BodyRadiusMM(spec);
            float bodyZ = BodyAxisZMM(spec);
            float inletR = InletRadiusMM(spec);
            float capLength = EndCapLengthMM(spec);
            float capR = spec.BodyDiameterMM * EndCapRadiusRatio;
            float reach = spec.EscutcheonReachMM;
            float buttonX = halfLength - capLength * ButtonInsetRatio;
            float outletX = OutletCentreXMM(spec);
            float outletSeatY = -bodyR * OutletSeatRatio;

            return new[]
            {
                Escutcheon(-halfCentres, reach, spec),
                Escutcheon(halfCentres, reach, spec),
                Inlet(-halfCentres, reach, bodyZ, inletR),
                Inlet(halfCentres, reach, bodyZ, inletR),
                new PipeSegment(
                    new Vector3(-halfLength + capLength, 0f, bodyZ),
                    new Vector3(halfLength - capLength, 0f, bodyZ), bodyR),
                new PipeSegment(
                    new Vector3(-halfLength, 0f, bodyZ),
                    new Vector3(-halfLength + capLength, 0f, bodyZ), capR),
                new PipeSegment(
                    new Vector3(halfLength - capLength, 0f, bodyZ),
                    new Vector3(halfLength, 0f, bodyZ), capR),
                new PipeSegment(
                    new Vector3(buttonX, capR - ButtonRiseMM, bodyZ),
                    new Vector3(buttonX, capR + ButtonRiseMM, bodyZ),
                    spec.BodyDiameterMM * ButtonRadiusRatio),
                new PipeSegment(
                    new Vector3(0f, 0f, bodyZ),
                    new Vector3(0f, -spec.SpoutLengthMM * SpoutDropRatio,
                        bodyZ + spec.SpoutLengthMM),
                    spec.BodyDiameterMM * SpoutBaseRadiusRatio,
                    spec.BodyDiameterMM * SpoutTipRadiusRatio),
                new PipeSegment(
                    new Vector3(outletX, outletSeatY, bodyZ),
                    new Vector3(outletX, outletSeatY - OutletLengthMM, bodyZ),
                    spec.OutletDiameterMM * 0.5f),
            };
        }

        public static Bounds BoundsMM(BathMixerSpec spec) => PipeBounds.Of(Parts(spec));

        public static float CentreAboveFloorMM(BathMixerSpec spec) =>
            BodyAxisAboveFloorMM + BoundsMM(spec).center.y;

        public static Vector3Int DimensionsMM(BathMixerSpec spec)
        {
            var size = BoundsMM(spec).size;
            return new Vector3Int(
                Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), Mathf.RoundToInt(size.z));
        }

        private static PipeSegment Escutcheon(float xMM, float reachMM, BathMixerSpec spec) =>
            new PipeSegment(
                new Vector3(xMM, 0f, 0f), new Vector3(xMM, 0f, reachMM),
                EscutcheonRadiusMM(spec), InletRadiusMM(spec) * EscutcheonThroatRatio);

        private static PipeSegment Inlet(float xMM, float reachMM, float bodyZMM, float radiusMM) =>
            new PipeSegment(
                new Vector3(xMM, 0f, reachMM), new Vector3(xMM, 0f, bodyZMM), radiusMM);
    }
}
