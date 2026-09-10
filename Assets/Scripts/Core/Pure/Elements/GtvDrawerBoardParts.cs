using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class GtvDrawerBoardParts
    {
        public static Vector3Int BottomDimsMM(int lwMM, int nlMM)
        {
            int width = lwMM - DrawerConstants.BOTTOM_WIDTH_INSET;
            int depth = nlMM - DrawerConstants.BOTTOM_DEPTH_INSET;
            return new Vector3Int(width, DrawerConstants.PANEL_THICKNESS, depth);
        }

        public static Vector3Int BackDimsMM(int lwMM, DrawerType type)
        {
            int width = lwMM - DrawerConstants.BACK_WIDTH_INSET;
            int height = DrawerConstants.GetBackHeight(type);
            return new Vector3Int(width, height, DrawerConstants.PANEL_THICKNESS);
        }
    }
}
