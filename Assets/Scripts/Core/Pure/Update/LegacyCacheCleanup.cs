using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Update
{
    public static class LegacyCacheCleanup
    {
        public static bool Run(IUpdateFolder legacyFolder, IUpdateConsole console)
        {
            IReadOnlyList<FolderEntry> entries;
            try
            {
                entries = legacyFolder.List();
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                console.Write(UpdateLogLevel.Warning, UpdateMessages.FolderUnreadable(e.Message));
                return false;
            }

            var ours = new List<string>();
            foreach (var entry in entries)
                if (InstallerFileName.IsOurs(entry.Name)) ours.Add(entry.Name);
            if (ours.Count == 0) return true;

            ours.Sort(StringComparer.Ordinal);
            console.Write(UpdateLogLevel.Info, UpdateMessages.LegacyCleaning(ours));
            bool complete = true;
            foreach (var name in ours)
            {
                if (legacyFolder.TryDelete(name, out var error)) continue;
                console.Write(UpdateLogLevel.Warning, UpdateMessages.DeleteFailed(name, error));
                complete = false;
            }
            return complete;
        }
    }
}
