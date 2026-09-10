using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class HintBubbleLayout
    {
        public const float GapPx = 8f;
        public const float ScreenPadPx = 8f;

        public static Vector2 Beside(Vector2 badgeCenter, float badgeSize, Vector2 bubbleSize,
            Vector2 canvasSize)
        {
            float halfW = bubbleSize.x * 0.5f;
            float limitX = Mathf.Max(0f, canvasSize.x * 0.5f - halfW - ScreenPadPx);
            float limitY = Mathf.Max(0f, canvasSize.y * 0.5f - bubbleSize.y * 0.5f - ScreenPadPx);

            float right = badgeCenter.x + badgeSize * 0.5f + GapPx + halfW;
            float left = badgeCenter.x - badgeSize * 0.5f - GapPx - halfW;
            float x = right <= limitX ? right : left;

            return new Vector2(Mathf.Clamp(x, -limitX, limitX), Mathf.Clamp(badgeCenter.y, -limitY, limitY));
        }
    }
}
