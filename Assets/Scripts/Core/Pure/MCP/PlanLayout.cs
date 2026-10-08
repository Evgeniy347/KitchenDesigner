using System;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    public sealed class PlanLayout
    {
        public const int MinExtentMm = 100;
        public const int EmptySceneExtentMm = 1000;
        public const int MinPixelsPerRect = 2;

        private const int MarginUnits = 5;
        private const int FooterUnits = 13;
        private const int MinWidthUnits = 130;
        private const int PixelsPerFontScale = 256;
        private const int MaxFontScale = 4;

        public readonly PlanView View;
        public readonly int FontScale;
        public readonly int Margin;
        public readonly int Width;
        public readonly int Height;
        public readonly int FooterTop;
        public readonly double MmPerPixel;

        private readonly float _minX;
        private readonly float _minV;
        private readonly float _maxV;
        private readonly int _left;
        private readonly int _top;

        private PlanLayout(PlanView view, int fontScale, int width, int height, int footerTop, double mmPerPixel,
            float minX, float minV, float maxV, int left)
        {
            View = view;
            FontScale = fontScale;
            Margin = MarginUnits * fontScale;
            Width = width;
            Height = height;
            FooterTop = footerTop;
            MmPerPixel = mmPerPixel;
            _minX = minX;
            _minV = minV;
            _maxV = maxV;
            _left = left;
            _top = Margin;
        }

        public static int FontScaleFor(int px) => Math.Max(1, Math.Min(MaxFontScale, px / PixelsPerFontScale));

        public static PlanLayout For(DigestInput input, PlanView view, int px)
        {
            var bounds = PlanBounds.Of(input, view);
            int scale = FontScaleFor(px);
            int margin = MarginUnits * scale, footer = FooterUnits * scale;
            double availW = px - 2 * margin, availH = px - 2 * margin - footer;
            double k = Math.Max(bounds.Width / availW, bounds.Height / availH);
            int drawW = (int)Math.Min(availW, Math.Ceiling(bounds.Width / k - 1e-9));
            int drawH = (int)Math.Min(availH, Math.Ceiling(bounds.Height / k - 1e-9));
            int width = Math.Max(drawW + 2 * margin, MinWidthUnits * scale);
            int footerTop = 2 * margin + drawH;
            return new PlanLayout(view, scale, width, footerTop + footer, footerTop, k,
                bounds.MinX, bounds.MinV, bounds.MaxV, (width - drawW) / 2);
        }

        public int FooterHeight => Height - FooterTop;

        public int X(float xMm) => _left + Pixels((xMm - _minX) / MmPerPixel);

        public int Y(float vMm) =>
            View == PlanView.Top ? _top + Pixels((vMm - _minV) / MmPerPixel) : _top + Pixels((_maxV - vMm) / MmPerPixel);

        public PlanRect RectOf(BoxMm box)
        {
            int x0 = X(box.Min.x), x1 = X(box.Max.x);
            int ya = Y(PlanBounds.VOf(View, box.Min)), yb = Y(PlanBounds.VOf(View, box.Max));
            int y0 = Math.Min(ya, yb), y1 = Math.Max(ya, yb);
            return new PlanRect(x0, y0, Math.Max(x1, x0 + MinPixelsPerRect), Math.Max(y1, y0 + MinPixelsPerRect));
        }

        public PlanRect RectOf(Vector2 min, Vector2 max)
        {
            int x0 = X(min.x), x1 = X(max.x), y0 = Y(min.y), y1 = Y(max.y);
            return new PlanRect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));
        }

        private static int Pixels(double value) => (int)Math.Floor(value + 0.5);
    }
}
