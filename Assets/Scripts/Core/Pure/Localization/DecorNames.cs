namespace KitchenDesigner.Core
{
    public static class DecorNames
    {
        public const string KeyPrefix = "decor.name.";

        public static string Of(string decorId, string catalogName) =>
            Loc.Current.TryGet(KeyPrefix + decorId, out var text) ? text : catalogName;
    }
}
