namespace KitchenDesigner.Core
{
    /// <summary>Зазоры проёмного бокса в миллиметрах. Ровно то, что нужно
    /// <see cref="GappedBox"/> от детали: раньше он принимал целиком PartData,
    /// а тот через MaterialCatalog тянет загрузку текстур — в ядро такое не
    /// проходит. Зазоры асимметричны, поэтому бокс может быть не центрирован
    /// относительно трансформа.</summary>
    public readonly struct BoxGaps
    {
        public readonly int Left;
        public readonly int Right;
        public readonly int Top;
        public readonly int Bottom;

        public BoxGaps(int left, int right, int top, int bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        public static BoxGaps None => new BoxGaps(0, 0, 0, 0);
    }
}
