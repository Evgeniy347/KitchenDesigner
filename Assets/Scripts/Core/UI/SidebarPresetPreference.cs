namespace KitchenDesigner.Core.UI
{
    internal static class SidebarPresetPreference
    {
        internal const string KeyPrefix = "KitchenSidebarPreset_";

        internal static string? Load(string tileTitle)
        {
            string key = KeyPrefix + tileTitle;
            return PreferenceStore.Current.Has(key) ? PreferenceStore.Current.GetString(key, "") : null;
        }

        internal static void Save(string tileTitle, string presetName) =>
            PreferenceStore.Current.SetString(KeyPrefix + tileTitle, presetName);
    }
}
