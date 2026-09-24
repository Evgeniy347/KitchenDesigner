namespace KitchenDesigner.Core.UI
{
    public static class LoadWindowLayout
    {
        public const float MaxScreenHeightFraction = 0.5f;
        public const float MinHeight = 220f;

        public static float HeightFor(float preferredHeight, float availableScreenHeight)
        {
            float cap = availableScreenHeight * MaxScreenHeightFraction;
            if (cap < MinHeight) cap = MinHeight;
            return preferredHeight < cap ? preferredHeight : cap;
        }
    }
}
