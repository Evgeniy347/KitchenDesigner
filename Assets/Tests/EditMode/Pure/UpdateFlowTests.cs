#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Весь поток обновления от запуска приложения до запуска установщика: настоящий планировщик,
/// настоящая папка и настоящее хеширование на диске; подставные — только сеть (релиз и
/// загрузка), окно, запуск установщика и консоль. Диалог не показывается ни при одном сбое:
/// сбои пишутся в консоль и приложение живёт дальше.
/// </summary>
public class UpdateFlowTests
{
    private const string Current = "0.2000";
    private const string Latest = "0.2100";
    private static readonly string FinalName = InstallerFileName.For(Latest);
    private static readonly string PartName = InstallerFileName.PartFor(Latest);

    private sealed class FakeReleases : IReleaseSource
    {
        public ReleaseLookup Answer;
        public int Fetches;

        public void Fetch(Action<ReleaseLookup> done)
        {
            Fetches++;
            done(Answer);
        }
    }

    private sealed class SyncInspector : IFileInspector
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

    private sealed class ScriptedDownloader : IPartDownloader
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

    private sealed class FakeConsole : IUpdateConsole
    {
        public readonly List<(UpdateLogLevel level, string text)> Lines = new List<(UpdateLogLevel, string)>();

        public void Write(UpdateLogLevel level, string text) => Lines.Add((level, text));

        public IEnumerable<string> Texts => Lines.Select(l => l.text);
    }

    private sealed class FakeDialog : IUpdateDialog
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

    private sealed class FakeApplier : IUpdateApplier
    {
        public readonly List<string> Applied = new List<string>();

        public void ApplyAndRelaunch(string installerPath) => Applied.Add(installerPath);
    }

    private sealed class Harness
    {
        public TempUpdateFolder Temp;
        public IUpdateFolder Folder;
        public FakeReleases Releases = new FakeReleases();
        public SyncInspector Inspector;
        public ScriptedDownloader Downloader;
        public FakeConsole Console = new FakeConsole();
        public FakeDialog Dialog = new FakeDialog();
        public FakeApplier Applier = new FakeApplier();
        public float Clock;
        public UpdatePlanner Planner;
        public UpdateFlow Flow;

        public Harness(IUpdateFolder folder = null, TempUpdateFolder temp = null)
        {
            Temp = temp ?? new TempUpdateFolder();
            Folder = folder ?? Temp.Folder;
            Inspector = new SyncInspector(Folder);
            Downloader = new ScriptedDownloader(Folder);
            Planner = new UpdatePlanner(Current);
            Flow = new UpdateFlow(Planner, Releases, Folder, Inspector, Downloader, Console, Dialog, Applier, () => Clock);
        }

        public void Run(ReleaseLookup answer)
        {
            Releases.Answer = answer;
            Flow.Start();
        }
    }

    private TempUpdateFolder _temp;

    [SetUp]
    public void SetUp() => _temp = new TempUpdateFolder();

    [TearDown]
    public void TearDown() => _temp.Dispose();

    private static ReleaseLookup Release(byte[] installer, bool withDigest = true, bool withSize = true) =>
        ReleaseLookup.Found(new ReleaseManifest
        {
            Version = Latest,
            FileName = FinalName,
            DownloadUrl = "https://example.invalid/" + FinalName,
            Sha256 = withDigest ? Payload.Sha256(installer) : string.Empty,
            Size = withSize ? installer.Length : 0,
        });

    private Harness NewHarness() => new Harness(temp: _temp);

    [Test]
    public void FirstRun_AnnouncesTheVersion_Downloads_Verifies_Promotes_AndOffersTheDialog()
    {
        var installer = Payload.Bytes(200_000, 1);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);

        h.Run(Release(installer));

        Assert.AreEqual("обнаружена новая версия " + Latest, h.Console.Texts.First());
        Assert.AreEqual(1, h.Downloader.Requests.Count);
        Assert.AreEqual(PartName, h.Downloader.Requests[0].PartName);
        Assert.AreEqual("https://example.invalid/" + FinalName, h.Downloader.Requests[0].Url);
        CollectionAssert.AreEqual(new[] { FinalName }, _temp.Names(), "после проверки остаётся только готовый файл");
        CollectionAssert.AreEqual(installer, File.ReadAllBytes(_temp.PathOf(FinalName)));
        Assert.AreEqual(1, h.Dialog.Shown);
        Assert.AreEqual(Latest, h.Dialog.Version);
        Assert.IsEmpty(h.Applier.Applied, "пока пользователь не согласился, установщик не запускается");
        Assert.AreEqual(UpdatePhase.AwaitingUser, h.Flow.Phase);
    }

    [Test]
    public void FirstRun_TheDialogComesOnlyAfterTheFileIsComplete()
    {
        var installer = Payload.Bytes(50_000, 2);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);
        int shownWhileDownloading = -1;
        bool finalExistedWhileDownloading = true;
        h.Downloader.Script = observer =>
        {
            shownWhileDownloading = h.Dialog.Shown;
            finalExistedWhileDownloading = _temp.Has(FinalName);
        };

        h.Run(Release(installer));

        Assert.AreEqual(0, shownWhileDownloading, "окно не показывается, пока файл качается");
        Assert.IsFalse(finalExistedWhileDownloading, "готового имени нет, пока файл не проверен");
        Assert.AreEqual(1, h.Dialog.Shown);
    }

    [Test]
    public void Accepting_RunsTheInstallerFromTheUpdatesFolder_AfterAFreshVerification()
    {
        var installer = Payload.Bytes(20_000, 3);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);
        h.Run(Release(installer));
        int verificationsBefore = h.Inspector.Calls.Count;

        h.Dialog.Accept();

        Assert.AreEqual(1, h.Dialog.Hidden);
        Assert.Greater(h.Inspector.Calls.Count, verificationsBefore, "перед запуском файл проверяется ещё раз");
        Assert.AreEqual(FinalName, h.Inspector.Calls.Last().name);
        CollectionAssert.AreEqual(new[] { _temp.PathOf(FinalName) }, h.Applier.Applied);
        Assert.AreEqual(UpdatePhase.Finished, h.Flow.Phase);
    }

    [Test]
    public void Declining_KeepsTheFile_AndRunsNothing()
    {
        var installer = Payload.Bytes(20_000, 4);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);
        h.Run(Release(installer));

        h.Dialog.Decline();

        Assert.IsEmpty(h.Applier.Applied);
        Assert.IsTrue(_temp.Has(FinalName), "отказ не удаляет скачанное: файл пригодится при следующем запуске");
        Assert.AreEqual(1, h.Dialog.Hidden);
    }

    [Test]
    public void AFileTamperedWithAfterTheDialog_IsNotRun_AndIsRemoved()
    {
        var installer = Payload.Bytes(20_000, 5);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);
        h.Run(Release(installer));
        File.WriteAllBytes(_temp.PathOf(FinalName), Payload.Corrupted(installer));

        h.Dialog.Accept();

        Assert.IsEmpty(h.Applier.Applied);
        Assert.IsFalse(_temp.Has(FinalName));
        Assert.IsTrue(h.Console.Lines.Any(l => l.level == UpdateLogLevel.Error));
    }

    [Test]
    public void SecondStart_WithTheFileAlreadyDownloadedAndIntact_SkipsTheDownload_AndShowsTheDialog()
    {
        var installer = Payload.Bytes(30_000, 6);
        _temp.Write(FinalName, installer);
        var h = NewHarness();

        h.Run(Release(installer));

        Assert.IsEmpty(h.Downloader.Requests);
        Assert.AreEqual(1, h.Dialog.Shown);
        Assert.AreEqual(Latest, h.Dialog.Version);
        CollectionAssert.AreEqual(new[] { FinalName }, _temp.Names());
        StringAssert.Contains("загрузка не нужна", string.Join("\n", h.Console.Texts));
    }

    [Test]
    public void SecondStart_WithACorruptedFile_DeletesItAndDownloadsAgain()
    {
        var installer = Payload.Bytes(30_000, 7);
        _temp.Write(FinalName, Payload.Corrupted(installer));
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);

        h.Run(Release(installer));

        Assert.AreEqual(1, h.Downloader.Requests.Count);
        CollectionAssert.AreEqual(installer, File.ReadAllBytes(_temp.PathOf(FinalName)));
        Assert.AreEqual(1, h.Dialog.Shown);
    }

    [Test]
    public void SecondStart_WithATruncatedFile_ChecksBySizeAndDownloadsAgain()
    {
        var installer = Payload.Bytes(30_000, 8);
        _temp.Write(FinalName, installer.Take(10_000).ToArray());
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);

        h.Run(Release(installer, withDigest: false));

        Assert.AreEqual(1, h.Downloader.Requests.Count);
        CollectionAssert.AreEqual(installer, File.ReadAllBytes(_temp.PathOf(FinalName)));
    }

    [Test]
    public void SecondStart_WithAStalePartFile_DeletesItAndDownloadsFromScratch()
    {
        var installer = Payload.Bytes(30_000, 9);
        _temp.Write(PartName, installer.Take(12_000).ToArray());
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);

        h.Run(Release(installer));

        Assert.AreEqual(1, h.Downloader.Requests.Count);
        CollectionAssert.AreEqual(new[] { FinalName }, _temp.Names());
        CollectionAssert.AreEqual(installer, File.ReadAllBytes(_temp.PathOf(FinalName)));
    }

    [Test]
    public void ACorruptedDownload_IsDownloadedOnceMore_AndTheSecondGoodCopyIsOffered()
    {
        var installer = Payload.Bytes(30_000, 10);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(Payload.Corrupted(installer));
        h.Downloader.Payloads.Enqueue(installer);

        h.Run(Release(installer));

        Assert.AreEqual(2, h.Downloader.Requests.Count);
        Assert.AreEqual(1, h.Dialog.Shown);
        CollectionAssert.AreEqual(installer, File.ReadAllBytes(_temp.PathOf(FinalName)));
    }

    [Test]
    public void TwoCorruptedDownloads_GiveUp_QuietlyInTheConsole_NoDialog_NoFilesLeft()
    {
        var installer = Payload.Bytes(30_000, 11);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(Payload.Corrupted(installer));
        h.Downloader.Payloads.Enqueue(Payload.Corrupted(installer));

        h.Run(Release(installer));

        Assert.AreEqual(2, h.Downloader.Requests.Count);
        Assert.AreEqual(0, h.Dialog.Shown, "ошибка не показывается окном");
        Assert.IsEmpty(_temp.Names());
        Assert.IsEmpty(h.Applier.Applied);
        StringAssert.Contains("дважды", h.Console.Lines.Last().text);
        Assert.AreEqual(UpdateLogLevel.Error, h.Console.Lines.Last().level);
        Assert.AreEqual(UpdatePhase.Finished, h.Flow.Phase);
    }

    [Test]
    public void NextStartAfterTwoCorruptedDownloads_TriesAgainWithAFullSetOfAttempts()
    {
        var installer = Payload.Bytes(30_000, 12);
        var first = NewHarness();
        first.Downloader.Payloads.Enqueue(Payload.Corrupted(installer));
        first.Downloader.Payloads.Enqueue(Payload.Corrupted(installer));
        first.Run(Release(installer));

        var second = NewHarness();
        second.Downloader.Payloads.Enqueue(installer);
        second.Run(Release(installer));

        Assert.AreEqual(1, second.Dialog.Shown);
    }

    [Test]
    public void ADownloadFailure_GoesToTheConsole_LeavesNoPart_AndShowsNoDialog()
    {
        var installer = Payload.Bytes(1000, 13);
        var h = NewHarness();
        h.Downloader.FailWith = "сервер ответил HTTP 404";
        h.Downloader.Script = observer => _temp.Write(PartName, new byte[10]);

        h.Run(Release(installer));

        Assert.AreEqual(0, h.Dialog.Shown);
        Assert.IsEmpty(_temp.Names());
        var last = h.Console.Lines.Last();
        Assert.AreEqual(UpdateLogLevel.Error, last.level);
        StringAssert.Contains("404", last.text);
        StringAssert.Contains("при следующем запуске", last.text);
        Assert.AreEqual(UpdatePhase.Finished, h.Flow.Phase);
    }

    [Test]
    public void ALookupFailure_GoesToTheConsole_TouchesNoFile_AndShowsNoDialog()
    {
        _temp.Write(InstallerFileName.For("0.1000"), new byte[10]);
        var h = NewHarness();

        h.Run(ReleaseLookup.Failed("HTTP 403"));

        Assert.AreEqual(0, h.Dialog.Shown);
        Assert.IsEmpty(h.Downloader.Requests);
        CollectionAssert.AreEqual(new[] { InstallerFileName.For("0.1000") }, _temp.Names());
        StringAssert.Contains("403", h.Console.Lines.Single().text);
    }

    [Test]
    public void UpToDate_CleansOldInstallersAndParts_ButNothingElse_AndNothingOutsideTheFolder()
    {
        _temp.Write(InstallerFileName.For("0.1000"), new byte[10]);
        _temp.Write(InstallerFileName.For(Current), new byte[10]);
        _temp.Write(InstallerFileName.PartFor("0.1500"), new byte[10]);
        _temp.Write(InstallerFileName.For("0.2500"), new byte[10]);
        _temp.Write("notes.txt", new byte[10]);
        _temp.Write("KitchenDesigner-Setup-0.1000-x64.exe.bak", new byte[10]);
        _temp.Write("KitchenDesigner-Setup-0.1000-x64.log", new byte[10]);
        _temp.WriteOutside(InstallerFileName.For("0.1000"), new byte[10]);
        var h = NewHarness();
        var newest = Payload.Bytes(10);

        h.Run(ReleaseLookup.Found(new ReleaseManifest
        {
            Version = Current, FileName = InstallerFileName.For(Current), DownloadUrl = "u", Sha256 = Payload.Sha256(newest),
        }));

        CollectionAssert.AreEqual(new[]
        {
            "KitchenDesigner-Setup-0.1000-x64.exe.bak",
            "KitchenDesigner-Setup-0.1000-x64.log",
            InstallerFileName.For("0.2500"),
            "notes.txt",
        }.OrderBy(n => n, StringComparer.Ordinal), _temp.Names());
        Assert.IsTrue(File.Exists(_temp.OutsidePathOf(InstallerFileName.For("0.1000"))), "вне папки обновлений не трогается ничто");
        Assert.AreEqual(0, h.Dialog.Shown);
        Assert.IsEmpty(h.Downloader.Requests);
        StringAssert.Contains("актуальная версия", string.Join("\n", h.Console.Texts));
    }

    [Test]
    public void UpToDate_WhenTheFolderDoesNotExist_JustLogs()
    {
        var h = NewHarness();

        h.Run(ReleaseLookup.NoInstallerAsset(Current, "нет"));

        Assert.AreEqual(1, h.Console.Lines.Count);
        Assert.IsFalse(Directory.Exists(_temp.Root), "чистка не создаёт папку ради того, чтобы её почистить");
    }

    [Test]
    public void ReleaseWithNeitherDigestNorSize_IsNeverDownloaded_NeverRun()
    {
        var installer = Payload.Bytes(1000, 14);
        var h = NewHarness();

        h.Run(Release(installer, withDigest: false, withSize: false));

        Assert.IsEmpty(h.Downloader.Requests);
        Assert.AreEqual(0, h.Dialog.Shown);
        Assert.IsEmpty(h.Applier.Applied);
        StringAssert.Contains("не будет загружен и запущен", h.Console.Lines.Last().text);
    }

    [Test]
    public void SizeOnlyRelease_IsVerifiedBySize_AndTheConsoleSaysSo()
    {
        var installer = Payload.Bytes(8000, 15);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);

        h.Run(Release(installer, withDigest: false));

        Assert.AreEqual(1, h.Dialog.Shown);
        Assert.IsTrue(h.Inspector.Calls.All(c => !c.hash), "без SHA-256 хеш не считается");
        Assert.IsTrue(h.Console.Lines.Any(l => l.level == UpdateLogLevel.Warning && l.text.Contains("только по размеру")));
    }

    [Test]
    public void SizeOnlyRelease_CannotTellASameSizedSubstitution_TheCostOfNoDigest()
    {
        var installer = Payload.Bytes(8000, 16);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(Payload.Corrupted(installer));

        h.Run(Release(installer, withDigest: false));

        Assert.AreEqual(1, h.Dialog.Shown,
            "проверка по размеру ловит только обрезанный файл; подмену байтов при том же размере — нет: это цена отсутствия SHA-256");
    }

    [Test]
    public void Progress_GoesToTheConsole_ThrottledByStepAndByTime()
    {
        var installer = Payload.Bytes(1000, 17);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);
        h.Downloader.Script = observer =>
        {
            foreach (var received in new long[] { 0, 90, 99, 100, 150, 400, 500, 600, 1000 })
            {
                h.Clock += 1f;
                observer.OnProgress(received, 1000);
            }
        };

        h.Run(Release(installer));

        var progress = h.Console.Texts.Where(t => t.StartsWith("загрузка: ")).ToList();
        Assert.AreEqual(4, progress.Count, string.Join(" | ", progress));
        StringAssert.StartsWith("загрузка: 10%", progress[0]);
        StringAssert.StartsWith("загрузка: 40%", progress[1]);
        StringAssert.StartsWith("загрузка: 60%", progress[2]);
        StringAssert.StartsWith("загрузка: 100%", progress[3]);
    }

    [Test]
    public void Progress_UsesTheExpectedSize_WhenTheServerDeclaresNoLength()
    {
        var installer = Payload.Bytes(2000, 18);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);
        h.Downloader.Script = observer =>
        {
            h.Clock = 10f;
            observer.OnProgress(1000, 0);
        };

        h.Run(Release(installer));

        Assert.IsTrue(h.Console.Texts.Any(t => t.StartsWith("загрузка: 50%")));
    }

    [Test]
    public void Retries_AreForwardedToTheConsole()
    {
        var installer = Payload.Bytes(1000, 19);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);
        h.Downloader.Script = observer => observer.OnRetry(2, 3, "нет соединения");

        h.Run(Release(installer));

        Assert.IsTrue(h.Console.Lines.Any(l => l.level == UpdateLogLevel.Warning && l.text.Contains("попытка 2 из 3")));
    }

    private sealed class ThrowingListing : IUpdateFolder
    {
        public string PathOf(string name) => name;
        public IReadOnlyList<FolderEntry> List() => throw new IOException("доступ запрещён");
        public void EnsureExists() { }
        public bool TryDelete(string name, out string error) { error = ""; return true; }
        public bool TryPromote(string partName, string finalName, out string error) { error = ""; return true; }
    }

    [Test]
    public void AnUnreadableFolder_IsAConsoleError_NotAnException_AndNothingIsDownloaded()
    {
        var installer = Payload.Bytes(1000, 20);
        var h = new Harness(new ThrowingListing(), _temp);

        h.Run(Release(installer));

        Assert.IsEmpty(h.Downloader.Requests);
        Assert.AreEqual(0, h.Dialog.Shown);
        StringAssert.Contains("доступ запрещён", h.Console.Lines.Last().text);
    }

    [Test]
    public void ADiskThatCannotCreateTheFolder_IsAConsoleError_AndNoDialog()
    {
        var installer = Payload.Bytes(1000, 21);
        File.WriteAllText(_temp.Root, "this is a file where the folder should be");
        var h = new Harness(new FileSystemUpdateFolderProbe(_temp.Root), _temp);

        h.Run(Release(installer));

        Assert.IsEmpty(h.Downloader.Requests);
        Assert.AreEqual(0, h.Dialog.Shown);
        Assert.AreEqual(UpdateLogLevel.Error, h.Console.Lines.Last().level);
        Assert.AreEqual(UpdatePhase.Finished, h.Flow.Phase);
    }

    private sealed class FileSystemUpdateFolderProbe : IUpdateFolder
    {
        private readonly FileSystemUpdateFolder _inner;

        public FileSystemUpdateFolderProbe(string root) => _inner = new FileSystemUpdateFolder(root);

        public string PathOf(string name) => _inner.PathOf(name);
        public IReadOnlyList<FolderEntry> List() => new List<FolderEntry>();
        public void EnsureExists() => _inner.EnsureExists();
        public bool TryDelete(string name, out string error) => _inner.TryDelete(name, out error);
        public bool TryPromote(string partName, string finalName, out string error) =>
            _inner.TryPromote(partName, finalName, out error);
    }

    [Test]
    public void AnExceptionFromTheDialog_IsCaught_AndWrittenToTheConsole()
    {
        var installer = Payload.Bytes(1000, 22);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);
        var throwing = new ThrowingDialog();
        var flow = new UpdateFlow(h.Planner, h.Releases, h.Folder, h.Inspector, h.Downloader, h.Console, throwing,
            h.Applier, () => 0f);
        h.Releases.Answer = Release(installer);

        Assert.DoesNotThrow(() => flow.Start());

        StringAssert.Contains("диалог сломан", h.Console.Lines.Last().text);
        Assert.AreEqual(UpdateLogLevel.Error, h.Console.Lines.Last().level);
    }

    private sealed class ThrowingDialog : IUpdateDialog
    {
        public void ShowUpdateAvailable(string version, Action onUpdate, Action onCancel) =>
            throw new InvalidOperationException("диалог сломан");

        public void Hide() { }
    }

    [Test]
    public void ADoubleClickOnAccept_RunsTheInstallerOnce()
    {
        var installer = Payload.Bytes(1000, 23);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);
        h.Run(Release(installer));

        h.Dialog.Accept();
        h.Dialog.Accept();

        Assert.AreEqual(1, h.Applier.Applied.Count);
    }

    [Test]
    public void ACleanupThatCannotDeleteALockedFile_WarnsInTheConsole_AndTheFlowContinues()
    {
        var installer = Payload.Bytes(1000, 24);
        var old = InstallerFileName.For("0.1000");
        _temp.Write(old, new byte[10]);
        var h = NewHarness();
        h.Downloader.Payloads.Enqueue(installer);

        using (new FileStream(_temp.PathOf(old), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            h.Run(Release(installer));
        }

        Assert.IsTrue(h.Console.Lines.Any(l => l.level == UpdateLogLevel.Warning && l.text.Contains(old)));
        Assert.AreEqual(1, h.Dialog.Shown);
    }

    [Test]
    public void TheConstructor_RejectsMissingCollaborators()
    {
        var h = NewHarness();

        Assert.Throws<ArgumentNullException>(() => new UpdateFlow(null, h.Releases, h.Folder, h.Inspector,
            h.Downloader, h.Console, h.Dialog, h.Applier, () => 0f));
        Assert.Throws<ArgumentNullException>(() => new UpdateFlow(h.Planner, h.Releases, h.Folder, h.Inspector,
            h.Downloader, h.Console, null, h.Applier, () => 0f));
        Assert.Throws<ArgumentNullException>(() => new UpdateFlow(h.Planner, h.Releases, h.Folder, h.Inspector,
            h.Downloader, h.Console, h.Dialog, h.Applier, null));
    }
}
