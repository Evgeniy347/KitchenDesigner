namespace KitchenDesigner.Core.MCP
{
    public readonly struct NeighbourBox
    {
        public readonly string Name;
        public readonly BoxMm Box;

        public NeighbourBox(string name, BoxMm box)
        {
            Name = name;
            Box = box;
        }
    }
}
