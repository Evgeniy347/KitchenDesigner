using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal static class SidebarLastGroupPreference
    {
        private const string Key = "KitchenSidebarLastGroup";

        internal static string? Load()
            => PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;

        internal static void Save(string groupTitle)
        {
            PlayerPrefs.SetString(Key, groupTitle);
            PlayerPrefs.Save();
        }

        internal static void ClearForTests()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
