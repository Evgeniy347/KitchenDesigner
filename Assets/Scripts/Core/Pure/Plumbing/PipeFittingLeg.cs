namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct PipeFittingLeg
    {
        public readonly PipeAxis Axis;
        public readonly float LengthMm;

        public PipeFittingLeg(in PipeAxis axis, float lengthMm)
        {
            Axis = axis;
            LengthMm = lengthMm;
        }
    }
}
