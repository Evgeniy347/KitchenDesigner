using System.Collections.Generic;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core.Analysis
{
    public static class ScenePipeSurvey
    {
        public static PipeSurvey Current() =>
            PipeSurvey.Of(new ScenePipeSnapshot(PartRegistry.All).Ports());

        public static IReadOnlyList<string?> SizesOf(KitchenElement? element) =>
            element is PipeFittingElement
                ? Current().SizesOfElement(element.PartName)
                : new string?[0];

        public static string DesignationAt(IReadOnlyList<string?> sizes, int portIndex) =>
            portIndex >= 0 && portIndex < sizes.Count
                ? PipeSpec.DesignationOrDash(sizes[portIndex])
                : PipeSpec.NoValue;
    }
}
