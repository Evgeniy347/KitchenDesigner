using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Update
{
    public interface IReleaseSource
    {
        void Fetch(Action<ReleaseLookup> done);
    }

    public interface IUpdateFolder
    {
        string PathOf(string name);
        IReadOnlyList<FolderEntry> List();
        void EnsureExists();
        bool TryDelete(string name, out string error);
        bool TryPromote(string partName, string finalName, out string error);
    }

    public interface IFileInspector
    {
        void Inspect(string fileName, bool computeSha256, Action<FileFacts> done);
    }

    public sealed class DownloadRequest
    {
        public DownloadRequest(string url, string partName, long expectedSize)
        {
            Url = url;
            PartName = partName;
            ExpectedSize = expectedSize;
        }

        public string Url { get; }
        public string PartName { get; }
        public long ExpectedSize { get; }
    }

    public interface IDownloadObserver
    {
        void OnProgress(long received, long total);
        void OnRetry(int attempt, int attempts, string reason);
        void OnFinished(DownloadOutcome outcome);
    }

    public interface IPartDownloader
    {
        void Download(DownloadRequest request, IDownloadObserver observer);
    }

    public interface IUpdateConsole
    {
        void Write(UpdateLogLevel level, string text);
    }

    public interface IMainThread
    {
        void Post(Action action);
    }
}
