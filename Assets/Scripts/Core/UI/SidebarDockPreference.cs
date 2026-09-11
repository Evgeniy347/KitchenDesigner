using System;

namespace KitchenDesigner.Core.UI
{
    internal static class SidebarDockPreference
    {
        internal const string Key = "KitchenSidebarDockChoice";

        internal static SidebarDockChoice Load()
        {
            int raw = PreferenceStore.Current.GetInt(Key, (int)SidebarDockChoice.Unset);
            return Enum.IsDefined(typeof(SidebarDockChoice), raw)
                ? (SidebarDockChoice)raw
                : SidebarDockChoice.Unset;
        }

        internal static void Save(SidebarDockChoice choice) =>
            PreferenceStore.Current.SetInt(Key, (int)choice);
    }
}
