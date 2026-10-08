using System;

namespace KitchenDesigner.Core.MCP
{
    public sealed class PlanCanvas
    {
        public readonly int Width;
        public readonly int Height;
        public readonly byte[] Pixels;

        public PlanCanvas(int width, int height, int background)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height];
            if (background != 0) Array.Fill(Pixels, (byte)background);
        }

        public int At(int x, int y) => Pixels[y * Width + x];

        public void FillRect(int x0, int y0, int x1, int y1, int color)
        {
            int left = Math.Max(x0, 0), right = Math.Min(x1, Width);
            int top = Math.Max(y0, 0), bottom = Math.Min(y1, Height);
            for (int y = top; y < bottom; y++)
                for (int x = left; x < right; x++)
                    Pixels[y * Width + x] = (byte)color;
        }

        public void StrokeRectInside(int x0, int y0, int x1, int y1, int width, int color)
        {
            int w = Math.Min(width, Math.Min(x1 - x0, y1 - y0) / 2 + 1);
            FillRect(x0, y0, x1, y0 + w, color);
            FillRect(x0, y1 - w, x1, y1, color);
            FillRect(x0, y0, x0 + w, y1, color);
            FillRect(x1 - w, y0, x1, y1, color);
        }

        public void Line(int x0, int y0, int x1, int y1, int color)
        {
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                Plot(x0, y0, color);
                if (x0 == x1 && y0 == y1) return;
                int doubled = 2 * error;
                if (doubled >= dy) { error += dy; x0 += sx; }
                if (doubled <= dx) { error += dx; y0 += sy; }
            }
        }

        public void FillTriangle(int ax, int ay, int bx, int by, int cx, int cy, int color)
        {
            int left = Math.Min(ax, Math.Min(bx, cx)), right = Math.Max(ax, Math.Max(bx, cx));
            int top = Math.Min(ay, Math.Min(by, cy)), bottom = Math.Max(ay, Math.Max(by, cy));
            for (int y = top; y < bottom; y++)
                for (int x = left; x < right; x++)
                    if (Covers(ax, ay, bx, by, cx, cy, x + 0.5, y + 0.5)) Plot(x, y, color);
        }

        public void DrawText(int left, int top, string text, int scale, int color)
        {
            for (int i = 0; i < text.Length; i++)
            {
                int origin = left + i * PlanFont.Advance * scale;
                for (int row = 0; row < PlanFont.Rows; row++)
                    for (int column = 0; column < PlanFont.Columns; column++)
                        if (PlanFont.IsSet(text[i], column, row))
                            FillRect(origin + column * scale, top + row * scale,
                                origin + (column + 1) * scale, top + (row + 1) * scale, color);
            }
        }

        private void Plot(int x, int y, int color)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height) Pixels[y * Width + x] = (byte)color;
        }

        private static bool Covers(int ax, int ay, int bx, int by, int cx, int cy, double px, double py)
        {
            double d1 = Side(px, py, ax, ay, bx, by);
            double d2 = Side(px, py, bx, by, cx, cy);
            double d3 = Side(px, py, cx, cy, ax, ay);
            bool negative = d1 < 0 || d2 < 0 || d3 < 0;
            bool positive = d1 > 0 || d2 > 0 || d3 > 0;
            return !(negative && positive);
        }

        private static double Side(double px, double py, int ax, int ay, int bx, int by) =>
            (px - bx) * (ay - by) - (ax - bx) * (py - by);
    }
}
