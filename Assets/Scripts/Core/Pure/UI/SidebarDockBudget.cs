using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class SidebarDockBudget
    {
        public static float PanelHeight(float screenHeight, float topOffset, float bottomMargin)
            => screenHeight - topOffset - bottomMargin;

        public static float ViewportHeight(float panelHeight)
            => panelHeight - SidebarLayout.TopStripH - SidebarLayout.SearchBandH;

        public static float TilesAreaHeight(float viewportHeight)
            => viewportHeight - SidebarLayout.HeaderH - SidebarLayout.HeaderGap;

        public static int TilesVisibleWithoutScroll(float screenHeight, float topOffset, float bottomMargin)
        {
            float panel = PanelHeight(screenHeight, topOffset, bottomMargin);
            float viewport = ViewportHeight(panel);
            float tilesArea = TilesAreaHeight(viewport);
            return Mathf.Max(0, SidebarLayout.VisibleTileRows(tilesArea)) * SidebarLayout.GridColumns;
        }

        public static bool AutoCollapsesAt(float screenHeight) => screenHeight <= 800f;
    }
}
