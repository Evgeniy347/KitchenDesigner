using System.Collections.Generic;

namespace KitchenDesigner.Core.Analysis
{
    public static class PipeFittingSizeLink
    {
        public static int ApplyAll(IReadOnlyList<KitchenElement> scene)
        {
            if (scene == null || !CarriesAFitting(scene)) return 0;

            var survey = ScenePipeSurvey.Of(scene);

            int changed = 0;
            for (int i = 0; i < scene.Count; i++)
            {
                if (!(scene[i] is PipeFittingElement fitting)) continue;
                if (!fitting.TakeBoreSizes(survey.SizesOfElement(fitting.PartName))) continue;
                fitting.ApplyDimensions();
                changed++;
            }

            return changed;
        }

        private static bool CarriesAFitting(IReadOnlyList<KitchenElement> scene)
        {
            for (int i = 0; i < scene.Count; i++)
                if (scene[i] is PipeFittingElement) return true;
            return false;
        }
    }
}
