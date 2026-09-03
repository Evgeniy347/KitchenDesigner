using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct WallDeviceSpec
    {
        public readonly int PlateWidthMM;
        public readonly int PlateHeightMM;
        public readonly int ProtrusionMM;
        public readonly int PostCount;

        private WallDeviceSpec(int plateWidthMM, int plateHeightMM, int protrusionMM,
            int postCount)
        {
            PlateWidthMM = plateWidthMM;
            PlateHeightMM = plateHeightMM;
            ProtrusionMM = protrusionMM;
            PostCount = postCount;
        }

        public static WallDeviceSpec Default => Clamped(WallDeviceLayout.DefaultPlateWidthMM,
            WallDeviceLayout.DefaultPlateHeightMM, WallDeviceLayout.DefaultProtrusionMM,
            WallDeviceLayout.DefaultPostCount);

        public static WallDeviceSpec Clamped(int plateWidthMM, int plateHeightMM,
            int protrusionMM, int postCount)
            => new WallDeviceSpec(
                WallDeviceLayout.ClampPlateSideMM(plateWidthMM),
                WallDeviceLayout.ClampPlateSideMM(plateHeightMM),
                WallDeviceLayout.ClampProtrusionMM(protrusionMM),
                WallDeviceLayout.ClampPostCount(postCount));

        public static WallDeviceSpec Of(IWallDevice? device)
            => device == null
                ? Default
                : Clamped(device.PlateWidthMM, device.PlateHeightMM, device.ProtrusionMM,
                    device.PostCount);

        public Vector3Int DimensionsMM => WallDeviceLayout.DimensionsMM(PlateWidthMM,
            PlateHeightMM, ProtrusionMM, PostCount);

        public void ApplyTo(IWallDevice? device)
        {
            if (device == null) return;
            device.PlateWidthMM = PlateWidthMM;
            device.PlateHeightMM = PlateHeightMM;
            device.ProtrusionMM = ProtrusionMM;
            device.PostCount = PostCount;
        }
    }
}
