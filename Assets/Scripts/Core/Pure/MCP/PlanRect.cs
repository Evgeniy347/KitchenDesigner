namespace KitchenDesigner.Core.MCP
{
    internal readonly struct PlanRect
    {
        public readonly int X0;
        public readonly int Y0;
        public readonly int X1;
        public readonly int Y1;

        public PlanRect(int x0, int y0, int x1, int y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        public int Width => X1 - X0;

        public int Height => Y1 - Y0;

        public int CenterX => (X0 + X1) / 2;

        public int CenterY => (Y0 + Y1) / 2;
    }
}
