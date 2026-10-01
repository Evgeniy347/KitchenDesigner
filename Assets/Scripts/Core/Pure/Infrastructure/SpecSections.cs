namespace KitchenDesigner.Core
{
    public static class SpecSections
    {
        public static string Furniture => Loc.T("spec.section.furniture");
        public static string Plumbing => Loc.T("spec.section.plumbing");
        public static string Walls => Loc.T("spec.section.walls");
        public static string Foundation => Loc.T("spec.section.foundation");
        public static string Structures => Loc.T("spec.section.structures");
        public static string PurchasedGoods => Loc.T("spec.section.purchasedGoods");

        public static string[] All => new[]
        {
            Furniture, Plumbing, Walls, Foundation, Structures, PurchasedGoods,
        };
    }
}
