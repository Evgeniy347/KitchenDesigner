using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class FoundationWallSurvey
    {
        public static List<WallCentreline> LoadBearingCentrelines(
            IReadOnlyList<KitchenElement>? allElements)
        {
            var centrelines = new List<WallCentreline>();
            if (allElements == null) return centrelines;

            foreach (var element in allElements)
            {
                if (element == null) continue;
                var wall = element.GetComponent<Wall>();
                if (wall == null || !wall.LoadBearing) continue;

                var centreline = WallCentreline.Of(wall.FullPosition, element.transform.rotation,
                    element.DimensionsMM);
                if (centreline.IsDefined) centrelines.Add(centreline);
            }

            return centrelines;
        }

        public static List<KitchenElement> OnLowestLevel(IReadOnlyList<KitchenElement>? allElements)
        {
            var result = new List<KitchenElement>();
            if (allElements == null) return result;

            var bottomLevel = LevelResolution.BottomLevel(LevelRegistry.Snapshot());
            foreach (var element in allElements)
                if (element != null && LevelRegistry.LevelOf(element).id == bottomLevel.id)
                    result.Add(element);

            return result;
        }

        public static List<WallCentreline> LoadBearingCentrelinesOnLowestLevel(
            IReadOnlyList<KitchenElement>? allElements) =>
            LoadBearingCentrelines(OnLowestLevel(allElements));

        public static bool SameCentrelines(IReadOnlyList<WallCentreline> a, IReadOnlyList<WallCentreline> b)
        {
            if (a == null || b == null) return a == b;
            if (a.Count != b.Count) return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].IsDefined != b[i].IsDefined) return false;
                if (a[i].IsDefined && (a[i].Start != b[i].Start || a[i].End != b[i].End)) return false;
            }

            return true;
        }
    }
}
