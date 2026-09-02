namespace KitchenDesigner.Core
{
    public readonly struct SoftSlabRing
    {
        public readonly float Inset;
        public readonly float Y;
        public readonly float Radial;
        public readonly float Up;

        public SoftSlabRing(float inset, float y, float radial, float up)
        {
            Inset = inset;
            Y = y;
            Radial = radial;
            Up = up;
        }
    }
}
