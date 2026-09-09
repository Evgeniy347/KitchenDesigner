using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public readonly struct SidebarTileGroupMetrics
    {
        public SidebarTileGroupMetrics(bool open, int tileCount)
        {
            Open = open;
            TileCount = tileCount;
        }

        public bool Open { get; }

        public int TileCount { get; }
    }

    public readonly struct SidebarTileRow
    {
        public SidebarTileRow(int group, int tile, Vector2 position, float size, bool visible)
        {
            Group = group;
            Tile = tile;
            Position = position;
            Size = size;
            Visible = visible;
        }

        public int Group { get; }

        public int Tile { get; }

        public Vector2 Position { get; }

        public float Size { get; }

        public bool Visible { get; }

        public bool IsHeader => Tile == SidebarLayout.HeaderRow;

        public float Bottom => -Position.y + Size;
    }

    public static class SidebarLayout
    {
        public const int HeaderRow = -1;

        public const float Pad = 8f;
        public const float HeaderH = 34f;
        public const float HeaderGap = 4f;
        public const float MiniPad = 6f;
        public const float MiniButtonH = 38f;
        public const float MiniGap = 4f;

        public const int GridColumns = 2;
        public const float TileW = 96f;
        public const float TileH = 96f;
        public const float TileGap = 16f;
        public const float TileImageH = 56f;

        public const float DockW = 260f;
        public const float TopStripH = 36f;
        public const float SearchBandH = 32f;

        public static float TileColX(int col) => Pad + col * (TileW + TileGap);

        public static float TileRowY(int row) => -(row * (TileH + TileGap));

        public static int TileRowOf(int index) => index / GridColumns;

        public static int TileColOf(int index) => index % GridColumns;

        public static float GridContentHeight(int tileCount)
        {
            if (tileCount <= 0) return 0f;
            int rows = (tileCount + GridColumns - 1) / GridColumns;
            return rows * TileH + (rows - 1) * TileGap;
        }

        public static int VisibleTileRows(float availableHeight)
        {
            if (availableHeight < TileH) return 0;
            return 1 + Mathf.FloorToInt((availableHeight - TileH) / (TileH + TileGap));
        }

        public static float PlaceTiles(IReadOnlyList<SidebarTileGroupMetrics> groups, List<SidebarTileRow> rows)
        {
            rows.Clear();
            float y = -Pad;

            for (int g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                rows.Add(new SidebarTileRow(g, HeaderRow, new Vector2(Pad, y), HeaderH, true));
                y -= HeaderH + HeaderGap;

                for (int i = 0; i < group.TileCount; i++)
                {
                    int row = TileRowOf(i);
                    int col = TileColOf(i);
                    var pos = new Vector2(TileColX(col), y + TileRowY(row));
                    rows.Add(new SidebarTileRow(g, i, pos, TileH, group.Open));
                }

                if (group.Open)
                {
                    y -= GridContentHeight(group.TileCount);
                    if (group.TileCount > 0) y -= TileGap;
                }
            }

            return -y;
        }

        public static float MiniItemY(int index) => -(MiniPad + index * (MiniButtonH + MiniGap));

        public static float MiniContentHeight(int groupCount)
            => groupCount <= 0 ? MiniPad : MiniPad + groupCount * (MiniButtonH + MiniGap);
    }
}
