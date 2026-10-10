namespace KitchenDesigner.Core.MCP
{
    internal sealed class RunModuleSize
    {
        public readonly string Name;
        public readonly RunKind Kind;
        public readonly int WidthMm;
        public readonly int HeightMm;
        public readonly int DepthMm;

        public RunModuleSize(string name, RunKind kind, int widthMm, int heightMm, int depthMm)
        {
            Name = name;
            Kind = kind;
            WidthMm = widthMm;
            HeightMm = heightMm;
            DepthMm = depthMm;
        }
    }
}
