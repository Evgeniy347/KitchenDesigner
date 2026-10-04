using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SofaBoxLayout
    {
        public const int FloorClearanceMM = 20;
        public const int WallMM = 18;
        public const int FrontLipMM = 100;
        public const int RearSetbackMM = 2;
        public const int MinSideInsetMM = 30;
        public const int CornerMarginMM = 5;
        public const int CompartmentCount = 3;
        public const int PanelCount = 4 + (CompartmentCount - 1);
        public const int DrawerFrontGapMM = 4;
        public const int DrawerFrontRecessMM = 2;
        public const int DrawerFrontTopGapMM = 2;
        public const string DrawerFrontNamePrefix = "SofaBoxDrawer";

        public static string DrawerFrontName(int index) => DrawerFrontNamePrefix + index;

        public static float DepthMM(int sofaDepthMM)
            => Mathf.Max(SofaLayout.MinPartMM,
                SofaLayout.SeatDepthFor(sofaDepthMM) - FrontLipMM - RearSetbackMM);

        public static float TopYMM(Vector3Int dimensionsMM, int seatHeightMM)
            => SofaLayout.FloorYMM(dimensionsMM) + seatHeightMM - SofaLayout.BackrestThicknessMM;

        public static float SideInsetMM(float cornerRadiusMM)
        {
            float radius = Mathf.Max(0f, cornerRadiusMM);
            float reach = radius > FrontLipMM
                ? radius - Mathf.Sqrt(radius * radius - (radius - FrontLipMM) * (radius - FrontLipMM))
                : 0f;
            return Mathf.Max(MinSideInsetMM, Mathf.Ceil(reach) + CornerMarginMM);
        }

        private static void Extents(Vector3Int dimensionsMM, int seatHeightMM, int cornerRadiusMM,
            out Vector3 min, out Vector3 max)
        {
            float inset = SideInsetMM(cornerRadiusMM);
            float z0 = SofaLayout.BackrestFrontZMM(dimensionsMM) + RearSetbackMM;
            min = new Vector3(-dimensionsMM.x * 0.5f + inset,
                SofaLayout.FloorYMM(dimensionsMM) + FloorClearanceMM, z0);
            max = new Vector3(dimensionsMM.x * 0.5f - inset,
                TopYMM(dimensionsMM, seatHeightMM), z0 + DepthMM(dimensionsMM.z));
        }

        public static Cuboid[] Panels(Vector3Int dimensionsMM, int seatHeightMM, int cornerRadiusMM)
        {
            Extents(dimensionsMM, seatHeightMM, cornerRadiusMM, out var min, out var max);
            float innerY0 = min.y + WallMM;
            float innerX0 = min.x + WallMM;
            float innerX1 = max.x - WallMM;
            float innerZ0 = min.z + WallMM;
            float innerZ1 = max.z - WallMM;

            var panels = new Cuboid[PanelCount];
            panels[0] = Cuboid.Between(min, new Vector3(max.x, innerY0, max.z));
            panels[1] = Cuboid.Between(new Vector3(min.x, innerY0, min.z),
                new Vector3(innerX0, max.y, max.z));
            panels[2] = Cuboid.Between(new Vector3(innerX1, innerY0, min.z),
                new Vector3(max.x, max.y, max.z));
            panels[3] = Cuboid.Between(new Vector3(innerX0, innerY0, min.z),
                new Vector3(innerX1, max.y, innerZ0));

            float clearWidth = (innerX1 - innerX0 - (CompartmentCount - 1) * WallMM)
                / CompartmentCount;
            for (int k = 1; k < CompartmentCount; k++)
            {
                float left = innerX0 + k * clearWidth + (k - 1) * WallMM;
                panels[3 + k] = Cuboid.Between(new Vector3(left, innerY0, innerZ0),
                    new Vector3(left + WallMM, max.y, innerZ1));
            }

            return panels;
        }

        public static FurniturePartBox[] DrawerFronts(Vector3Int dimensionsMM, int seatHeightMM,
            int cornerRadiusMM)
        {
            Extents(dimensionsMM, seatHeightMM, cornerRadiusMM, out var min, out var max);
            float innerX0 = min.x + WallMM;
            float innerX1 = max.x - WallMM;
            float width = (innerX1 - innerX0 - (CompartmentCount - 1) * DrawerFrontGapMM)
                / CompartmentCount;
            float bottom = min.y + WallMM;
            float height = max.y - DrawerFrontTopGapMM - bottom;
            float frontFace = max.z - DrawerFrontRecessMM;

            var fronts = new FurniturePartBox[CompartmentCount];
            for (int k = 0; k < CompartmentCount; k++)
            {
                float left = innerX0 + k * (width + DrawerFrontGapMM);
                fronts[k] = new FurniturePartBox(DrawerFrontName(k),
                    new Vector3(left + width * 0.5f, bottom + height * 0.5f,
                        frontFace - WallMM * 0.5f),
                    width, WallMM, height, 0f, FurniturePartOrientation.Horizontal);
            }

            return fronts;
        }
    }
}
