using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public sealed class PipeSwapPlan
    {
        private readonly List<PipeSwapMove> _kept;
        private readonly List<int> _brokenPorts;
        private readonly List<int> _freeNewPorts;

        public PipeSwapPlan(List<PipeSwapMove> kept, List<int> brokenPorts, List<int> freeNewPorts)
        {
            _kept = kept;
            _brokenPorts = brokenPorts;
            _freeNewPorts = freeNewPorts;
        }

        public IReadOnlyList<PipeSwapMove> Kept => _kept;

        public IReadOnlyList<int> BrokenPorts => _brokenPorts;

        public IReadOnlyList<int> FreeNewPorts => _freeNewPorts;

        public bool Keeps(int oldPortIndex) => NewPortFor(oldPortIndex) != PipeKindSwap.NoPort;

        public int NewPortFor(int oldPortIndex)
        {
            for (int i = 0; i < _kept.Count; i++)
                if (_kept[i].OldPortIndex == oldPortIndex) return _kept[i].NewPortIndex;
            return PipeKindSwap.NoPort;
        }
    }
}
