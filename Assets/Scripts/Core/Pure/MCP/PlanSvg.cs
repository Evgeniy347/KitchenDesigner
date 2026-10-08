using System.Globalization;
using System.Security;
using System.Text;

namespace KitchenDesigner.Core.MCP
{
    internal static class PlanSvg
    {
        private const int EmPerScale = 10;

        public static string Write(PlanDrawing drawing)
        {
            var svg = new StringBuilder();
            svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(drawing.Width)
                .Append("\" height=\"").Append(drawing.Height)
                .Append("\" viewBox=\"0 0 ").Append(drawing.Width).Append(' ').Append(drawing.Height)
                .Append("\" shape-rendering=\"crispEdges\" font-family=\"monospace\">\n");
            svg.Append("<rect width=\"").Append(drawing.Width).Append("\" height=\"").Append(drawing.Height)
                .Append("\" fill=\"").Append(PlanPalette.Hex(PlanPalette.Background)).Append("\"/>\n");
            foreach (var shape in drawing.Shapes) AppendShape(svg, shape);
            return svg.Append("</svg>\n").ToString();
        }

        private static void AppendShape(StringBuilder svg, PlanShape shape)
        {
            switch (shape.Kind)
            {
                case PlanShapeKind.Rect: AppendRect(svg, shape); break;
                case PlanShapeKind.Line: AppendLine(svg, shape); break;
                case PlanShapeKind.Triangle: AppendTriangle(svg, shape); break;
                default: AppendText(svg, shape); break;
            }
        }

        private static void AppendRect(StringBuilder svg, PlanShape shape)
        {
            double inset = shape.Stroke != PlanShape.NoColor ? shape.StrokeWidth / 2.0 : 0.0;
            svg.Append("<rect");
            if (shape.Name != null) svg.Append(" data-name=\"").Append(Escape(shape.Name)).Append('"');
            svg.Append(" x=\"").Append(Num(shape.X0 + inset)).Append("\" y=\"").Append(Num(shape.Y0 + inset))
                .Append("\" width=\"").Append(Num(shape.X1 - shape.X0 - 2 * inset))
                .Append("\" height=\"").Append(Num(shape.Y1 - shape.Y0 - 2 * inset)).Append('"');
            svg.Append(" fill=\"").Append(shape.Fill != PlanShape.NoColor ? PlanPalette.Hex(shape.Fill) : "none").Append('"');
            if (shape.Stroke != PlanShape.NoColor)
                svg.Append(" stroke=\"").Append(PlanPalette.Hex(shape.Stroke)).Append("\" stroke-width=\"")
                    .Append(shape.StrokeWidth).Append('"');
            svg.Append("/>\n");
        }

        private static void AppendLine(StringBuilder svg, PlanShape shape) =>
            svg.Append("<line x1=\"").Append(Num(shape.X0 + 0.5)).Append("\" y1=\"").Append(Num(shape.Y0 + 0.5))
                .Append("\" x2=\"").Append(Num(shape.X1 + 0.5)).Append("\" y2=\"").Append(Num(shape.Y1 + 0.5))
                .Append("\" stroke=\"").Append(PlanPalette.Hex(shape.Stroke)).Append("\" stroke-width=\"1\"/>\n");

        private static void AppendTriangle(StringBuilder svg, PlanShape shape) =>
            svg.Append("<polygon points=\"").Append(shape.X0).Append(',').Append(shape.Y0).Append(' ')
                .Append(shape.X1).Append(',').Append(shape.Y1).Append(' ')
                .Append(shape.X2).Append(',').Append(shape.Y2).Append("\" fill=\"")
                .Append(PlanPalette.Hex(shape.Fill)).Append("\"/>\n");

        private static void AppendText(StringBuilder svg, PlanShape shape)
        {
            int baseline = shape.Y0 + (PlanFont.Rows - 1) * shape.Scale;
            svg.Append("<text x=\"").Append(shape.X0).Append("\" y=\"").Append(baseline)
                .Append("\" font-size=\"").Append(shape.Scale * EmPerScale)
                .Append("\" text-anchor=\"").Append(AnchorWord(shape.Anchor))
                .Append("\" fill=\"").Append(PlanPalette.Hex(shape.Fill)).Append("\">")
                .Append(Escape(shape.Text)).Append("</text>\n");
        }

        private static string AnchorWord(PlanTextAnchor anchor)
        {
            if (anchor == PlanTextAnchor.Middle) return "middle";
            return anchor == PlanTextAnchor.End ? "end" : "start";
        }

        private static string Num(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

        private static string Escape(string text) => SecurityElement.Escape(text) ?? string.Empty;
    }
}
