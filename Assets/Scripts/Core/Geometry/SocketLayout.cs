using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SocketLayout
    {
        public const float PinHoleDiameterRatio = 0.16f;
        public const float PinSpacingRatio = 0.4f;
        public const float EarthWidthRatio = 0.44f;
        public const float EarthHeightRatio = 0.07f;
        public const float EarthOffsetRatio = 0.38f;
        public const float PinHoleDepthRatio = 0.5f;
        public const float EarthDepthRatio = 0.6f;

        public const float MinPinHoleDiameterMM = 1.5f;
        public const float MinEarthHeightMM = 0.8f;

        public const string WellName = "SocketWell";
        public const string PinHoleName = "SocketPin";
        public const string EarthName = "SocketEarth";

        public static float WellDiameterMM(int plateWidthMM, int plateHeightMM)
            => WallDeviceLayout.InnerSideMM(plateWidthMM, plateHeightMM);

        public static float PinHoleDiameterMM(int plateWidthMM, int plateHeightMM)
            => Mathf.Max(MinPinHoleDiameterMM,
                WellDiameterMM(plateWidthMM, plateHeightMM) * PinHoleDiameterRatio);

        public static float PinSpacingMM(int plateWidthMM, int plateHeightMM)
            => WellDiameterMM(plateWidthMM, plateHeightMM) * PinSpacingRatio;

        public static float EarthOffsetYMM(int plateWidthMM, int plateHeightMM)
            => WellDiameterMM(plateWidthMM, plateHeightMM) * EarthOffsetRatio;

        public static float EarthHeightMM(int plateWidthMM, int plateHeightMM)
            => Mathf.Max(MinEarthHeightMM,
                WellDiameterMM(plateWidthMM, plateHeightMM) * EarthHeightRatio);

        public static List<FurniturePartBox> BodyParts(int plateWidthMM, int plateHeightMM,
            int protrusionMM, int postCount)
        {
            var parts = new List<FurniturePartBox>();
            int posts = WallDeviceLayout.ClampPostCount(postCount);
            float wellDia = WellDiameterMM(plateWidthMM, plateHeightMM);
            float wellDepth = WallDeviceLayout.BodyDepthMM(protrusionMM);
            float wellZ = WallDeviceLayout.BodyCentreZMM(protrusionMM);

            for (int post = 0; post < posts; post++)
            {
                float x = WallDeviceLayout.PostCentreXMM(post, plateWidthMM, posts);
                WallDeviceLayout.AddRim(parts, post, x, plateWidthMM, plateHeightMM, protrusionMM);
                parts.Add(WallDeviceLayout.Disc(WellName + post, x, 0f, wellZ,
                    wellDia, wellDepth));
            }

            return parts;
        }

        public static List<FurniturePartBox> ContactParts(int plateWidthMM, int plateHeightMM,
            int protrusionMM, int postCount)
        {
            var parts = new List<FurniturePartBox>();
            int posts = WallDeviceLayout.ClampPostCount(postCount);
            float floorZ = WallDeviceLayout.RecessFloorZMM(protrusionMM);
            float recess = WallDeviceLayout.RecessDepthMM(protrusionMM);
            float pinDia = PinHoleDiameterMM(plateWidthMM, plateHeightMM);
            float halfSpacing = PinSpacingMM(plateWidthMM, plateHeightMM) * 0.5f;
            float pinDepth = WallDeviceLayout.BodyDepthMM(protrusionMM) * PinHoleDepthRatio;
            float earthWidth = WellDiameterMM(plateWidthMM, plateHeightMM) * EarthWidthRatio;
            float earthHeight = EarthHeightMM(plateWidthMM, plateHeightMM);
            float earthDepth = recess * EarthDepthRatio;
            float earthY = EarthOffsetYMM(plateWidthMM, plateHeightMM);

            for (int post = 0; post < posts; post++)
            {
                float x = WallDeviceLayout.PostCentreXMM(post, plateWidthMM, posts);
                parts.Add(WallDeviceLayout.Disc(PinHoleName + post + "L",
                    x - halfSpacing, 0f, floorZ - pinDepth * 0.5f, pinDia, pinDepth));
                parts.Add(WallDeviceLayout.Disc(PinHoleName + post + "R",
                    x + halfSpacing, 0f, floorZ - pinDepth * 0.5f, pinDia, pinDepth));
                parts.Add(WallDeviceLayout.Bar(EarthName + post + "Top",
                    x, earthY, floorZ + earthDepth * 0.5f, earthWidth, earthHeight,
                    earthDepth, 0f));
                parts.Add(WallDeviceLayout.Bar(EarthName + post + "Bottom",
                    x, -earthY, floorZ + earthDepth * 0.5f, earthWidth, earthHeight,
                    earthDepth, 0f));
            }

            return parts;
        }
    }
}
