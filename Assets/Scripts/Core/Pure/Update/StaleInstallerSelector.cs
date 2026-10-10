using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Update
{
    public static class StaleInstallerSelector
    {
        public static IReadOnlyList<string> Select(
            IReadOnlyList<FolderEntry> entries, string currentVersion, string? targetVersion)
        {
            var stale = new List<string>();
            foreach (var entry in entries)
            {
                if (IsStale(entry.Name, currentVersion, targetVersion)) stale.Add(entry.Name);
            }
            stale.Sort(StringComparer.Ordinal);
            return stale;
        }

        private static bool IsStale(string name, string currentVersion, string? targetVersion)
        {
            if (InstallerFileName.TryParsePart(name, out _)) return true;
            if (!InstallerFileName.TryParseInstaller(name, out var version)) return false;
            if (VersionUtil.Compare(version, currentVersion) <= 0) return true;
            return targetVersion != null && VersionUtil.Compare(version, targetVersion) < 0;
        }
    }
}
