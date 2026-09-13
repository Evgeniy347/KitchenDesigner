using System;

namespace KitchenDesigner.Core
{
    public readonly struct AutoSaveOutcome
    {
        public readonly bool Wrote;
        public readonly string LastSavedJson;

        public AutoSaveOutcome(bool wrote, string lastSavedJson)
        {
            Wrote = wrote;
            LastSavedJson = lastSavedJson;
        }
    }

    public static class AutoSaveCycle
    {
        public static AutoSaveOutcome Run(
            Func<string> captureSnapshot, Func<string, bool> writeSnapshot, string lastSavedJson)
        {
            string snapshot = captureSnapshot();
            if (string.Equals(snapshot, lastSavedJson, StringComparison.Ordinal))
                return new AutoSaveOutcome(false, lastSavedJson);

            if (!writeSnapshot(snapshot))
                return new AutoSaveOutcome(false, lastSavedJson);

            return new AutoSaveOutcome(true, snapshot);
        }
    }
}
