using System.Collections.Generic;

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
    }
}
