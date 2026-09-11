namespace KitchenDesigner.Core
{
    internal static class FirstRunMarker
    {
        internal const string Key = "KitchenFirstRunDone";

        internal static bool Recorded => PreferenceStore.Current.GetInt(Key, 0) != 0;

        internal static void Record(string[]? commandLineArgs) =>
            PreferenceStore.For(commandLineArgs).SetInt(Key, 1);
    }
}
