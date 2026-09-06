using UnityEngine;

namespace KitchenDesigner.Core.Handles
{
    public static class HandleScale
    {
        public const float GrabRadiusPixels = 26f;

        public const float WorldSized = 1f;

        public const float DrawnArrowPixels = 42f;

        public const float MaxGrabRadiusToArrowLength = 0.75f;

        public static float ForScreen(in PinholeView view, Vector3 point, in HandleMetrics metrics)
            => WorldSized;
    }
}
