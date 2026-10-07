using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    [Serializable]
    public class PlacementInfo
    {
        public string name = string.Empty;
        public float[] posMm = Array.Empty<float>();
        public float[] footprintMm = Array.Empty<float>();
        public string? on;
        public List<PlacementContact>? touches;
        public List<PlacementGap>? gaps;
        public string? room;
        public string? level;
        public List<string> issues = new List<string>();
    }
}
