using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ShowerColumnLayout
    {
        public const float BendRadiusRatio = 3.5f;
        public const float BracketArmRadiusRatio = 0.55f;
        public const float BracketFlangeRadiusRatio = 1.9f;
        public const float BracketThroatRadiusRatio = 1.4f;
        public const float SliderRingRadiusRatio = 1.7f;
        public const float SliderStubRadiusRatio = 0.5f;
        public const float HandleBottomRadiusRatio = 0.42f;
        public const float HandleTopRadiusRatio = 0.75f;
        public const float DiverterWidthRatio = 2.2f;
        public const float DiverterHeightRatio = 2.9f;
        public const float DiverterFrontRatio = 1.2f;
        public const float DiverterCornerRatio = 0.4f;

        public const float UpperBracketRatio = 0.8f;
        public const float LowerBracketRatio = 0.28f;
        public const float SliderRatio = 0.5f;

        public const float BracketFlangeDepthMM = 14f;
        public const float SliderRingHalfHeightMM = 30f;
        public const float HoseDiameterMM = 15f;

        public const float DiverterAboveFloorMM = 1100f;

        public const float HandleLengthRatio = 2.2f;
        public const float HandleBelowSliderRatio = 0.38f;
        public const float HandShowerStandoffRatio = 0.35f;
        public const float HandShowerHeadThicknessRatio = 0.2f;

        public const int GooseneckArcSegments = 14;

        public const string DiverterName = "ShowerDiverter";

        public static float BendRadiusMM(int riserDiameterMM) => riserDiameterMM * BendRadiusRatio;

        public static float RiserRadiusMM(ShowerColumnSpec spec) => spec.RiserDiameterMM * 0.5f;

        public static float ArmAxisYMM(ShowerColumnSpec spec) =>
            spec.ColumnHeightMM - RiserRadiusMM(spec);

        public static float DiverterDepthMM(ShowerColumnSpec spec) =>
            spec.WallOffsetMM + spec.RiserDiameterMM * DiverterFrontRatio;

        public static float DiverterHeightMM(ShowerColumnSpec spec) =>
            spec.RiserDiameterMM * DiverterHeightRatio;

        public static FurniturePartBox DiverterBox(ShowerColumnSpec spec)
        {
            float depth = DiverterDepthMM(spec);
            float height = DiverterHeightMM(spec);

            return new FurniturePartBox(DiverterName,
                new Vector3(0f, height * 0.5f, depth * 0.5f),
                spec.RiserDiameterMM * DiverterWidthRatio, depth, height,
                spec.RiserDiameterMM * DiverterCornerRatio,
                FurniturePartOrientation.Horizontal);
        }

        public static Vector3[] RiserPath(ShowerColumnSpec spec)
        {
            float bend = BendRadiusMM(spec.RiserDiameterMM);
            float armY = ArmAxisYMM(spec);
            var bendCentre = new Vector3(0f, armY - bend, spec.WallOffsetMM + bend);

            return PipePath.Chain(
                new[]
                {
                    new Vector3(0f, 0f, spec.WallOffsetMM),
                    new Vector3(0f, armY - bend, spec.WallOffsetMM),
                },
                PipePath.Arc(bendCentre, Vector3.down, Vector3.forward, bend, 90f,
                    GooseneckArcSegments),
                new[] { new Vector3(0f, armY, spec.ArmReachMM) });
        }

        public static float SliderYMM(ShowerColumnSpec spec) => spec.ColumnHeightMM * SliderRatio;

        public static float HandShowerZMM(ShowerColumnSpec spec) =>
            spec.WallOffsetMM + RiserRadiusMM(spec)
            + spec.HandShowerDiameterMM * HandShowerStandoffRatio;

        public static float HandleLengthMM(ShowerColumnSpec spec) =>
            spec.HandShowerDiameterMM * HandleLengthRatio;

        public static float HandleBottomYMM(ShowerColumnSpec spec) =>
            SliderYMM(spec) - HandleLengthMM(spec) * HandleBelowSliderRatio;

        public static Vector3 HoseOutletMM(ShowerColumnSpec spec) =>
            new Vector3(0f, 0f, spec.WallOffsetMM);

        public static Vector3 HoseInletMM(ShowerColumnSpec spec) =>
            new Vector3(0f, HandleBottomYMM(spec), HandShowerZMM(spec));

        public static Vector3[] HosePath(ShowerColumnSpec spec) =>
            HoseSagCurve.Hanging(HoseOutletMM(spec), Vector3.down, HoseInletMM(spec), Vector3.down,
                spec.HoseLengthMM, HoseSagCurve.DefaultSamples);

        public static PipeSegment[] Parts(ShowerColumnSpec spec)
        {
            float riserR = RiserRadiusMM(spec);
            float offset = spec.WallOffsetMM;
            float armY = ArmAxisYMM(spec);
            float sliderY = SliderYMM(spec);
            float handZ = HandShowerZMM(spec);
            float handleBottom = HandleBottomYMM(spec);
            float handleTop = handleBottom + HandleLengthMM(spec);
            float handHeadThickness = spec.HandShowerDiameterMM * HandShowerHeadThicknessRatio;

            return new[]
            {
                Bracket(spec.ColumnHeightMM * UpperBracketRatio, spec),
                BracketArm(spec.ColumnHeightMM * UpperBracketRatio, spec),
                Bracket(spec.ColumnHeightMM * LowerBracketRatio, spec),
                BracketArm(spec.ColumnHeightMM * LowerBracketRatio, spec),
                new PipeSegment(
                    new Vector3(0f, sliderY - SliderRingHalfHeightMM, offset),
                    new Vector3(0f, sliderY + SliderRingHalfHeightMM, offset),
                    riserR * SliderRingRadiusRatio),
                new PipeSegment(
                    new Vector3(0f, sliderY, offset), new Vector3(0f, sliderY, handZ),
                    riserR * SliderStubRadiusRatio),
                new PipeSegment(
                    new Vector3(0f, handleBottom, handZ), new Vector3(0f, handleTop, handZ),
                    riserR * HandleBottomRadiusRatio, riserR * HandleTopRadiusRatio),
                new PipeSegment(
                    new Vector3(0f, handleTop, handZ),
                    new Vector3(0f, handleTop + handHeadThickness, handZ),
                    spec.HandShowerDiameterMM * 0.5f),
                new PipeSegment(
                    new Vector3(0f, armY - riserR - spec.HeadThicknessMM, spec.ArmReachMM),
                    new Vector3(0f, armY - riserR, spec.ArmReachMM),
                    spec.HeadDiameterMM * 0.5f),
            };
        }

        public static Bounds BoundsMM(ShowerColumnSpec spec)
        {
            var bounds = PipeBounds.Of(Parts(spec));
            bounds.Encapsulate(PipeBounds.OfPolyline(RiserPath(spec), RiserRadiusMM(spec)));
            bounds.Encapsulate(PipeBounds.OfPolyline(HosePath(spec), HoseDiameterMM * 0.5f));

            var diverter = DiverterBox(spec);
            bounds.Encapsulate(new Bounds(diverter.CentreMM, diverter.SizeMM));
            return bounds;
        }

        public static float CentreAboveFloorMM(ShowerColumnSpec spec) =>
            DiverterAboveFloorMM + BoundsMM(spec).center.y;

        public static Vector3Int DimensionsMM(ShowerColumnSpec spec)
        {
            var size = BoundsMM(spec).size;
            return new Vector3Int(
                Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), Mathf.RoundToInt(size.z));
        }

        private static PipeSegment Bracket(float heightMM, ShowerColumnSpec spec) =>
            new PipeSegment(
                new Vector3(0f, heightMM, 0f), new Vector3(0f, heightMM, BracketFlangeDepthMM),
                RiserRadiusMM(spec) * BracketFlangeRadiusRatio,
                RiserRadiusMM(spec) * BracketThroatRadiusRatio);

        private static PipeSegment BracketArm(float heightMM, ShowerColumnSpec spec) =>
            new PipeSegment(
                new Vector3(0f, heightMM, BracketFlangeDepthMM),
                new Vector3(0f, heightMM, spec.WallOffsetMM),
                RiserRadiusMM(spec) * BracketArmRadiusRatio);
    }
}
