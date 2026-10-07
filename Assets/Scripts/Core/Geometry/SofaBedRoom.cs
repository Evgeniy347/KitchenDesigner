using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public readonly struct SofaBedBlock
    {
        public readonly int Sofa;
        public readonly int Blocker;
        public readonly float TravelMM;

        public SofaBedBlock(int sofa, int blocker, float travelMM)
        {
            Sofa = sofa;
            Blocker = blocker;
            TravelMM = travelMM;
        }
    }

    public static class SofaBedRoom
    {
        public static List<SofaBedBlock> Blocks(IReadOnlyList<ValidationElement> all)
        {
            var blocks = new List<SofaBedBlock>();
            if (all == null || !AnySofa(all)) return blocks;

            ValidationBroadPhase.Clear();
            var pairs = ValidationBroadPhase.ReachingPairsInNestedLoopOrder(all,
                ValidationCore.ContactDistUnits);
            for (int p = 0; p < pairs.Count; p++)
            {
                AddIfBlocked(all, pairs[p].lo, pairs[p].hi, blocks);
                AddIfBlocked(all, pairs[p].hi, pairs[p].lo, blocks);
            }
            ValidationBroadPhase.Clear();

            blocks.Sort((a, b) => a.Sofa != b.Sofa
                ? a.Sofa.CompareTo(b.Sofa) : a.Blocker.CompareTo(b.Blocker));
            return blocks;
        }

        private static bool AnySofa(IReadOnlyList<ValidationElement> all)
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i].BedReach != null) return true;
            return false;
        }

        private static void AddIfBlocked(IReadOnlyList<ValidationElement> all, int sofaIdx,
            int otherIdx, List<SofaBedBlock> blocks)
        {
            var reach = all[sofaIdx].BedReach;
            if (reach == null) return;

            var other = all[otherIdx];
            if (other.StaysOutOfTheBedsWay) return;
            if (reach.IsBlockedBy(other.Geometry)
                || (other.HasExtraBody && reach.IsBlockedBy(other.ExtraBody)))
                blocks.Add(new SofaBedBlock(sofaIdx, otherIdx, reach.TravelMM));
        }
    }
}
