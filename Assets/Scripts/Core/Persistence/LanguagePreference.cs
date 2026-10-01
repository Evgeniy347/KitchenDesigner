namespace KitchenDesigner.Core
{
    internal static class LanguagePreference
    {
        internal const string Key = "Language";

        internal static string? Load()
        {
            var store = PreferenceStore.Current;
            return store.Has(Key) ? store.GetString(Key, "") : null;
        }

        internal static void Save(string language) => PreferenceStore.Current.SetString(Key, language);
    }
}
