using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    internal static class EdgeStateMigration
    {
        public static int Apply(ElementData[]? data, List<KitchenElement?> restored)
        {
            if (data == null) return 0;

            SceneFaces? scene = null;
            int migrated = 0;
            int limit = data.Length < restored.Count ? data.Length : restored.Count;
            for (int i = 0; i < limit; i++)
            {
                var ed = data[i];
                var el = restored[i];
                if (ed == null || el == null) continue;
                if (ed.edgeSuppressedMask != EdgeStates.Unmigrated) continue;
                if (!el.SupportsEdges || el.EdgeForcedMask == 0) continue;

                scene ??= SceneFaces.Of(PartRegistry.GetAll());
                var coverage = EdgeBanding.Coverage(el, scene);
                foreach (EdgeSide side in EdgeStates.All)
                {
                    if (el.EdgeStateOf(side) != EdgeSideState.Forced) continue;
                    el.Data.SetEdgeState(side, EdgeSideState.Auto);
                    el.Data.SetEdgeState(side, EdgeBanding.HasEdgeEffective(el, coverage, side)
                        ? EdgeSideState.Forced
                        : EdgeSideState.Suppressed);
                }
                migrated++;
            }
            return migrated;
        }
    }
}
