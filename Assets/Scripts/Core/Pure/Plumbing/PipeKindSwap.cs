using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeKindSwap
    {
        public const int NoPort = -1;

        public static PipeSwapPlan Plan(IReadOnlyList<PipePort?> neighbourPorts,
            IReadOnlyList<PipePort> newPorts)
        {
            var kept = new List<PipeSwapMove>();
            var brokenPorts = new List<int>();
            var taken = new bool[newPorts.Count];

            for (int oldPortIndex = 0; oldPortIndex < neighbourPorts.Count; oldPortIndex++)
            {
                var neighbour = neighbourPorts[oldPortIndex];
                if (neighbour == null) continue;

                int match = FirstFreeMatch(newPorts, taken, neighbour.Value);
                if (match == NoPort)
                {
                    brokenPorts.Add(oldPortIndex);
                    continue;
                }

                taken[match] = true;
                kept.Add(new PipeSwapMove(oldPortIndex, match));
            }

            var freeNewPorts = new List<int>();
            for (int i = 0; i < taken.Length; i++)
                if (!taken[i]) freeNewPorts.Add(i);

            return new PipeSwapPlan(kept, brokenPorts, freeNewPorts);
        }

        public static int AnchorPortIndex(IReadOnlyList<PipePort?> neighbourPorts)
        {
            for (int i = 0; i < neighbourPorts.Count; i++)
                if (neighbourPorts[i] != null) return i;
            return NoPort;
        }

        private static int FirstFreeMatch(IReadOnlyList<PipePort> newPorts, bool[] taken,
            in PipePort neighbour)
        {
            for (int i = 0; i < newPorts.Count; i++)
            {
                if (taken[i]) continue;
                if (!PipeJoint.Connects(newPorts[i], neighbour)) continue;
                return i;
            }

            return NoPort;
        }
    }
}
