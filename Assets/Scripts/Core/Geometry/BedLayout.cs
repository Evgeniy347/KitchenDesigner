using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class BedLayout
    {
        public const int SingleWidthMM = 900;
        public const int DoubleWidthMM = 1800;
        public const int LengthMM = 2000;

        public const int LegHeightMM = 100;
        public const int LegCrossSectionMM = 60;
        public const int LegInsetMM = 40;
        public const int SingleLegCount = 4;
        public const int DoubleLegCount = 6;

        public const int FrameHeightMM = 200;
        public const int FrameCornerRadiusMM = 40;

        public const int MattressThicknessMM = 180;
        public const int MattressInsetMM = 30;
        public const int MattressPlanRadiusMM = 60;
        public const int MattressFilletMM = 60;

        public const int PillowThicknessMM = 120;
        public const int PillowWidthMM = 700;
        public const int PillowLengthMM = 450;
        public const int PillowGapMM = 40;
        public const int PillowFromHeadMM = 20;
        public const int PillowCornerRadiusMM = 90;
        public const int SinglePillowCount = 1;
        public const int DoublePillowCount = 2;

        public const int HeadboardThicknessMM = 60;
        public const int HeadboardCornerRadiusMM = 60;
        public const int MinHeadboardRiseMM = 100;

        public const int MinPartMM = 100;
        public const float PillowMaxLengthFraction = 0.3f;

        public const int DeckTopMM = LegHeightMM + FrameHeightMM + MattressThicknessMM;
        public const int TopWithoutHeadboardMM = DeckTopMM + PillowThicknessMM;
        public const int DefaultHeightWithHeadboardMM = 900;

        public const string MattressName = "BedMattress";
        public const string HeadboardName = "BedHeadboard";
        public const string PillowNamePrefix = "BedPillow";
        public const string LegNamePrefix = "BedLeg";

        public static readonly Vector3 HeadboardEulerAngles = new Vector3(-90f, 0f, 0f);

        public static string PillowName(int index) => PillowNamePrefix + (index + 1);

        public static int WidthFor(bool isDouble) => isDouble ? DoubleWidthMM : SingleWidthMM;

        public static int HeightFor(bool hasHeadboard) =>
            hasHeadboard ? DefaultHeightWithHeadboardMM : TopWithoutHeadboardMM;

        public static int MinHeightMM(bool hasHeadboard) =>
            hasHeadboard ? TopWithoutHeadboardMM + MinHeadboardRiseMM : TopWithoutHeadboardMM;

        public static Vector3Int DefaultDimensions(bool isDouble, bool hasHeadboard) =>
            new Vector3Int(WidthFor(isDouble), HeightFor(hasHeadboard), LengthMM);

        public static bool IsDoubleWidth(int widthMM) =>
            widthMM >= (SingleWidthMM + DoubleWidthMM) / 2;

        public static int PillowCount(bool isDouble) =>
            isDouble ? DoublePillowCount : SinglePillowCount;

        public static int LegCount(bool isDouble) => isDouble ? DoubleLegCount : SingleLegCount;

        public static float FloorYMM(int heightMM) => -heightMM * 0.5f;

        public static float LegCentreYMM(int heightMM) =>
            FloorYMM(heightMM) + LegHeightMM * 0.5f;

        public static float FrameCentreYMM(int heightMM) =>
            FloorYMM(heightMM) + LegHeightMM + FrameHeightMM * 0.5f;

        public static float MattressCentreYMM(int heightMM) =>
            FloorYMM(heightMM) + DeckTopMM - MattressThicknessMM * 0.5f;

        public static float PillowCentreYMM(int heightMM) =>
            FloorYMM(heightMM) + DeckTopMM + PillowThicknessMM * 0.5f;

        public static Vector2[] LegCentresMM(Vector3Int dimensionsMM, bool isDouble)
        {
            float pull = LegInsetMM + LegCrossSectionMM * 0.5f;
            float x = Mathf.Max(0f, dimensionsMM.x * 0.5f - pull);
            float z = Mathf.Max(0f, dimensionsMM.z * 0.5f - pull);

            if (!isDouble)
                return new[]
                {
                    new Vector2(-x, -z), new Vector2(x, -z),
                    new Vector2(x, z), new Vector2(-x, z),
                };

            return new[]
            {
                new Vector2(-x, -z), new Vector2(x, -z),
                new Vector2(x, z), new Vector2(-x, z),
                new Vector2(-x, 0f), new Vector2(x, 0f),
            };
        }

        public static Vector3 MattressSizeMM(Vector3Int dimensionsMM) => new Vector3(
            Mathf.Max(MinPartMM, dimensionsMM.x - 2f * MattressInsetMM),
            MattressThicknessMM,
            Mathf.Max(MinPartMM,
                dimensionsMM.z - MattressInsetMM - HeadboardThicknessMM));

        public static float MattressCentreZMM(int depthMM) =>
            0.5f * ((-depthMM * 0.5f + HeadboardThicknessMM) + (depthMM * 0.5f - MattressInsetMM));

        public static Vector3 MattressCentreMM(Vector3Int dimensionsMM) => new Vector3(
            0f, MattressCentreYMM(dimensionsMM.y), MattressCentreZMM(dimensionsMM.z));

        public static float HeadboardPanelHeightMM(int heightMM) =>
            Mathf.Max(MinPartMM, heightMM - LegHeightMM);

        public static float HeadboardCentreYMM(int heightMM) =>
            FloorYMM(heightMM) + LegHeightMM + HeadboardPanelHeightMM(heightMM) * 0.5f;

        public static float HeadboardCentreZMM(int depthMM) =>
            -depthMM * 0.5f + HeadboardThicknessMM * 0.5f;

        public static Vector3 HeadboardCentreMM(Vector3Int dimensionsMM) => new Vector3(
            0f, HeadboardCentreYMM(dimensionsMM.y), HeadboardCentreZMM(dimensionsMM.z));

        public static float PillowWidthForMM(Vector3Int dimensionsMM, bool isDouble)
        {
            int count = PillowCount(isDouble);
            float usable = Mathf.Max(MinPartMM, dimensionsMM.x - 2f * MattressInsetMM);
            return Mathf.Max(MinPartMM, Mathf.Min(PillowWidthMM,
                (usable - (count - 1) * PillowGapMM) / count));
        }

        public static float PillowLengthForMM(int depthMM) =>
            Mathf.Max(MinPartMM, Mathf.Min(PillowLengthMM, depthMM * PillowMaxLengthFraction));

        public static Vector3 PillowSizeMM(Vector3Int dimensionsMM, bool isDouble) => new Vector3(
            PillowWidthForMM(dimensionsMM, isDouble),
            PillowThicknessMM,
            PillowLengthForMM(dimensionsMM.z));

        public static Vector3[] PillowCentresMM(Vector3Int dimensionsMM, bool isDouble)
        {
            int count = PillowCount(isDouble);
            float width = PillowWidthForMM(dimensionsMM, isDouble);
            float length = PillowLengthForMM(dimensionsMM.z);
            float y = PillowCentreYMM(dimensionsMM.y);
            float z = -dimensionsMM.z * 0.5f + HeadboardThicknessMM + PillowFromHeadMM
                + length * 0.5f;

            float span = count * width + (count - 1) * PillowGapMM;
            float startX = -span * 0.5f + width * 0.5f;

            var centres = new Vector3[count];
            for (int i = 0; i < count; i++)
                centres[i] = new Vector3(startX + i * (width + PillowGapMM), y, z);
            return centres;
        }

        public static float FittedRadiusMM(float asked, float first, float second) =>
            Mathf.Max(0f, Mathf.Min(asked, Mathf.Min(first, second) * 0.5f));
    }
}
