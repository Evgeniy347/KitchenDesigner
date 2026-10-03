namespace KitchenDesigner.Core
{
    internal static class PublisherMoveDecision
    {
        public static PublisherMoveStep Decide(bool oldHoldsData, bool newHoldsData) =>
            !oldHoldsData ? PublisherMoveStep.NothingToMove
            : newHoldsData ? PublisherMoveStep.BothHoldData
            : PublisherMoveStep.Move;
    }
}
