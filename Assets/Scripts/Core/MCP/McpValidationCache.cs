using System.Collections.Generic;

namespace KitchenDesigner.Core.MCP
{
    public static class McpValidationCache
    {
        private static readonly McpRevisionCache<ValidationResult> _cache = new McpRevisionCache<ValidationResult>();

        public static ValidationResult? Get(List<KitchenElement>? all)
        {
            if (all == null || all.Count == 0) return null;
            return _cache.Get(SceneRevision.Version, () => ConstraintValidator.Validate(all));
        }

        public static int TakeRecomputeCount() => _cache.TakeRecomputeCount();

        public static void ResetForTests() => _cache.Reset();
    }
}
