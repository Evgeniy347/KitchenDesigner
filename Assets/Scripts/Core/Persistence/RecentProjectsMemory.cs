using UnityEngine;

namespace KitchenDesigner.Core
{
    internal interface IRecentProjectsMemory
    {
        string[] Values { get; set; }
    }

    internal static class RecentProjectsMemory
    {
        internal const string Key = "KitchenRecentProjects";

        internal static IRecentProjectsMemory For(string[]? commandLineArgs) =>
            new RecentProjectsInPreferences(PreferenceStore.For(commandLineArgs));

        internal static void Remember(string[]? commandLineArgs, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var store = For(commandLineArgs);
            store.Values = RecentProjectsList.WithPromoted(store.Values, path);
        }
    }

    internal sealed class RecentProjectsInPreferences : IRecentProjectsMemory
    {
        [System.Serializable]
        private class Wrapper
        {
            public string[] paths = new string[0];
        }

        private readonly IPreferenceStore _preferences;

        internal RecentProjectsInPreferences(IPreferenceStore preferences) => _preferences = preferences;

        public string[] Values
        {
            get
            {
                string json = _preferences.GetString(RecentProjectsMemory.Key, "");
                if (string.IsNullOrEmpty(json)) return new string[0];
                try
                {
                    var wrapper = JsonUtility.FromJson<Wrapper>(json);
                    return wrapper?.paths ?? new string[0];
                }
                catch (System.ArgumentException)
                {
                    return new string[0];
                }
            }
            set
            {
                var wrapper = new Wrapper { paths = value ?? new string[0] };
                _preferences.SetString(RecentProjectsMemory.Key, JsonUtility.ToJson(wrapper));
            }
        }
    }
}
