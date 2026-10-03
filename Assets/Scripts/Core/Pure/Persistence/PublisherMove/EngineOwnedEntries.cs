using System;

namespace KitchenDesigner.Core
{
    internal static class EngineOwnedEntries
    {
        private static readonly string[] Files = { "Player.log", "Player-prev.log", "Unity" };

        public static bool IsEnginePreference(string valueName) =>
            valueName.StartsWith("Screenmanager", StringComparison.Ordinal)
            || valueName.StartsWith("unity", StringComparison.OrdinalIgnoreCase);

        public static bool IsEngineFile(string entryName) =>
            Array.Exists(Files, f => string.Equals(f, entryName, StringComparison.OrdinalIgnoreCase));
    }
}
