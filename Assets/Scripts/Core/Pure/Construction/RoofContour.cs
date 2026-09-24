using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public static class RoofContour
    {
        public static RoofFootprint BoundingFootprint(IReadOnlyList<WallCentreline>? centrelines)
        {
            if (centrelines == null) return default;

            bool any = false;
            float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;

            foreach (var line in centrelines)
            {
                if (!line.IsDefined) continue;

                any = Grow(line.Start.x, line.Start.z, any, ref minX, ref maxX, ref minZ, ref maxZ);
                any = Grow(line.End.x, line.End.z, any, ref minX, ref maxX, ref minZ, ref maxZ);
            }

            if (!any) return default;

            return new RoofFootprint(
                minX / AppConstants.MM_TO_UNITS, maxX / AppConstants.MM_TO_UNITS,
                minZ / AppConstants.MM_TO_UNITS, maxZ / AppConstants.MM_TO_UNITS);
        }

        private static bool Grow(float x, float z, bool any,
            ref float minX, ref float maxX, ref float minZ, ref float maxZ)
        {
            if (!any)
            {
                minX = x;
                maxX = x;
                minZ = z;
                maxZ = z;
                return true;
            }

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
            return true;
        }
    }
}
