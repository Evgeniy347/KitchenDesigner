using System;
using System.Globalization;

namespace KitchenDesigner.Core.MCP
{
    public static class PlanFurniture
    {
        private static readonly int[] NiceBarsMm = { 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000 };

        private const int ArrowShaft = 9;
        private const int ArrowHead = 3;
        private const int GizmoWidth = 18;
        private const int ViewWordGap = 4;
        private const int WidestViewWordChars = 5;
        private const int BarLabelGap = 3;
        private const double MaxBarShareOfWidth = 0.35;

        public static void Add(PlanDrawing drawing, PlanLayout layout)
        {
            int s = layout.FontScale;
            int gizmoLeft = layout.Width - layout.Margin - GizmoWidth * s;
            AddGizmo(drawing, layout, gizmoLeft);
            AddViewWord(drawing, layout, gizmoLeft);
            int wordsWidth = (WidestViewWordChars * PlanFont.Advance - 1) * s;
            AddScaleBar(drawing, layout, gizmoLeft - ViewWordGap * s - wordsWidth - BarLabelGap * s);
        }

        private static void AddViewWord(PlanDrawing drawing, PlanLayout layout, int gizmoLeft)
        {
            int s = layout.FontScale;
            drawing.Shapes.Add(PlanShape.Label(gizmoLeft - ViewWordGap * s, layout.FooterTop + 3 * s,
                PlanViewWord.Name(layout.View).ToUpperInvariant(), PlanTextAnchor.End, s, PlanPalette.Ink));
        }

        private static void AddGizmo(PlanDrawing drawing, PlanLayout layout, int ox)
        {
            int s = layout.FontScale;
            bool top = layout.View == PlanView.Top;
            int oy = layout.FooterTop + (top ? s : 8 * s);
            drawing.Shapes.Add(PlanShape.Rect(ox, oy, ox + ArrowShaft * s, oy + s, PlanPalette.Ink, PlanShape.NoColor, 0));
            drawing.Shapes.Add(PlanShape.Triangle(ox + ArrowShaft * s, oy - s, ox + ArrowShaft * s, oy + 2 * s,
                ox + (ArrowShaft + ArrowHead) * s, oy + s / 2, PlanPalette.Ink));
            drawing.Shapes.Add(PlanShape.Label(ox + (ArrowShaft + ArrowHead + 1) * s, oy + s / 2 - PlanFont.Rows * s / 2,
                "X", PlanTextAnchor.Start, s, PlanPalette.Ink));
            if (top) AddDownArrow(drawing, ox, oy, s);
            else AddUpArrow(drawing, ox, oy, s);
        }

        private static void AddDownArrow(PlanDrawing drawing, int ox, int oy, int s)
        {
            int end = oy + ArrowShaft * s;
            drawing.Shapes.Add(PlanShape.Rect(ox, oy, ox + s, end, PlanPalette.Ink, PlanShape.NoColor, 0));
            drawing.Shapes.Add(PlanShape.Triangle(ox - s, end, ox + 2 * s, end, ox + s / 2, end + ArrowHead * s, PlanPalette.Ink));
            drawing.Shapes.Add(PlanShape.Label(ox + 3 * s, oy + 4 * s, "Z", PlanTextAnchor.Start, s, PlanPalette.Ink));
        }

        private static void AddUpArrow(PlanDrawing drawing, int ox, int oy, int s)
        {
            int start = oy + s - ArrowShaft * s;
            drawing.Shapes.Add(PlanShape.Rect(ox, start, ox + s, oy + s, PlanPalette.Ink, PlanShape.NoColor, 0));
            drawing.Shapes.Add(PlanShape.Triangle(ox - s, start, ox + 2 * s, start, ox + s / 2, start - ArrowHead * s, PlanPalette.Ink));
            drawing.Shapes.Add(PlanShape.Label(ox + 3 * s, start - 2 * s, "Y", PlanTextAnchor.Start, s, PlanPalette.Ink));
        }

        private static void AddScaleBar(PlanDrawing drawing, PlanLayout layout, int rightLimit)
        {
            int s = layout.FontScale;
            int mm = NiceBarsMm[0], pixels = 0;
            foreach (var candidate in NiceBarsMm)
            {
                int candidatePixels = Math.Max((int)Math.Round(candidate / layout.MmPerPixel), 2);
                var label = Label(candidate);
                bool fits = candidatePixels <= layout.Width * MaxBarShareOfWidth
                    && layout.Margin + candidatePixels + BarLabelGap * s + label.Length * PlanFont.Advance * s <= rightLimit;
                if (!fits && pixels != 0) break;
                mm = candidate;
                pixels = candidatePixels;
            }
            int left = layout.Margin, top = layout.FooterTop;
            drawing.ScaleBarMm = mm;
            drawing.Shapes.Add(PlanShape.Rect(left, top + 5 * s, left + pixels, top + 6 * s, PlanPalette.Ink, PlanShape.NoColor, 0));
            drawing.Shapes.Add(PlanShape.Rect(left, top + 3 * s, left + s, top + 8 * s, PlanPalette.Ink, PlanShape.NoColor, 0));
            drawing.Shapes.Add(PlanShape.Rect(left + pixels - s, top + 3 * s, left + pixels, top + 8 * s, PlanPalette.Ink, PlanShape.NoColor, 0));
            drawing.Shapes.Add(PlanShape.Label(left + pixels + BarLabelGap * s, top + s, Label(mm), PlanTextAnchor.Start, s, PlanPalette.Ink));
        }

        private static string Label(int mm) => mm.ToString(CultureInfo.InvariantCulture) + " mm";
    }
}
