namespace KitchenDesigner.Core
{
    public static class SpecSections
    {
        public const string Furniture = "Мебель";
        public const string Plumbing = "Сантехника";
        public const string Walls = "Стены";
        public const string Foundation = "Фундамент";
        public const string Structures = "Конструкции";
        public const string PurchasedGoods = "Покупные изделия";

        public static readonly string[] All =
        {
            Furniture, Plumbing, Walls, Foundation, Structures, PurchasedGoods,
        };
    }
}
