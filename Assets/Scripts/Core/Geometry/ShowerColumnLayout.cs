using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ShowerColumnLayout
    {
        public const float BendRadiusRatio = 3.5f;
        public const float BracketRosetteRadiusRatio = 1.9f;
        public const float BracketRosetteChamferRatio = 1.6f;
        public const float BracketArmRadiusRatio = 0.4f;
        public const float BracketCollarRadiusRatio = 1.5f;
        public const float SliderCollarRadiusRatio = 1.6f;
        public const float HolderArmRadiusRatio = 0.42f;
        public const float HolderCupRadiusRatio = 1.6f;
        public const float HolderReachRatio = 0.85f;
        public const float HolderCupLengthRatio = 0.26f;
        public const float DiverterWidthRatio = 2.2f;
        public const float DiverterHeightRatio = 2.6f;
        public const float DiverterFrontRatio = 1.2f;
        public const float DiverterCornerRatio = 0.4f;
        public const float DiverterLeverRadiusRatio = 0.35f;
        public const float DiverterLeverReachRatio = 0.9f;

        public const float UpperBracketRatio = 0.8f;
        public const float LowerBracketRatio = 0.28f;
        public const float SliderRatio = 0.5f;

        public const float BracketRosetteDepthMM = 9f;
        public const float BracketRosetteChamferMM = 4f;
        public const float BracketCollarHalfHeightMM = 11f;
        public const float SliderCollarHalfHeightMM = 24f;
        public const float HoseDiameterMM = 15f;

        public const float DiverterAboveFloorMM = 1100f;

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

        public static PipeSegment DiverterLever(ShowerColumnSpec spec)
        {
            float y = DiverterHeightMM(spec) * 0.5f;

            return new PipeSegment(
                new Vector3(0f, y, DiverterDepthMM(spec) * 0.5f),
                new Vector3(0f, y,
                    DiverterDepthMM(spec) + spec.RiserDiameterMM * DiverterLeverReachRatio),
                spec.RiserDiameterMM * DiverterLeverRadiusRatio);
        }

        public static Vector3 BendCentreMM(ShowerColumnSpec spec)
        {
            float bend = BendRadiusMM(spec.RiserDiameterMM);
            return new Vector3(0f, ArmAxisYMM(spec) - bend, spec.WallOffsetMM + bend);
        }

        public static Vector3[] RiserPath(ShowerColumnSpec spec)
        {
            float bend = BendRadiusMM(spec.RiserDiameterMM);

            return PipePath.Chain(
                new[]
                {
                    new Vector3(0f, 0f, spec.WallOffsetMM),
                    new Vector3(0f, ArmAxisYMM(spec) - bend, spec.WallOffsetMM),
                },
                PipePath.Arc(BendCentreMM(spec), Vector3.back, Vector3.up, bend, 90f,
                    GooseneckArcSegments),
                new[] { new Vector3(0f, ArmAxisYMM(spec), spec.ArmReachMM) });
        }

        public static float SliderYMM(ShowerColumnSpec spec) => spec.ColumnHeightMM * SliderRatio;

        public static Vector3 HolderCentreMM(ShowerColumnSpec spec) =>
            new Vector3(0f, SliderYMM(spec),
                spec.WallOffsetMM + RiserRadiusMM(spec)
                + spec.HandShowerDiameterMM * HolderReachRatio);

        public static Vector3 HoseOutletMM(ShowerColumnSpec spec) =>
            new Vector3(0f, 0f, spec.WallOffsetMM);

        public static Vector3 HoseInletMM(ShowerColumnSpec spec) =>
            ShowerColumnHandShower.GripBottomMM(spec);

        public static Vector3[] HosePath(ShowerColumnSpec spec) =>
            HoseSagCurve.Hanging(HoseOutletMM(spec), Vector3.down, HoseInletMM(spec),
                -ShowerColumnHandShower.AxisDirection, spec.HoseLengthMM,
                HoseSagCurve.DefaultSamples);

        public static PipeSegment BracketRosette(ShowerColumnSpec spec, float heightMM) =>
            new PipeSegment(
                new Vector3(0f, heightMM, 0f),
                new Vector3(0f, heightMM, BracketRosetteDepthMM),
                RiserRadiusMM(spec) * BracketRosetteRadiusRatio);

        public static PipeSegment BracketRosetteChamfer(ShowerColumnSpec spec, float heightMM) =>
            new PipeSegment(
                new Vector3(0f, heightMM, BracketRosetteDepthMM),
                new Vector3(0f, heightMM, BracketRosetteDepthMM + BracketRosetteChamferMM),
                RiserRadiusMM(spec) * BracketRosetteRadiusRatio,
                RiserRadiusMM(spec) * BracketRosetteChamferRatio);

        public static PipeSegment BracketArm(ShowerColumnSpec spec, float heightMM) =>
            new PipeSegment(
                new Vector3(0f, heightMM, BracketRosetteDepthMM + BracketRosetteChamferMM),
                new Vector3(0f, heightMM, spec.WallOffsetMM),
                RiserRadiusMM(spec) * BracketArmRadiusRatio);

        public static PipeSegment BracketCollar(ShowerColumnSpec spec, float heightMM) =>
            new PipeSegment(
                new Vector3(0f, heightMM - BracketCollarHalfHeightMM, spec.WallOffsetMM),
                new Vector3(0f, heightMM + BracketCollarHalfHeightMM, spec.WallOffsetMM),
                RiserRadiusMM(spec) * BracketCollarRadiusRatio);

        public static PipeSegment SliderCollar(ShowerColumnSpec spec) =>
            new PipeSegment(
                new Vector3(0f, SliderYMM(spec) - SliderCollarHalfHeightMM, spec.WallOffsetMM),
                new Vector3(0f, SliderYMM(spec) + SliderCollarHalfHeightMM, spec.WallOffsetMM),
                RiserRadiusMM(spec) * SliderCollarRadiusRatio);

        public static PipeSegment HolderArm(ShowerColumnSpec spec) =>
            new PipeSegment(
                new Vector3(0f, SliderYMM(spec), spec.WallOffsetMM), HolderCentreMM(spec),
                RiserRadiusMM(spec) * HolderArmRadiusRatio);

        public static PipeSegment HolderCup(ShowerColumnSpec spec)
        {
            var half = ShowerColumnHandShower.AxisDirection
                * (spec.HandShowerDiameterMM * HolderCupLengthRatio * 0.5f);

            return new PipeSegment(HolderCentreMM(spec) - half, HolderCentreMM(spec) + half,
                ShowerColumnHandShower.GripRadiusAtHolderMM(spec) * HolderCupRadiusRatio);
        }

        public static PipeSegment[] Parts(ShowerColumnSpec spec)
        {
            float upper = spec.ColumnHeightMM * UpperBracketRatio;
            float lower = spec.ColumnHeightMM * LowerBracketRatio;
            var hand = ShowerColumnHandShower.Parts(spec);
            var rain = ShowerColumnRainHead.Parts(spec);
            var column = new[]
            {
                BracketRosette(spec, upper), BracketRosetteChamfer(spec, upper),
                BracketArm(spec, upper), BracketCollar(spec, upper),
                BracketRosette(spec, lower), BracketRosetteChamfer(spec, lower),
                BracketArm(spec, lower), BracketCollar(spec, lower),
                SliderCollar(spec), HolderArm(spec), HolderCup(spec), DiverterLever(spec),
            };

            var all = new PipeSegment[column.Length + hand.Length + rain.Length];
            column.CopyTo(all, 0);
            hand.CopyTo(all, column.Length);
            rain.CopyTo(all, column.Length + hand.Length);
            return all;
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
    }
}
