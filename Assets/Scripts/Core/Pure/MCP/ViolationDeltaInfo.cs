using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    [Serializable]
    public class ViolationDeltaInfo
    {
        public List<string> added = new List<string>();
        public List<string> removed = new List<string>();
    }
}
