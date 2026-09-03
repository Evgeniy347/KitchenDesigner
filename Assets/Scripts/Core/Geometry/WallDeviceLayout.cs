using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class WallDeviceLayout
    {
        public const int DefaultPlateWidthMM = 80;
        public const int DefaultPlateHeightMM = 80;
        public const int DefaultProtrusionMM = 10;
        public const int DefaultPostCount = 1;

        public const int MinPlateSideMM = 40;
        public const int MaxPlateSideMM = 200;
        public const int MinProtrusionMM = 3;
        public const int MaxProtrusionMM = 60;
        public const int MinPostCount = 1;
        public const int MaxPostCount = 3;

        public const int SocketCentreAboveFloorMM = 300;
        public const int SwitchCentreAboveFloorMM = 900;

        public const float RimWidthRatio = 0.14f;
        public const float MinRimWidthMM = 4f;
        public const float FrameDepthRatio = 0.45f;
        public const float RecessDepthRatio = 0.3f;
        public const float RimCornerRatio = 0.25f;

        public const string RimTopName = "RimTop";
        public const string RimBottomName = "RimBottom";
        public const string RimLeftName = "RimLeft";
        public const string RimRightName = "RimRight";

        public static int ClampPlateSideMM(int value)
            => Mathf.Clamp(value, MinPlateSideMM, MaxPlateSideMM);

        public static int ClampProtrusionMM(int value)
            => Mathf.Clamp(value, MinProtrusionMM, MaxProtrusionMM);

        public static int ClampPostCount(int value)
            => Mathf.Clamp(value, MinPostCount, MaxPostCount);

        public static int TotalWidthMM(int plateWidthMM, int postCount)
            => ClampPlateSideMM(plateWidthMM) * ClampPostCount(postCount);

        public static Vector3Int DimensionsMM(int plateWidthMM, int plateHeightMM,
            int protrusionMM, int postCount)
            => new Vector3Int(TotalWidthMM(plateWidthMM, postCount),
                ClampPlateSideMM(plateHeightMM), ClampProtrusionMM(protrusionMM));

        public static float PostCentreXMM(int index, int plateWidthMM, int postCount)
            => (index - (ClampPostCount(postCount) - 1) * 0.5f) * ClampPlateSideMM(plateWidthMM);

        public static float RimWidthMM(int plateWidthMM, int plateHeightMM)
            => Mathf.Max(MinRimWidthMM,
                Mathf.Min(ClampPlateSideMM(plateWidthMM), ClampPlateSideMM(plateHeightMM))
                * RimWidthRatio);

        public static float FrameDepthMM(int protrusionMM)
            => ClampProtrusionMM(protrusionMM) * FrameDepthRatio;

        public static float RecessDepthMM(int protrusionMM)
            => ClampProtrusionMM(protrusionMM) * RecessDepthRatio;

        public static float BodyDepthMM(int protrusionMM)
            => ClampProtrusionMM(protrusionMM) - FrameDepthMM(protrusionMM)
                - RecessDepthMM(protrusionMM);

        public static float InnerWidthMM(int plateWidthMM, int plateHeightMM)
            => ClampPlateSideMM(plateWidthMM) - 2f * RimWidthMM(plateWidthMM, plateHeightMM);

        public static float InnerHeightMM(int plateWidthMM, int plateHeightMM)
            => ClampPlateSideMM(plateHeightMM) - 2f * RimWidthMM(plateWidthMM, plateHeightMM);

        public static float InnerSideMM(int plateWidthMM, int plateHeightMM)
            => Mathf.Min(InnerWidthMM(plateWidthMM, plateHeightMM),
                InnerHeightMM(plateWidthMM, plateHeightMM));

        public static float FrontZMM(int protrusionMM) => ClampProtrusionMM(protrusionMM) * 0.5f;

        public static float RimCentreZMM(int protrusionMM)
            => FrontZMM(protrusionMM) - FrameDepthMM(protrusionMM) * 0.5f;

        public static float RecessFloorZMM(int protrusionMM)
            => FrontZMM(protrusionMM) - FrameDepthMM(protrusionMM) - RecessDepthMM(protrusionMM);

        public static float BodyCentreZMM(int protrusionMM)
            => -FrontZMM(protrusionMM) + BodyDepthMM(protrusionMM) * 0.5f;

        public static void AddRim(List<FurniturePartBox> into, int post, float centreXMM,
            int plateWidthMM, int plateHeightMM, int protrusionMM)
        {
            float plateW = ClampPlateSideMM(plateWidthMM);
            float plateH = ClampPlateSideMM(plateHeightMM);
            float rim = RimWidthMM(plateWidthMM, plateHeightMM);
            float depth = FrameDepthMM(protrusionMM);
            float z = RimCentreZMM(protrusionMM);
            float radius = rim * RimCornerRatio;
            float sideHeight = Mathf.Max(MinRimWidthMM, plateH - 2f * rim);

            into.Add(Bar($"{RimTopName}{post}", centreXMM, (plateH - rim) * 0.5f, z,
                plateW, rim, depth, radius));
            into.Add(Bar($"{RimBottomName}{post}", centreXMM, -(plateH - rim) * 0.5f, z,
                plateW, rim, depth, radius));
            into.Add(Bar($"{RimLeftName}{post}", centreXMM - (plateW - rim) * 0.5f, 0f, z,
                rim, sideHeight, depth, radius));
            into.Add(Bar($"{RimRightName}{post}", centreXMM + (plateW - rim) * 0.5f, 0f, z,
                rim, sideHeight, depth, radius));
        }

        public static FurniturePartBox Bar(string name, float centreXMM, float centreYMM,
            float centreZMM, float widthMM, float heightMM, float thicknessMM, float radiusMM)
            => new FurniturePartBox(name, new Vector3(centreXMM, centreYMM, centreZMM),
                widthMM, heightMM, thicknessMM, radiusMM, FurniturePartOrientation.Frontal);

        public static FurniturePartBox Disc(string name, float centreXMM, float centreYMM,
            float centreZMM, float diameterMM, float thicknessMM)
            => new FurniturePartBox(name, new Vector3(centreXMM, centreYMM, centreZMM),
                diameterMM, diameterMM, thicknessMM, diameterMM * 0.5f,
                FurniturePartOrientation.Frontal);
    }
}
