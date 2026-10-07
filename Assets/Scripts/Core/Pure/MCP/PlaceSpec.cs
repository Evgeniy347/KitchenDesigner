using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class PlaceSpec
    {
        public string Name = string.Empty;
        public string? On;
        public float LiftMm;
        public readonly List<PlaceAgainstSpec> Against = new List<PlaceAgainstSpec>();
        public readonly List<PlaceAlignSpec> Align = new List<PlaceAlignSpec>();
    }
}
