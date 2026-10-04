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
        public const float HeaderH = 32f;
        public const float HeaderGap = 4f;
        public const float MiniPad = 8f;
        public const float MiniButtonH = 40f;
        public const float MiniGap = 4f;

        public const int GridColumns = 2;
        public const float TileGap = 8f;
        public const float TileW = (DockW - 2f * Pad - TileGap) / GridColumns;
        public const float TileH = 120f;
        public const float TileImageH = 80f;

        public const float DockW = 268f;
        public const float RailW = MiniButtonH + 2f * MiniPad;
        public const float ControlH = 28f;
        public const float ControlGap = 4f;
        public const float TopStripH = ControlH + 2f * Pad;

        public const float PresetDotSize = 24f;
        public const float PresetDotGap = 4f;
        public const float PresetRowInset = 2f;
        public const float PresetRowWidth = TileW - 2f * PresetRowInset;

        public static int PresetDotsPerRow()
            => Mathf.Max(1, Mathf.FloorToInt((PresetRowWidth + PresetDotGap) / (PresetDotSize + PresetDotGap)));

        public static int PresetDotRows(int presetCount)
        {
            if (presetCount <= 0) return 0;
            int perRow = PresetDotsPerRow();
            return Mathf.CeilToInt(presetCount / (float)perRow);
        }

        public static float PresetRowsHeight(int presetCount)
        {
            int rows = PresetDotRows(presetCount);
            if (rows <= 0) return 0f;
            return rows * PresetDotSize + (rows - 1) * PresetDotGap;
        }

        public static Vector2 PresetDotPosition(int index)
        {
            int perRow = PresetDotsPerRow();
            int row = index / perRow;
            int col = index % perRow;
            return new Vector2(col * (PresetDotSize + PresetDotGap), -row * (PresetDotSize + PresetDotGap));
        }

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
                y -= HeaderH;
                if (group.Open && group.TileCount > 0) y -= HeaderGap;

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
