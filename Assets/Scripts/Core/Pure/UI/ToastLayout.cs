using System;

namespace KitchenDesigner.Core.UI
{
    public readonly struct ToastMetrics
    {
        public ToastMetrics(float minHeight, float padX, float padY, float gap, float stripeWidth,
            float iconSize, float dismissSize, float maxWidth)
        {
            MinHeight = minHeight;
            PadX = padX;
            PadY = padY;
            Gap = gap;
            StripeWidth = stripeWidth;
            IconSize = iconSize;
            DismissSize = dismissSize;
            MaxWidth = maxWidth;
        }

        public float MinHeight { get; }

        public float PadX { get; }

        public float PadY { get; }

        public float Gap { get; }

        public float StripeWidth { get; }

        public float IconSize { get; }

        public float DismissSize { get; }

        public float MaxWidth { get; }
    }

    public readonly struct ToastLayout
    {
        private ToastLayout(float width, float height, float iconX, float textX, float textWidth,
            float actionX, float actionWidth, float dismissX)
        {
            Width = width;
            Height = height;
            IconX = iconX;
            TextX = textX;
            TextWidth = textWidth;
            ActionX = actionX;
            ActionWidth = actionWidth;
            DismissX = dismissX;
        }

        public float Width { get; }

        public float Height { get; }

        public float IconX { get; }

        public float TextX { get; }

        public float TextWidth { get; }

        public float ActionX { get; }

        public float ActionWidth { get; }

        public float DismissX { get; }

        public static float TextWidthBudget(ToastMetrics m, float actionWidth) =>
            m.MaxWidth - FixedWidth(m, actionWidth);

        public static ToastLayout For(ToastMetrics m, float textWidth, float textHeight, float actionWidth)
        {
            float text = Math.Min(textWidth, TextWidthBudget(m, actionWidth));
            float iconX = m.PadX + m.StripeWidth + m.Gap;
            float textX = iconX + m.IconSize + m.Gap;
            float actionX = textX + text + m.Gap;
            float dismissX = actionX + (actionWidth > 0f ? actionWidth + m.Gap : 0f);
            float width = dismissX + m.DismissSize + m.PadX;
            float height = Math.Max(m.MinHeight, textHeight + 2f * m.PadY);
            return new ToastLayout(width, height, iconX, textX, text, actionX, actionWidth, dismissX);
        }

        private static float FixedWidth(ToastMetrics m, float actionWidth) =>
            m.PadX + m.StripeWidth + m.Gap + m.IconSize + m.Gap
            + (actionWidth > 0f ? m.Gap + actionWidth : 0f)
            + m.Gap + m.DismissSize + m.PadX;
    }
}
