using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class FloorDrop
    {
        public static float ContactUnits => Tolerance.ContactMm * AppConstants.MM_TO_UNITS;

        public static float? SupportTopUnder(Span footprintX, Span footprintZ, float bottomY,
            IReadOnlyList<FloorSupport> supports, float? levelFloorY = null)
        {
            if (supports == null) return levelFloorY;

            float highestThatCanCarry = bottomY + ContactUnits;
            float best = float.MinValue;
            bool found = false;
            foreach (var support in supports)
            {
                if (support.TopY > highestThatCanCarry) continue;
                if (found && support.TopY <= best) continue;
                if (!support.CarriesFootprint(footprintX, footprintZ)) continue;
                best = support.TopY;
                found = true;
            }
            return found ? best : levelFloorY;
        }

        public static bool WorthSeating(float bottomY, float supportTopY) =>
            bottomY - supportTopY > ContactUnits;

        public static float SeatedCentreY(float centreY, float bottomY, float supportTopY) =>
            centreY - (bottomY - supportTopY);
    }
}
