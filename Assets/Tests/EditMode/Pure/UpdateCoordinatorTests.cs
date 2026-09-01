#nullable disable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Полное покрытие конечного автообновления на фейках: ни сети, ни диска, ни
/// процессов — проверяются только решения координатора и то, какие сообщения и
/// окна он просит показать. Тесты ничего на машине не меняют.
/// </summary>
public class UpdateCoordinatorTests
{
    private const string Current = "0.632";
    private const string TempDir = @"Z:\tmp";

    // ── Фейки ─────────────────────────────────────────────────────────────
    private sealed class FakeChecker : IUpdateChecker
    {
        public int Calls;
        public Action<ReleaseManifest> Ok;
        public Action<string> Fail;
        public void Check(Action<ReleaseManifest> onSuccess, Action<string> onFailure)
        { Calls++; Ok = onSuccess; Fail = onFailure; }
    }

    private sealed class FakeDownloader : IInstallerDownloader
    {
        public int StartCalls; public string Url; public string Path;
        public Action<float> Progress; public Action Complete; public Action<string, bool> Fail;
        public int CancelCalls;
        public void Start(string url, string targetPath, Action<float> p, Action c, Action<string, bool> f)
        { StartCalls++; Url = url; Path = targetPath; Progress = p; Complete = c; Fail = f; }
        public void Cancel() { CancelCalls++; }
    }

    private sealed class FakeApplier : IUpdateApplier
    {
        public int Calls; public string Path;
        public void ApplyAndRelaunch(string installerPath) { Calls++; Path = installerPath; }
    }

    private sealed class FakeStatus : IStatusSink
    {
        public readonly List<(string msg, StatusLevel level, float secs)> Shown = new();
        public void Show(string message, StatusLevel level, float seconds)
            => Shown.Add((message, level, seconds));
        public (string, StatusLevel) Last => Shown.Count == 0
            ? (null, StatusLevel.Info) : (Shown[Shown.Count - 1].msg, Shown[Shown.Count - 1].level);
    }

    private sealed class FakeUpdateDialog : IUpdateDialog
    {
        public int ShowCalls; public int HideCalls; public string Version;
        public Action OnUpdate; public Action OnCancel;
        public void ShowUpdateAvailable(string version, Action onUpdate, Action onCancel)
        { ShowCalls++; Version = version; OnUpdate = onUpdate; OnCancel = onCancel; }
        public void Hide() => HideCalls++;
    }

    private sealed class FakeDownloadDialog : IDownloadDialog
    {
        public int ShowCalls; public int HideCalls; public string Version; public Action OnCancel;
        public readonly List<float> Progress = new();
        public void ShowDownloading(string version, Action onCancel)
        { ShowCalls++; Version = version; OnCancel = onCancel; }
        public void SetProgress(float t01) => Progress.Add(t01);
        public void Hide() => HideCalls++;
    }

    // ── Сборка под тест ────────────────────────────────────────────────────
    private FakeChecker _checker; private FakeDownloader _downloader; private FakeApplier _applier;
    private FakeStatus _status; private FakeUpdateDialog _udlg; private FakeDownloadDialog _ddlg;
    private UpdateCoordinator _c;
    private List<string> _log;

    [SetUp]
    public void SetUp()
    {
        _checker = new FakeChecker();
        _downloader = new FakeDownloader();
        _applier = new FakeApplier();
        _status = new FakeStatus();
        _udlg = new FakeUpdateDialog();
        _ddlg = new FakeDownloadDialog();
        _log = new List<string>();
        _c = new UpdateCoordinator(_checker, _downloader, _applier, _status, _udlg, _ddlg,
            Current, () => TempDir, _log.Add);
    }

    private static ReleaseManifest Newer() => new ReleaseManifest
    { Version = "0.700", FileName = "KitchenDesigner-Setup-0.700-x64.exe", DownloadUrl = "https://gh/i.exe" };

    // ── Проверка ──────────────────────────────────────────────────────────
    [Test]
    public void Check_UpToDate_ShowsSuccessStatus_NoDialog()
    {
        _c.CheckForUpdates();
        _checker.Ok(new ReleaseManifest { Version = Current, FileName = "f", DownloadUrl = "u" });

        Assert.AreEqual(1, _status.Shown.Count);
        Assert.AreEqual(UpdateStrings.UpToDate, _status.Shown[0].msg);
        Assert.AreEqual(StatusLevel.Success, _status.Shown[0].level);
        Assert.AreEqual(UpdateStrings.TransientSeconds, _status.Shown[0].secs);
        Assert.AreEqual(0, _udlg.ShowCalls);
        Assert.AreEqual(UpdateCoordinator.State.Idle, _c.CurrentState);
    }

    [Test]
    public void Check_OlderThanCurrent_AlsoTreatedAsUpToDate()
    {
        _c.CheckForUpdates();
        _checker.Ok(new ReleaseManifest { Version = "0.500", FileName = "f", DownloadUrl = "u" });
        Assert.AreEqual(0, _udlg.ShowCalls);
        Assert.AreEqual(UpdateStrings.UpToDate, _status.Last.Item1);
    }

    [Test]
    public void Check_NewerVersion_ShowsDialog_WithVersion_NoStatus()
    {
        _c.CheckForUpdates();
        _checker.Ok(Newer());

        Assert.AreEqual(1, _udlg.ShowCalls);
        Assert.AreEqual("0.700", _udlg.Version);
        Assert.AreEqual(0, _status.Shown.Count, "at a newer version the status bar must stay silent");
        Assert.AreEqual(UpdateCoordinator.State.UpdateAvailable, _c.CurrentState);
    }

    [Test]
    public void Check_Failure_ShowsErrorStatus()
    {
        _c.CheckForUpdates();
        _checker.Fail("HTTP 500");
        Assert.AreEqual(UpdateStrings.CheckError, _status.Last.Item1);
        Assert.AreEqual(StatusLevel.Error, _status.Last.Item2);
        Assert.AreEqual(0, _udlg.ShowCalls);
        Assert.AreEqual(UpdateCoordinator.State.Idle, _c.CurrentState);
    }

    [Test]
    public void CheckWhileChecking_IsIgnored()
    {
        _c.CheckForUpdates();
        _c.CheckForUpdates();
        Assert.AreEqual(1, _checker.Calls);
    }

    // ── Диалог обновления ─────────────────────────────────────────────────
    [Test]
    public void DialogCancel_ClosesDialog_NoDownload()
    {
        _c.CheckForUpdates();
        _checker.Ok(Newer());
        _udlg.OnCancel();

        Assert.AreEqual(1, _udlg.HideCalls);
        Assert.AreEqual(0, _downloader.StartCalls);
        Assert.AreEqual(UpdateCoordinator.State.Idle, _c.CurrentState);
    }

    [Test]
    public void DialogUpdate_HidesUpdateDialog_ShowsDownloadDialog_WithTempTargetPath()
    {
        _c.CheckForUpdates();
        _checker.Ok(Newer());
        _udlg.OnUpdate();

        Assert.AreEqual(1, _udlg.HideCalls);
        Assert.AreEqual(1, _ddlg.ShowCalls);
        Assert.AreEqual("0.700", _ddlg.Version);
        Assert.AreEqual(1, _downloader.StartCalls);
        Assert.AreEqual("https://gh/i.exe", _downloader.Url);
        Assert.AreEqual(@"Z:\tmp\KitchenDesigner-Setup-0.700-x64.exe", _downloader.Path);
        Assert.AreEqual(UpdateCoordinator.State.Downloading, _c.CurrentState);
    }

    // ── Загрузка ──────────────────────────────────────────────────────────
    [Test]
    public void DownloadProgress_ForwardsClampedToDialog()
    {
        _c.CheckForUpdates();
        _checker.Ok(Newer());
        _udlg.OnUpdate();
        _downloader.Progress(0.25f);
        _downloader.Progress(1.5f);   // > 1 -> clamp to 1
        _downloader.Progress(-0.5f);  // < 0 -> clamp to 0

        CollectionAssert.AreEqual(new[] { 0.25f, 1f, 0f }, _ddlg.Progress);
    }

    [Test]
    public void DownloadComplete_HidesDialog_CallsApplierOnce_WithTargetPath()
    {
        _c.CheckForUpdates();
        _checker.Ok(Newer());
        _udlg.OnUpdate();
        _downloader.Complete();

        Assert.AreEqual(1, _ddlg.HideCalls);
        Assert.AreEqual(1, _applier.Calls);
        Assert.AreEqual(_downloader.Path, _applier.Path);
        Assert.AreEqual(UpdateCoordinator.State.Idle, _c.CurrentState);
    }

    [Test]
    public void DownloadError_ShowsErrorStatus_NoApply()
    {
        _c.CheckForUpdates();
        _checker.Ok(Newer());
        _udlg.OnUpdate();
        _downloader.Fail("HTTP 404", false);

        Assert.AreEqual(UpdateStrings.DownloadError, _status.Last.Item1);
        Assert.AreEqual(StatusLevel.Error, _status.Last.Item2);
        Assert.AreEqual(0, _applier.Calls);
        Assert.AreEqual(1, _ddlg.HideCalls);
    }

    [Test]
    public void DialogCancel_DuringDownload_CancelsDownloader_ShowsCancelledStatus_NoApply()
    {
        _c.CheckForUpdates();
        _checker.Ok(Newer());
        _udlg.OnUpdate();
        _ddlg.OnCancel();                       // user clicks "Отмена" in the download window

        Assert.AreEqual(1, _downloader.CancelCalls);
        _downloader.Fail("cancelled", true);    // adapter reports it as cancelled
        Assert.AreEqual(UpdateStrings.DownloadCancelled, _status.Last.Item1);
        Assert.AreEqual(StatusLevel.Info, _status.Last.Item2);
        Assert.AreEqual(0, _applier.Calls);
    }

    [Test]
    public void StatusTransientSeconds_IsThreeOrMore()
    {
        _c.CheckForUpdates();
        _checker.Ok(new ReleaseManifest { Version = Current, FileName = "f", DownloadUrl = "u" });
        Assert.GreaterOrEqual(_status.Shown[0].secs, 3f);
    }

    [Test]
    public void CheckFailure_PassesTheReasonToTheLog()
    {
        _c.CheckForUpdates();
        _checker.Fail("HTTP 500");
        CollectionAssert.IsNotEmpty(_log,
            "причина отказа видна пользователю только в логе: статус-бар показывает общую фразу");
        StringAssert.Contains("HTTP 500", _log[_log.Count - 1]);
    }

    [Test]
    public void DownloadFailure_PassesTheReasonToTheLog_ButCancelIsNotAFailure()
    {
        _c.CheckForUpdates();
        _checker.Ok(Newer());
        _udlg.OnUpdate();
        _downloader.Fail("disk full", false);
        StringAssert.Contains("disk full", _log[_log.Count - 1]);

        _log.Clear();
        _c.CheckForUpdates();
        _checker.Ok(Newer());
        _udlg.OnUpdate();
        _downloader.Fail("cancelled", true);
        CollectionAssert.IsEmpty(_log, "отмена пользователем — не отказ, в лог она не пишется");
    }

    [Test]
    public void CancelClick_OnlyAsksTheDownloader_AndWaitsForItToReportBack()
    {
        _c.CheckForUpdates();
        _checker.Ok(Newer());
        _udlg.OnUpdate();
        int hidesBeforeCancel = _ddlg.HideCalls;

        _ddlg.OnCancel();

        Assert.AreEqual(1, _downloader.CancelCalls);
        Assert.AreEqual(hidesBeforeCancel, _ddlg.HideCalls,
            "окно загрузки закрывает ТОЛЬКО ответ загрузчика: Cancel() обязан прийти "
            + "обратно как onFailure(_, cancelled: true), и если закрыть окно раньше, "
            + "пришедший следом ответ закроет уже чужое окно");
        CollectionAssert.IsEmpty(_status.Shown,
            "до ответа загрузчика сказать пользователю нечего: неизвестно, успела "
            + "загрузка завершиться или нет");

        _downloader.Fail("cancelled", true);
        Assert.AreEqual(UpdateStrings.DownloadCancelled, _status.Last.Item1);
        Assert.AreEqual(hidesBeforeCancel + 1, _ddlg.HideCalls);
    }

    [Test]
    public void Check_ManifestWithUnreadableVersion_ReportsAFailure_NotASilentUpToDate()
    {
        _c.CheckForUpdates();
        _checker.Ok(new ReleaseManifest
        { Version = "сломанный ответ", FileName = "f", DownloadUrl = "u" });

        Assert.AreEqual(0, _udlg.ShowCalls,
            "версию не удалось прочитать — предлагать обновление не на чем");
        Assert.AreEqual(UpdateStrings.CheckError, _status.Last.Item1,
            "отказ, замаскированный под успех, хуже отказа: увидев зелёное "
            + "«обновлений нет», пользователь сделает единственный разумный вывод — "
            + "что версия свежая. Он не перепроверит, не заглянет в лог и не напишет "
            + "нам, и сигнал о сломанном канале обновлений пропадёт молча и навсегда");
        Assert.AreEqual(StatusLevel.Error, _status.Last.Item2);
        Assert.AreEqual(UpdateCoordinator.State.Idle, _c.CurrentState);
    }

    [Test]
    public void Check_ManifestWithoutADownloadUrl_ReportsAFailure()
    {
        _c.CheckForUpdates();
        _checker.Ok(new ReleaseManifest { Version = "0.700", FileName = "setup.exe", DownloadUrl = "" });

        Assert.AreEqual(UpdateStrings.CheckError, _status.Last.Item1,
            "релиз без ссылки скачать нельзя: диалог «доступно обновление» привёл бы "
            + "к загрузке с пустого адреса, то есть к отказу уже ПОСЛЕ согласия "
            + "пользователя обновиться");
        Assert.AreEqual(0, _udlg.ShowCalls);
    }

    [Test]
    public void Check_ManifestWithoutAFileName_ReportsAFailure()
    {
        _c.CheckForUpdates();
        _checker.Ok(new ReleaseManifest { Version = "0.700", FileName = "   ", DownloadUrl = "https://gh/i.exe" });

        Assert.AreEqual(UpdateStrings.CheckError, _status.Last.Item1,
            "имя файла уходит в Path.Combine как имя установщика: без него путь "
            + "указывает на саму временную ПАПКУ, и установщик «скачивается» поверх неё");
        Assert.AreEqual(0, _udlg.ShowCalls);
    }

    [Test]
    public void Check_ManifestThatIsMissingAltogether_ReportsAFailure_AndSaysWhyInTheLog()
    {
        _c.CheckForUpdates();
        _checker.Ok(null!);

        Assert.AreEqual(UpdateStrings.CheckError, _status.Last.Item1);
        CollectionAssert.IsNotEmpty(_log,
            "статус-бар показывает общую фразу, поэтому единственное место, где "
            + "видно ПРИЧИНУ, — лог: без записи отказ невозможно разобрать по факту");
    }

    [Test]
    public void Check_BrokenManifest_NamesTheOffendingFieldsInTheLog()
    {
        _c.CheckForUpdates();
        _checker.Ok(new ReleaseManifest
        { Version = "сломанный ответ", FileName = "f", DownloadUrl = "u" });

        StringAssert.Contains("сломанный ответ", _log[_log.Count - 1],
            "в лог уходит то, что реально пришло от сервера: иначе разбор отказа "
            + "начинается с догадок о том, что было в ответе");
    }
}
