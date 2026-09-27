using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class FoundationHostLink
    {
        private static readonly List<FoundationElement> _foundations = new List<FoundationElement>();

        public static int ApplyAll(IReadOnlyList<KitchenElement> scene)
        {
            if (scene == null) return 0;

            _foundations.Clear();
            for (int i = 0; i < scene.Count; i++)
                if (scene[i] is FoundationElement f && f != null) _foundations.Add(f);
            if (_foundations.Count == 0) return 0;

            var centrelines = FoundationWallSurvey.LoadBearingCentrelinesOnLowestLevel(scene);

            int rebuilt = 0;
            for (int i = 0; i < _foundations.Count; i++)
            {
                var foundation = _foundations[i];
                if (FoundationWallSurvey.SameCentrelines(foundation.LastBuiltCentrelines, centrelines))
                    continue;
                foundation.ApplyDimensions();
                rebuilt++;
            }

            _foundations.Clear();
            return rebuilt;
        }
    }
}
