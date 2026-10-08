using System;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    public sealed class PlanLayout
    {
        public const int MinExtentMm = 100;
        public const int EmptySceneExtentMm = 1000;
        public const int MinPixelsPerRect = 2;
        public const int MaxStripRows = 2;

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
        public readonly int StripRows;
        public readonly double MmPerPixel;

        private readonly float _minX;
        private readonly float _maxV;
        private readonly int _left;
        private readonly int _top;

        private PlanLayout(PlanView view, int fontScale, int stripRows, int width, int height, int footerTop,
            double mmPerPixel, PlanBounds bounds, int left)
        {
            View = view;
            FontScale = fontScale;
            StripRows = stripRows;
            Margin = MarginUnits * fontScale;
            Width = width;
            Height = height;
            FooterTop = footerTop;
            MmPerPixel = mmPerPixel;
            _minX = bounds.MinX;
            _maxV = bounds.MaxV;
            _left = left;
            _top = Margin + StripRows * StripRowPitch;
        }

        public int StripRowPitch => (PlanFont.Rows + 1) * FontScale;

        public int StripTop => Margin;

        public int FooterHeight => Height - FooterTop;

        public static int FontScaleFor(int px) => Math.Max(1, Math.Min(MaxFontScale, px / PixelsPerFontScale));

        public static PlanLayout For(DigestInput input, PlanView view, int px, bool labels)
        {
            var bounds = PlanBounds.Of(input, view);
            int scale = FontScaleFor(px);
            int margin = MarginUnits * scale, footer = FooterUnits * scale;
            int rows = labels ? StripRowsFor(input, view) : 0;
            int strip = rows * (PlanFont.Rows + 1) * scale;
            double availW = px - 2 * margin, availH = px - 2 * margin - strip - footer;
            double k = Math.Max(bounds.Width / availW, bounds.Height / availH);
            int drawW = (int)Math.Min(availW, Math.Ceiling(bounds.Width / k - 1e-9));
            int drawH = (int)Math.Min(availH, Math.Ceiling(bounds.Height / k - 1e-9));
            int width = Math.Max(drawW + 2 * margin, MinWidthUnits * scale);
            int footerTop = margin + strip + drawH + margin;
            return new PlanLayout(view, scale, rows, width, footerTop + footer, footerTop, k, bounds, (width - drawW) / 2);
        }

        public int X(float xMm) => _left + Pixels((xMm - _minX) / MmPerPixel);

        public int Y(float vMm) => _top + Pixels((_maxV - vMm) / MmPerPixel);

        public PlanRect RectOf(BoxMm box)
        {
            int x0 = X(box.Min.x), x1 = X(box.Max.x);
            int ya = Y(PlanBounds.VOf(View, box.Min)), yb = Y(PlanBounds.VOf(View, box.Max));
            int y0 = Math.Min(ya, yb), y1 = Math.Max(ya, yb);
            return new PlanRect(x0, y0, Math.Max(x1, x0 + MinPixelsPerRect), Math.Max(y1, y0 + MinPixelsPerRect));
        }

        private static int StripRowsFor(DigestInput input, PlanView view)
        {
            int inStrip = 0;
            foreach (var entry in input.Entries)
                if (PlanStyles.LabelsInStrip(PlanLayers.Of(entry), view)) inStrip++;
            return Math.Min(inStrip, MaxStripRows);
        }

        private static int Pixels(double value) => (int)Math.Floor(value + 0.5);
    }
}
