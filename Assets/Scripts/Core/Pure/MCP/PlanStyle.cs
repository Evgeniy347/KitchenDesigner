namespace KitchenDesigner.Core.MCP
{
    public readonly struct PlanStyle
    {
        public readonly int Fill;
        public readonly int Stroke;
        public readonly int StrokeWidth;

        public PlanStyle(int fill, int stroke, int strokeWidth)
        {
            Fill = fill;
            Stroke = stroke;
            StrokeWidth = strokeWidth;
        }
    }
}
