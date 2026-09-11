using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class HintBadgeLane
    {
        public const float LeftPivot = 0f;

        public readonly struct LabelRect
        {
            public readonly float AnchoredX;
            public readonly float PivotX;
            public readonly float Width;

            public LabelRect(float anchoredX, float pivotX, float width)
            {
                AnchoredX = anchoredX;
                PivotX = pivotX;
                Width = width;
            }

            public float LeftEdge => AnchoredX - Width * PivotX;
        }

        public static LabelRect NarrowedToTextAndLane(LabelRect label, float textWidth,
            float badgeSize, float gap) =>
            new LabelRect(label.LeftEdge, LeftPivot,
                LabelWidthWithLane(label.Width, textWidth, badgeSize, gap));

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
