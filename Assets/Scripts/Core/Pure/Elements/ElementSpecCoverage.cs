using System;

namespace KitchenDesigner.Core
{
    [Flags]
    public enum SpecRoute
    {
        None = 0,
        Quantifies = 1,
        SpecificationParts = 2,
        FlatBoard = 4,
    }

    public static class ElementSpecCoverage
    {
        public static SpecRoute Declared(bool selfQuantifies, bool isSpecificationParts,
            bool isFlatBoardElement)
        {
            var routes = SpecRoute.None;
            if (selfQuantifies) routes |= SpecRoute.Quantifies;
            if (isSpecificationParts) routes |= SpecRoute.SpecificationParts;
            if (isFlatBoardElement) routes |= SpecRoute.FlatBoard;
            return routes;
        }

        public static SpecRoute Taken(SpecRoute declared)
        {
            if ((declared & SpecRoute.Quantifies) != 0) return SpecRoute.Quantifies;
            if ((declared & SpecRoute.SpecificationParts) != 0) return SpecRoute.SpecificationParts;
            if ((declared & SpecRoute.FlatBoard) != 0) return SpecRoute.FlatBoard;
            return SpecRoute.None;
        }

        public static SpecRoute Dead(SpecRoute declared) => declared & ~Taken(declared);

        public static bool IsCovered(int specLineCount) => specLineCount > 0;
    }
}
