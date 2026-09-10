using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core
{
    public static class WallQuantitySurvey
    {
        public static IEnumerable<SpecItem> Items(Wall wall)
        {
            if (wall == null) return new List<SpecItem>();

            var element = wall.GetComponent<KitchenElement>();
            var dims = element != null ? element.DimensionsMM : Vector3Int.zero;

            return WallSpecItems.Of(wall.Masonry,
                WallCentreline.LengthMM(dims), dims.y, WallCentreline.ThicknessMM(dims),
                Openings(wall), wall.JointMm, wall.WastePct);
        }

        public static List<WallOpening> Openings(Wall wall)
        {
            var openings = new List<WallOpening>();
            if (wall == null) return openings;

            foreach (var window in wall.AttachedWindows)
                Add(openings, window);
            foreach (var door in wall.AttachedDoors)
                Add(openings, door);

            return openings;
        }

        private static void Add(List<WallOpening> openings, KitchenElement? opening)
        {
            if (opening == null) return;
            var dims = opening.DimensionsMM;
            openings.Add(new WallOpening(opening.PartName, dims.x, dims.y));
        }
    }
}
