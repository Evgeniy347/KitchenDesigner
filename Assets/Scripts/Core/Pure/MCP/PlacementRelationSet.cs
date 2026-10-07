using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class PlacementRelationSet
    {
        public string? On;
        public readonly List<PlacementContact> Touches = new List<PlacementContact>();
        public readonly List<PlacementGap> Gaps = new List<PlacementGap>();
        public readonly List<PlacementOverlap> Overlaps = new List<PlacementOverlap>();
    }
}
