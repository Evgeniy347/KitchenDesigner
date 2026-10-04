using System;

namespace KitchenDesigner.Core.UI
{
    public enum TreeRowKind
    {
        Level,
        Group,
        Element,
    }

    public readonly struct TreeMetrics
    {
        public TreeMetrics(float lead, float indent, float chevronW, float chevronGap, float iconW, float iconGap)
        {
            Lead = lead;
            Indent = indent;
            ChevronW = chevronW;
            ChevronGap = chevronGap;
            IconW = iconW;
            IconGap = iconGap;
        }

        public float Lead { get; }

        public float Indent { get; }

        public float ChevronW { get; }

        public float ChevronGap { get; }

        public float IconW { get; }

        public float IconGap { get; }
    }

    public readonly struct TreeRowSlots
    {
        public TreeRowSlots(float chevronX, float iconX, float labelX, bool hasChevron, bool hasIcon)
        {
            ChevronX = chevronX;
            IconX = iconX;
            LabelX = labelX;
            HasChevron = hasChevron;
            HasIcon = hasIcon;
        }

        public float ChevronX { get; }

        public float IconX { get; }

        public float LabelX { get; }

        public bool HasChevron { get; }

        public bool HasIcon { get; }
    }

    public static class TreeRowLayout
    {
        public static TreeRowSlots For(TreeRowKind kind, int depth, TreeMetrics m)
        {
            float nested = Math.Max(0, depth - 1) * m.Indent;
            float afterChevron = m.ChevronW + m.ChevronGap;
            float afterIcon = m.IconW + m.IconGap;

            switch (kind)
            {
                case TreeRowKind.Level:
                    return new TreeRowSlots(m.Lead, 0f, m.Lead + afterChevron, hasChevron: true, hasIcon: false);
                case TreeRowKind.Group:
                {
                    float chevron = m.Lead + nested;
                    float icon = chevron + afterChevron;
                    return new TreeRowSlots(chevron, icon, icon + afterIcon, hasChevron: true, hasIcon: true);
                }
                default:
                {
                    float icon = m.Lead + nested + afterChevron;
                    return new TreeRowSlots(0f, icon, icon + afterIcon, hasChevron: false, hasIcon: true);
                }
            }
        }
    }
}
