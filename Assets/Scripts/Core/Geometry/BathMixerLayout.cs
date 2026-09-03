using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BathMixerLayout
    {
        public const float HeadRadiusRatio = 0.5f;
        public const float BodyTubeRadiusRatio = 0.42f;
        public const float GrooveRadiusRatio = 0.34f;
        public const float GrooveLengthRatio = 0.12f;
        public const float EscutcheonRadiusRatio = 0.45f;
        public const float EscutcheonChamferRadiusRatio = 0.4f;
        public const float EscutcheonDiscRatio = 0.72f;
        public const float InletRadiusRatio = 0.13f;

        public const float MinBodySlendernessRatio = 0.8f;

        public const float BodyAxisAboveFloorMM = 700f;

        public static float BodyRadiusMM(BathMixerSpec spec) =>
            spec.BodyDiameterMM * HeadRadiusRatio;

        public static float BodyTubeRadiusMM(BathMixerSpec spec) =>
            spec.BodyDiameterMM * BodyTubeRadiusRatio;

        public static float HeadLengthMM(BathMixerSpec spec) =>
            (spec.BodyLengthMM - spec.CentresMM) * 0.5f;

        public static float GrooveLengthMM(BathMixerSpec spec) =>
            HeadLengthMM(spec) * GrooveLengthRatio;

        public static float BodyAxisZMM(BathMixerSpec spec) =>
            spec.EscutcheonReachMM + BodyRadiusMM(spec);

        public static float EscutcheonRadiusMM(BathMixerSpec spec) =>
            spec.BodyDiameterMM * EscutcheonRadiusRatio;

        public static float InletRadiusMM(BathMixerSpec spec) =>
            spec.BodyDiameterMM * InletRadiusRatio;

        public static float InletXMM(BathMixerSpec spec, float side) =>
            side * spec.CentresMM * 0.5f;

        public static float GrooveOuterXMM(BathMixerSpec spec, float side) =>
            InletXMM(spec, side) + side * GrooveLengthMM(spec);

        public static PipeSegment EscutcheonDisc(BathMixerSpec spec, float side) =>
            new PipeSegment(
                new Vector3(InletXMM(spec, side), 0f, 0f),
                new Vector3(InletXMM(spec, side), 0f,
                    spec.EscutcheonReachMM * EscutcheonDiscRatio),
                EscutcheonRadiusMM(spec));

        public static PipeSegment EscutcheonChamfer(BathMixerSpec spec, float side) =>
            new PipeSegment(
                new Vector3(InletXMM(spec, side), 0f,
                    spec.EscutcheonReachMM * EscutcheonDiscRatio),
                new Vector3(InletXMM(spec, side), 0f, spec.EscutcheonReachMM),
                EscutcheonRadiusMM(spec),
                spec.BodyDiameterMM * EscutcheonChamferRadiusRatio);

        public static PipeSegment Inlet(BathMixerSpec spec, float side) =>
            new PipeSegment(
                new Vector3(InletXMM(spec, side), 0f, spec.EscutcheonReachMM),
                new Vector3(InletXMM(spec, side), 0f, BodyAxisZMM(spec)),
                InletRadiusMM(spec));

        public static PipeSegment BodyTube(BathMixerSpec spec) =>
            new PipeSegment(
                new Vector3(InletXMM(spec, -1f), 0f, BodyAxisZMM(spec)),
                new Vector3(InletXMM(spec, 1f), 0f, BodyAxisZMM(spec)),
                BodyTubeRadiusMM(spec));

        public static PipeSegment Groove(BathMixerSpec spec, float side) =>
            new PipeSegment(
                new Vector3(InletXMM(spec, side), 0f, BodyAxisZMM(spec)),
                new Vector3(GrooveOuterXMM(spec, side), 0f, BodyAxisZMM(spec)),
                spec.BodyDiameterMM * GrooveRadiusRatio);

        public static PipeSegment[] Parts(BathMixerSpec spec)
        {
            var flow = BathMixerControls.FlowHandleParts(spec);
            var thermostat = BathMixerControls.ThermostatParts(spec);
            var outlets = BathMixerOutlets.Parts(spec);
            var body = new[]
            {
                EscutcheonDisc(spec, -1f), EscutcheonChamfer(spec, -1f), Inlet(spec, -1f),
                EscutcheonDisc(spec, 1f), EscutcheonChamfer(spec, 1f), Inlet(spec, 1f),
                BodyTube(spec), Groove(spec, -1f), Groove(spec, 1f),
            };

            var all = new PipeSegment[body.Length + flow.Length + thermostat.Length
                + outlets.Length];
            body.CopyTo(all, 0);
            flow.CopyTo(all, body.Length);
            thermostat.CopyTo(all, body.Length + flow.Length);
            outlets.CopyTo(all, body.Length + flow.Length + thermostat.Length);
            return all;
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
    }
}
