using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal static class TooltipPlacement
    {
        public const float GapPx = 6f;
        public const float ScreenPadPx = 4f;

        public static Vector2 Below(Vector2 targetBottomCenter, float targetHeight,
            Vector2 tooltipSize, Vector2 canvasSize)
        {
            float limitX = Mathf.Max(0f, canvasSize.x * 0.5f - tooltipSize.x * 0.5f - ScreenPadPx);
            float x = Mathf.Clamp(targetBottomCenter.x, -limitX, limitX);

            float y = targetBottomCenter.y - GapPx;
            bool bottomWouldLeaveCanvas = y - tooltipSize.y < -canvasSize.y * 0.5f + ScreenPadPx;
            if (bottomWouldLeaveCanvas)
                y = targetBottomCenter.y + targetHeight + GapPx + tooltipSize.y;

            return new Vector2(x, y);
        }
    }
}
