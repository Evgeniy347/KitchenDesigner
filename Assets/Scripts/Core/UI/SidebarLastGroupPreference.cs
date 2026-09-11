namespace KitchenDesigner.Core.UI
{
    internal static class SidebarLastGroupPreference
    {
        internal const string Key = "KitchenSidebarLastGroup";

        internal static string? Load()
            => PreferenceStore.Current.Has(Key) ? PreferenceStore.Current.GetString(Key, "") : null;

        internal static void Save(string groupTitle) =>
            PreferenceStore.Current.SetString(Key, groupTitle);

        internal static void ClearForTests() => PreferenceStore.Current.Delete(Key);
    }
}
