#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using KitchenDesigner.Core.Update;
using KitchenDesigner.Tests.Geometry;

/// <summary>
/// Общие заготовки тестов автообновления. Временная папка лежит внутри репозитория
/// (test-results/ в .gitignore), а не в системной Temp: правило agents/GIT.md «Temp files»,
/// и мусор после упавшего прогона виден там, где его ищут.
/// </summary>
public sealed class TempUpdateFolder : IDisposable
{
    public TempUpdateFolder()
    {
        var results = RepoPaths.Subdir("test-results");
        Parent = Path.Combine(results, "update-tests", Guid.NewGuid().ToString("N"));
        Root = Path.Combine(Parent, "Updates");
        Directory.CreateDirectory(Parent);
        Folder = new FileSystemUpdateFolder(Root);
    }

    public string Parent { get; }
    public string Root { get; }
    public FileSystemUpdateFolder Folder { get; }

    public string PathOf(string name) => Path.Combine(Root, name);

    public string OutsidePathOf(string name) => Path.Combine(Parent, name);

    public void Write(string name, byte[] bytes)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllBytes(PathOf(name), bytes);
    }

    public void WriteOutside(string name, byte[] bytes) => File.WriteAllBytes(OutsidePathOf(name), bytes);

    public bool Has(string name) => File.Exists(PathOf(name));

    public IReadOnlyList<string> Names() =>
        Directory.Exists(Root)
            ? new DirectoryInfo(Root).GetFileSystemInfos().Select(i => i.Name).OrderBy(n => n, StringComparer.Ordinal).ToList()
            : new List<string>();

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Parent)) Directory.Delete(Parent, true);
        }
        catch (IOException)
        {
        }
    }
}

public static class Payload
{
    public static byte[] Bytes(int length, int seed = 1)
    {
        var bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    public static string Sha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Sha256Digest.ToHex(sha.ComputeHash(bytes));
    }

    public static byte[] Corrupted(byte[] bytes)
    {
        var copy = (byte[])bytes.Clone();
        copy[copy.Length / 2] ^= 0xFF;
        return copy;
    }
}

public sealed class InlineMainThread : IMainThread
{
    public void Post(Action action) => action();
}

public sealed class RecordingObserver : IDownloadObserver
{
    private readonly object _gate = new object();
    private readonly List<(long received, long total)> _progress = new List<(long, long)>();
    private readonly List<(int attempt, int attempts, string reason)> _retries = new List<(int, int, string)>();

    public ManualResetEventSlim Done { get; } = new ManualResetEventSlim(false);
    public DownloadOutcome Outcome { get; private set; }
    public int Finished { get; private set; }

    public IReadOnlyList<(long received, long total)> Progress
    {
        get { lock (_gate) return _progress.ToList(); }
    }

    public IReadOnlyList<(int attempt, int attempts, string reason)> Retries
    {
        get { lock (_gate) return _retries.ToList(); }
    }

    public void OnProgress(long received, long total)
    {
        lock (_gate) _progress.Add((received, total));
    }

    public void OnRetry(int attempt, int attempts, string reason)
    {
        lock (_gate) _retries.Add((attempt, attempts, reason));
    }

    public void OnFinished(DownloadOutcome outcome)
    {
        lock (_gate)
        {
            Outcome = outcome;
            Finished++;
        }
        Done.Set();
    }

    public DownloadOutcome Await(int seconds = 15)
    {
        if (!Done.Wait(TimeSpan.FromSeconds(seconds)))
            throw new TimeoutException("загрузка не закончилась за " + seconds + " с");
        return Outcome;
    }
}

public sealed class FakeReleaseSource : IReleaseSource
{
    public ReleaseLookup Answer;
    public int Fetches;

    public void Fetch(Action<ReleaseLookup> done)
    {
        Fetches++;
        done(Answer);
    }
}

public sealed class SyncInspector : IFileInspector
{
    private readonly IUpdateFolder _folder;
    public readonly List<(string name, bool hash)> Calls = new List<(string, bool)>();
    public Action<string> BeforeAnswer;

    public SyncInspector(IUpdateFolder folder) => _folder = folder;

    public void Inspect(string fileName, bool computeSha256, Action<FileFacts> done)
    {
        Calls.Add((fileName, computeSha256));
        BeforeAnswer?.Invoke(fileName);
        done(Sha256FileInspector.Examine(_folder.PathOf(fileName), computeSha256));
    }
}

public sealed class ScriptedDownloader : IPartDownloader
{
    private readonly IUpdateFolder _folder;
    public readonly List<DownloadRequest> Requests = new List<DownloadRequest>();
    public readonly Queue<byte[]> Payloads = new Queue<byte[]>();
    public string FailWith;
    public Action<IDownloadObserver> Script;

    public ScriptedDownloader(IUpdateFolder folder) => _folder = folder;

    public void Download(DownloadRequest request, IDownloadObserver observer)
    {
        Requests.Add(request);
        Script?.Invoke(observer);
        if (FailWith != null)
        {
            observer.OnFinished(DownloadOutcome.Failure(FailWith));
            return;
        }
        var bytes = Payloads.Dequeue();
        File.WriteAllBytes(_folder.PathOf(request.PartName), bytes);
        observer.OnProgress(bytes.Length, bytes.Length);
        observer.OnFinished(DownloadOutcome.Success());
    }
}

public sealed class RecordingConsole : IUpdateConsole
{
    public readonly List<(UpdateLogLevel level, string text)> Lines = new List<(UpdateLogLevel, string)>();

    public void Write(UpdateLogLevel level, string text) => Lines.Add((level, text));

    public IEnumerable<string> Texts => Lines.Select(l => l.text);
}

public sealed class RecordingDialog : IUpdateDialog
{
    public int Shown;
    public int Hidden;
    public string Version;
    public Action Accept;
    public Action Decline;

    public void ShowUpdateAvailable(string version, Action onUpdate, Action onCancel)
    {
        Shown++;
        Version = version;
        Accept = onUpdate;
        Decline = onCancel;
    }

    public void Hide() => Hidden++;
}

public sealed class RecordingApplier : IUpdateApplier
{
    public readonly List<string> Applied = new List<string>();

    public void ApplyAndRelaunch(string installerPath) => Applied.Add(installerPath);
}

