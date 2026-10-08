namespace KitchenDesigner.Core.MCP
{
    public sealed class PlanShape
    {
        public const int NoColor = -1;

        public readonly PlanShapeKind Kind;
        public readonly int X0;
        public readonly int Y0;
        public readonly int X1;
        public readonly int Y1;
        public readonly int X2;
        public readonly int Y2;
        public readonly int Fill;
        public readonly int Stroke;
        public readonly int StrokeWidth;
        public readonly string Text;
        public readonly PlanTextAnchor Anchor;
        public readonly int Scale;
        public readonly string? Name;

        private PlanShape(PlanShapeKind kind, int x0, int y0, int x1, int y1, int x2, int y2,
            int fill, int stroke, int strokeWidth, string text, PlanTextAnchor anchor, int scale, string? name)
        {
            Kind = kind;
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            Fill = fill;
            Stroke = stroke;
            StrokeWidth = strokeWidth;
            Text = text;
            Anchor = anchor;
            Scale = scale;
            Name = name;
        }

        public static PlanShape Rect(int x0, int y0, int x1, int y1, int fill, int stroke, int strokeWidth, string? name = null) =>
            new PlanShape(PlanShapeKind.Rect, x0, y0, x1, y1, 0, 0, fill, stroke, strokeWidth, string.Empty,
                PlanTextAnchor.Start, 0, name);

        public static PlanShape Line(int x0, int y0, int x1, int y1, int color) =>
            new PlanShape(PlanShapeKind.Line, x0, y0, x1, y1, 0, 0, NoColor, color, 1, string.Empty,
                PlanTextAnchor.Start, 0, null);

        public static PlanShape Triangle(int x0, int y0, int x1, int y1, int x2, int y2, int fill) =>
            new PlanShape(PlanShapeKind.Triangle, x0, y0, x1, y1, x2, y2, fill, NoColor, 0, string.Empty,
                PlanTextAnchor.Start, 0, null);

        public static PlanShape Label(int x, int top, string text, PlanTextAnchor anchor, int scale, int color) =>
            new PlanShape(PlanShapeKind.Text, x, top, 0, 0, 0, 0, color, NoColor, 0, text, anchor, scale, null);

        public int TextWidth => Text.Length == 0 ? 0 : Text.Length * PlanFont.Advance * Scale - Scale;

        public int TextLeft
        {
            get
            {
                if (Anchor == PlanTextAnchor.Middle) return X0 - TextWidth / 2;
                return Anchor == PlanTextAnchor.End ? X0 - TextWidth : X0;
            }
        }
    }
}
