namespace KitchenDesigner.Core
{
    public static class ElementSpecCoverage
    {
        public static bool IsCovered(bool selfQuantifies, bool isSpecificationParts,
            bool isFlatBoardElement, bool isKnownExclusion) =>
            selfQuantifies || isSpecificationParts || isFlatBoardElement || isKnownExclusion;
    }
}
