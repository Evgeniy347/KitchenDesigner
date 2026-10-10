namespace KitchenDesigner.Core.MCP
{
    internal readonly struct RunPlaced
    {
        public readonly string Name;
        public readonly RunKind Kind;
        public readonly BoxMm Box;

        public RunPlaced(string name, RunKind kind, BoxMm box)
        {
            Name = name;
            Kind = kind;
            Box = box;
        }
    }
}
