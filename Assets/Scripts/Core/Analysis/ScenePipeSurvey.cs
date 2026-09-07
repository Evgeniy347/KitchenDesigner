using System.Collections.Generic;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core.Analysis
{
    public static class ScenePipeSurvey
    {
        public static PipeSurvey Of(IReadOnlyList<KitchenElement> scene) =>
            PipeSurvey.Of(new ScenePipeSnapshot(scene).Ports());

        public static IReadOnlyList<string?> SizesOf(KitchenElement? element) =>
            element is PipeFittingElement fitting
                ? fitting.BoreSizeIds
                : new string?[0];

        public static string DesignationAt(IReadOnlyList<string?> sizes, int portIndex) =>
            portIndex >= 0 && portIndex < sizes.Count
                ? PipeSpec.DesignationOrDash(sizes[portIndex])
                : PipeSpec.NoValue;
    }
}
