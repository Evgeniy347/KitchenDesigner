namespace KitchenDesigner.Core.Construction
{
    public readonly struct RoofFootprint
    {
        public readonly float MinXMm;
        public readonly float MaxXMm;
        public readonly float MinZMm;
        public readonly float MaxZMm;

        public RoofFootprint(float minXMm, float maxXMm, float minZMm, float maxZMm)
        {
            MinXMm = minXMm;
            MaxXMm = maxXMm;
            MinZMm = minZMm;
            MaxZMm = maxZMm;
        }

        public float WidthXMm => MaxXMm - MinXMm;

        public float LengthZMm => MaxZMm - MinZMm;

        public bool LongAxisIsX => WidthXMm >= LengthZMm;
    }
}
