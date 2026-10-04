using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal static class PerfHudBinding
    {
        public static void Install()
        {
            PerfHud.Style = new PerfHudStyle(UIStyle.HudPanel, UIStyle.Text, UIStyle.TextSecondary,
                UIStyle.TextWarning, UIStyle.FontMono, Mathf.RoundToInt(UIStyle.Space3),
                Mathf.RoundToInt(UIStyle.Space2));
            PerfHud.RightLimit = RightDockLeftEdge;
        }

        internal static float RightDockLeftEdge()
        {
            var dock = HierarchyPanelUI.Instance;
            if (dock == null || !dock.IsVisible || dock.WindowRect == null) return float.MaxValue;

            var corners = new Vector3[4];
            dock.WindowRect.GetWorldCorners(corners);
            return DockLimit(corners[0].x, corners[2].x, Screen.width);
        }

        internal static float DockLimit(float panelLeft, float panelRight, float screenWidth) =>
            panelRight > screenWidth * 0.5f ? panelLeft : float.MaxValue;
    }
}
