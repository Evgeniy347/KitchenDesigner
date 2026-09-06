using UnityEngine;

namespace KitchenDesigner.Core.Handles
{
    public static class OverlayHandleScale
    {
        public const float CubeEdgeUnits = 0.05f;

        public const float DrawnCubePixels = 28f;

        public const float CubeWorstCornerToEdge = 0.8660254f;

        public const float CubeDrawnDiagonalToEdge = 1.4142136f;

        public const float GrabAlongArrowLen = 0.6f;

        public static float ArrowScale(in PinholeView view, Vector3 point,
            in HandleMetrics metrics) => HandleScale.WorldSized;

        public static float CubeScale(in PinholeView view, Vector3 point) =>
            HandleScale.WorldSized;

        public static float ArrowGrabAlongAxis(in HandleMetrics metrics, float scale) =>
            GrabAlongArrowLen * metrics.ArrowLen * scale;
    }
}
