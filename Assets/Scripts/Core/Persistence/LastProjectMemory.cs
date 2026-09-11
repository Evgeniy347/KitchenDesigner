namespace KitchenDesigner.Core
{
    internal interface ILastProjectMemory
    {
        string Value { get; set; }
    }

    internal static class LastProjectMemory
    {
        internal const string Key = "KitchenLastSavePath";

        internal static ILastProjectMemory For(string[]? commandLineArgs) =>
            new LastProjectInPreferences(PreferenceStore.For(commandLineArgs));
    }

    internal sealed class LastProjectInPreferences : ILastProjectMemory
    {
        private readonly IPreferenceStore _preferences;

        internal LastProjectInPreferences(IPreferenceStore preferences) =>
            _preferences = preferences;

        public string Value
        {
            get => _preferences.GetString(LastProjectMemory.Key, "");
            set => _preferences.SetString(LastProjectMemory.Key, value ?? "");
        }
    }
}
