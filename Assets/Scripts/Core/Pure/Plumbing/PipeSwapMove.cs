namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct PipeSwapMove
    {
        public readonly int OldPortIndex;
        public readonly int NewPortIndex;

        public PipeSwapMove(int oldPortIndex, int newPortIndex)
        {
            OldPortIndex = oldPortIndex;
            NewPortIndex = newPortIndex;
        }
    }
}
