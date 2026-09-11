using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class HintBadgeLane
    {
        public static float LabelWidthWithLane(float labelWidth, float textWidth,
            float badgeSize, float gap) =>
            Mathf.Min(labelWidth, textWidth + gap + badgeSize);

        public static float AfterLabel(float labelCenterX, float labelWidth, float textWidth,
            float badgeSize, float gap)
        {
            float left = labelCenterX - labelWidth * 0.5f;
            float wanted = left + textWidth + gap + badgeSize * 0.5f;
            float rightmost = labelCenterX + labelWidth * 0.5f - badgeSize * 0.5f;
            float leftmost = left + badgeSize * 0.5f;
            return Mathf.Clamp(wanted, leftmost, Mathf.Max(leftmost, rightmost));
        }
    }
}
