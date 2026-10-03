using System;

namespace KitchenDesigner.Core
{
    internal static class PublisherMigration
    {
        public static PublisherMoveOutcome Run(IPublisherStore store, PublisherMigrationLog log)
        {
            try
            {
                switch (PublisherMoveDecision.Decide(store.OldHoldsData(), store.NewHoldsData()))
                {
                    case PublisherMoveStep.Move:
                        store.Move();
                        log.Info("[Publisher] moved: " + store.Describe);
                        return PublisherMoveOutcome.Moved;
                    case PublisherMoveStep.BothHoldData:
                        log.Warning("[Publisher] both the old and the new place hold data, left untouched: " + store.Describe);
                        return PublisherMoveOutcome.LeftBothAlone;
                    default:
                        return PublisherMoveOutcome.NothingToMove;
                }
            }
            catch (Exception e)
            {
                log.Error("[Publisher] move failed, the old data stays where it was: " + store.Describe + ": " + e.Message);
                return PublisherMoveOutcome.Failed;
            }
        }
    }
}
