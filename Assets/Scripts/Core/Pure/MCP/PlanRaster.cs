namespace KitchenDesigner.Core.MCP
{
    public static class PlanRaster
    {
        public static PlanCanvas Render(PlanDrawing drawing)
        {
            var canvas = new PlanCanvas(drawing.Width, drawing.Height, PlanPalette.Background);
            foreach (var shape in drawing.Shapes) Draw(canvas, shape);
            return canvas;
        }

        private static void Draw(PlanCanvas canvas, PlanShape shape)
        {
            switch (shape.Kind)
            {
                case PlanShapeKind.Rect:
                    if (shape.Fill != PlanShape.NoColor) canvas.FillRect(shape.X0, shape.Y0, shape.X1, shape.Y1, shape.Fill);
                    if (shape.Stroke != PlanShape.NoColor && shape.StrokeWidth > 0)
                        canvas.StrokeRectInside(shape.X0, shape.Y0, shape.X1, shape.Y1, shape.StrokeWidth, shape.Stroke);
                    break;
                case PlanShapeKind.Line:
                    canvas.Line(shape.X0, shape.Y0, shape.X1, shape.Y1, shape.Stroke);
                    break;
                case PlanShapeKind.Triangle:
                    canvas.FillTriangle(shape.X0, shape.Y0, shape.X1, shape.Y1, shape.X2, shape.Y2, shape.Fill);
                    break;
                default:
                    canvas.DrawText(shape.TextLeft, shape.Y0, shape.Text, shape.Scale, shape.Fill);
                    break;
            }
        }
    }
}
