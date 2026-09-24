namespace KitchenDesigner.Core.Construction
{
    public readonly struct FenceRun
    {
        public readonly string Id;
        public readonly float LengthMm;

        public FenceRun(string id, float lengthMm)
        {
            Id = id;
            LengthMm = lengthMm;
        }
    }
}
