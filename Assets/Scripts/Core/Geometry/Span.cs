namespace KitchenDesigner.Core
{
    public readonly struct Span
    {
        public readonly float Min;
        public readonly float Max;

        public Span(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Size => Max - Min;

        public static Span FromCenter(float center, float size) =>
            new Span(center - size * 0.5f, center + size * 0.5f);
    }
}
