using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Update
{
    public enum UpdateLogLevel { Info, Warning, Error }

    public enum UpdateActionKind
    {
        Log,
        VerifyExisting,
        VerifyDownloaded,
        VerifyBeforeApply,
        Download,
        Delete,
        Cleanup,
        Promote,
        ShowDialog,
        Apply,
    }

    public sealed class UpdateAction
    {
        private UpdateAction(UpdateActionKind kind)
        {
            Kind = kind;
        }

        public UpdateActionKind Kind { get; }
        public UpdateLogLevel Level { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public string FileName { get; private set; } = string.Empty;
        public string PartName { get; private set; } = string.Empty;
        public string Version { get; private set; } = string.Empty;
        public string Url { get; private set; } = string.Empty;
        public int Attempt { get; private set; }
        public InstallerExpectation Expectation { get; private set; }
        public IReadOnlyList<string> Files { get; private set; } = Array.Empty<string>();

        public static UpdateAction Log(UpdateLogLevel level, string message) =>
            new UpdateAction(UpdateActionKind.Log) { Level = level, Message = message };

        public static UpdateAction Verify(
            UpdateActionKind kind, string fileName, InstallerExpectation expectation) =>
            new UpdateAction(kind) { FileName = fileName, Expectation = expectation };

        public static UpdateAction Download(
            string version, string url, string partName, InstallerExpectation expectation, int attempt) =>
            new UpdateAction(UpdateActionKind.Download)
            {
                Version = version,
                Url = url,
                PartName = partName,
                Expectation = expectation,
                Attempt = attempt,
            };

        public static UpdateAction Delete(string fileName) =>
            new UpdateAction(UpdateActionKind.Delete) { FileName = fileName };

        public static UpdateAction Cleanup(IReadOnlyList<string> files) =>
            new UpdateAction(UpdateActionKind.Cleanup) { Files = files };

        public static UpdateAction Promote(string partName, string fileName) =>
            new UpdateAction(UpdateActionKind.Promote) { PartName = partName, FileName = fileName };

        public static UpdateAction ShowDialog(string version, string fileName) =>
            new UpdateAction(UpdateActionKind.ShowDialog) { Version = version, FileName = fileName };

        public static UpdateAction Apply(string fileName) =>
            new UpdateAction(UpdateActionKind.Apply) { FileName = fileName };
    }
}
