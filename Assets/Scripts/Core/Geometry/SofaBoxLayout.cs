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
        public const int PanelCount = 5 + (CompartmentCount - 1);

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

        public static Cuboid[] Panels(Vector3Int dimensionsMM, int seatHeightMM, int cornerRadiusMM)
        {
            float inset = SideInsetMM(cornerRadiusMM);
            float x0 = -dimensionsMM.x * 0.5f + inset;
            float x1 = dimensionsMM.x * 0.5f - inset;
            float z0 = SofaLayout.BackrestFrontZMM(dimensionsMM) + RearSetbackMM;
            float z1 = z0 + DepthMM(dimensionsMM.z);
            float y0 = SofaLayout.FloorYMM(dimensionsMM) + FloorClearanceMM;
            float y1 = TopYMM(dimensionsMM, seatHeightMM);
            float innerY0 = y0 + WallMM;
            float innerX0 = x0 + WallMM;
            float innerX1 = x1 - WallMM;
            float innerZ0 = z0 + WallMM;
            float innerZ1 = z1 - WallMM;

            var panels = new Cuboid[PanelCount];
            panels[0] = Cuboid.Between(new Vector3(x0, y0, z0), new Vector3(x1, innerY0, z1));
            panels[1] = Cuboid.Between(new Vector3(x0, innerY0, z0), new Vector3(innerX0, y1, z1));
            panels[2] = Cuboid.Between(new Vector3(innerX1, innerY0, z0), new Vector3(x1, y1, z1));
            panels[3] = Cuboid.Between(new Vector3(innerX0, innerY0, z0),
                new Vector3(innerX1, y1, innerZ0));
            panels[4] = Cuboid.Between(new Vector3(innerX0, innerY0, innerZ1),
                new Vector3(innerX1, y1, z1));

            float clearWidth = (innerX1 - innerX0 - (CompartmentCount - 1) * WallMM)
                / CompartmentCount;
            for (int k = 1; k < CompartmentCount; k++)
            {
                float left = innerX0 + k * clearWidth + (k - 1) * WallMM;
                panels[4 + k] = Cuboid.Between(new Vector3(left, innerY0, innerZ0),
                    new Vector3(left + WallMM, y1, innerZ1));
            }

            return panels;
        }
    }
}
