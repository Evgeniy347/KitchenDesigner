using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal static class SidebarPresetPreference
    {
        private const string KeyPrefix = "KitchenSidebarPreset_";

        internal static string? Load(string tileTitle)
        {
            string key = KeyPrefix + tileTitle;
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        internal static void Save(string tileTitle, string presetName)
        {
            PlayerPrefs.SetString(KeyPrefix + tileTitle, presetName);
            PlayerPrefs.Save();
        }
    }
}
