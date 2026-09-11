using UnityEngine;

namespace KitchenDesigner.Core
{
    internal interface ILastProjectMemory
    {
        string Value { get; set; }
    }

    internal static class LastProjectMemory
    {
        internal static ILastProjectMemory For(string[]? commandLineArgs) =>
            EphemeralSessionArgument.Parse(commandLineArgs)
                ? new SessionOnlyLastProject()
                : (ILastProjectMemory)new StoredLastProject();
    }

    internal sealed class SessionOnlyLastProject : ILastProjectMemory
    {
        private string _value = "";

        public string Value
        {
            get => _value;
            set => _value = value ?? "";
        }
    }

    internal sealed class StoredLastProject : ILastProjectMemory
    {
        internal const string Key = "KitchenLastSavePath";

        public string Value
        {
            get => PlayerPrefs.GetString(Key, "");
            set
            {
                PlayerPrefs.SetString(Key, value ?? "");
                PlayerPrefs.Save();
            }
        }
    }
}
