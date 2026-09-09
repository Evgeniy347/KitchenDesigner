using System;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal static class SidebarDockPreference
    {
        private const string Key = "KitchenSidebarDockChoice";

        internal static SidebarDockChoice Load()
        {
            int raw = PlayerPrefs.GetInt(Key, (int)SidebarDockChoice.Unset);
            return Enum.IsDefined(typeof(SidebarDockChoice), raw)
                ? (SidebarDockChoice)raw
                : SidebarDockChoice.Unset;
        }

        internal static void Save(SidebarDockChoice choice)
        {
            PlayerPrefs.SetInt(Key, (int)choice);
            PlayerPrefs.Save();
        }
    }
}
