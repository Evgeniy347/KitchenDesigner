using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public readonly struct SidebarGroupMetrics
    {
        public SidebarGroupMetrics(bool open, IReadOnlyList<float>? itemHeights)
        {
            Open = open;
            ItemHeights = itemHeights;
        }

        public bool Open { get; }

        public IReadOnlyList<float>? ItemHeights { get; }

        public int ItemCount => ItemHeights == null ? 0 : ItemHeights.Count;
    }

    public readonly struct SidebarRow
    {
        public SidebarRow(int group, int item, Vector2 position, float height, bool visible)
        {
            Group = group;
            Item = item;
            Position = position;
            Height = height;
            Visible = visible;
        }

        public int Group { get; }

        public int Item { get; }

        public Vector2 Position { get; }

        public float Height { get; }

        public bool Visible { get; }

        public bool IsHeader => Item == SidebarLayout.HeaderRow;

        public float Bottom => -Position.y + Height;
    }

    public static class SidebarLayout
    {
        public const int HeaderRow = -1;

        public const float Pad = 8f;
        public const float HeaderH = 30f;
        public const float HeaderGap = 4f;
        public const float ItemGap = 3f;
        public const float ItemIndent = 14f;
        public const float SingleLineItemH = 26f;
        public const float ItemPadV = 8f;

        public const float MiniPad = 6f;
        public const float MiniButtonH = 38f;
        public const float MiniGap = 4f;

        public static float ItemHeight(int lines, int fontSize, float lineHeightFactor)
            => Mathf.Max(SingleLineItemH,
                Mathf.Ceil(Mathf.Max(1, lines) * fontSize * lineHeightFactor + ItemPadV));

        public static float Place(IReadOnlyList<SidebarGroupMetrics> groups, List<SidebarRow> rows)
        {
            rows.Clear();
            float y = -Pad;

            for (int g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                rows.Add(new SidebarRow(g, HeaderRow, new Vector2(Pad, y), HeaderH, true));
                y -= HeaderH + HeaderGap;

                for (int i = 0; i < group.ItemCount; i++)
                {
                    float h = group.ItemHeights![i];
                    rows.Add(new SidebarRow(g, i, new Vector2(Pad + ItemIndent, y), h, group.Open));
                    if (!group.Open) continue;
                    y -= h + ItemGap;
                }
            }

            return -y;
        }

        public static float MiniItemY(int index) => -(MiniPad + index * (MiniButtonH + MiniGap));

        public static float MiniContentHeight(int groupCount)
            => groupCount <= 0 ? MiniPad : MiniPad + groupCount * (MiniButtonH + MiniGap);
    }
}
