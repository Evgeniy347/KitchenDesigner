using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    [Serializable]
    public class PlanReply
    {
        public bool ok = true;
        public string? id;
        public List<string>? created;
        public int? updatedCount;
        public int? deletedCount;
        public int? elementCount;
        public int? wallCount;
        public int? floorCount;
        public int? openingCount;
        public int? roomCount;
        public ViolationDeltaInfo sceneViolationDelta = new ViolationDeltaInfo();
    }
}
