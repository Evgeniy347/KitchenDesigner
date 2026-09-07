namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct PipeLink
    {
        public readonly int APortIndex;
        public readonly int BPortIndex;

        public PipeLink(int aPortIndex, int bPortIndex)
        {
            APortIndex = aPortIndex;
            BPortIndex = bPortIndex;
        }
    }
}
