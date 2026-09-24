namespace KitchenDesigner.Core.Ports
{
    public readonly struct PortLink
    {
        public readonly int APortIndex;
        public readonly int BPortIndex;

        public PortLink(int aPortIndex, int bPortIndex)
        {
            APortIndex = aPortIndex;
            BPortIndex = bPortIndex;
        }
    }
}
