namespace KitchenDesigner.Core.Ventilation
{
    public static class DuctRectSizesAwaitingConfirmation
    {
        public static readonly (int WidthMm, int HeightMm)[] Candidates =
        {
            (100, 150),
            (100, 200),
            (150, 150),
            (150, 200),
            (200, 200),
            (200, 300),
            (300, 300),
        };
    }
}
