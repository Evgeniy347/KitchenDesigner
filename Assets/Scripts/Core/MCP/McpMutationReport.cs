using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    internal sealed class McpMutationReport
    {
        public const int MaxPlacements = 20;

        private readonly List<string> _violatingBefore;

        private McpMutationReport(List<string> violatingBefore)
        {
            _violatingBefore = violatingBefore;
        }

        public static McpMutationReport Begin() => new McpMutationReport(ViolatingNames(Validate().vr));

        public MutationReply Finish(IReadOnlyList<KitchenElement>? changed = null, McpReference? reference = null, bool full = false)
        {
            var (all, vr) = Validate();
            var reply = new MutationReply
            {
                sceneViolationDelta = McpViolationDelta.Between(_violatingBefore, ViolatingNames(vr))
            };
            if (reference.HasValue && changed != null)
                FillPlacements(reply, changed, reference.Value, all, vr, full);
            return reply;
        }

        private static (List<KitchenElement> all, ValidationResult? vr) Validate()
        {
            var all = PartRegistry.GetAll();
            return (all, McpValidationCache.Get(all));
        }

        private static List<string> ViolatingNames(ValidationResult? vr)
        {
            var names = new List<string>();
            if (vr == null) return names;
            foreach (var el in vr.violations)
                if (el != null) names.Add(el.PartName);
            return names;
        }

        private static void FillPlacements(MutationReply reply, IReadOnlyList<KitchenElement> changed,
            McpReference reference, List<KitchenElement> all, ValidationResult? vr, bool full)
        {
            var builder = new McpPlacementBuilder(reference, all, vr);
            reply.@ref = reference.Canonical;
            reply.placements = new List<PlacementInfo>();
            if (full) reply.elements = new List<object>();
            int live = 0;
            foreach (var el in changed)
            {
                if (el == null) continue;
                live++;
                if (reply.placements.Count >= MaxPlacements) continue;
                reply.placements.Add(builder.Build(el));
                if (full) reply.elements!.Add(ElementInfoBuilder.Build(el, all, false, vr, reference));
            }
            if (live > MaxPlacements) reply.omittedCount = live - MaxPlacements;
        }
    }
}
