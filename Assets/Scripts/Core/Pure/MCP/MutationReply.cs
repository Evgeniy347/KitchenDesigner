using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    [Serializable]
    public class MutationReply
    {
        public bool ok = true;
        public string? id;
        public bool? unchanged;
        public string? @ref;
        public List<PlacementInfo>? placements;
        public List<object>? elements;
        public int? omittedCount;
        public ViolationDeltaInfo sceneViolationDelta = new ViolationDeltaInfo();
        public bool? dryRun;
        public bool? applied;
        public string? axis;
        public float? spacingMm;
        public List<string>? deleted;
        public int? matchedCount;
        public int? updatedCount;
        public List<string>? steps;
        public int? doneCount;
        public int? undoAvailableCount;
        public int? redoAvailableCount;
    }
}
