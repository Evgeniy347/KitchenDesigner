using System;

namespace KitchenDesigner.Core
{
    public sealed class AutoSaveRevisionGate
    {
        private int _savedRevision;

        public AutoSaveRevisionGate(int savedRevision) => _savedRevision = savedRevision;

        public void MarkSaved(int revision) => _savedRevision = revision;

        public AutoSaveOutcome Run(int revision,
            Func<string> captureSnapshot, Func<string, bool> writeSnapshot, string lastSavedJson)
        {
            if (revision == _savedRevision) return new AutoSaveOutcome(false, lastSavedJson);

            bool writeFailed = false;
            var outcome = AutoSaveCycle.Run(captureSnapshot, json =>
            {
                bool written = writeSnapshot(json);
                writeFailed = !written;
                return written;
            }, lastSavedJson);

            if (!writeFailed) _savedRevision = revision;
            return outcome;
        }
    }
}
