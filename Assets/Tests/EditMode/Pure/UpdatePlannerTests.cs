#nullable disable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Решения автообновления целиком, без сети, диска и окон: планировщик получает факты
/// (релиз, список папки, результат хеширования) и отвечает списком действий. Здесь
/// проверена каждая ветка: версия новее/та же/старее, нет релиза, нет ассета, чужое имя,
/// есть/нет/битый скачанный файл, недокачанный .part, SHA-256 или только размер или ничего,
/// повторная загрузка после несовпадения и отказ после второго, отказ пользователя.
/// </summary>
public class UpdatePlannerTests
{
    private const string Current = "0.2000";
    private const string Latest = "0.2100";
    private static readonly string Sha = new string('a', 64);
    private static readonly string OtherSha = new string('b', 64);
    private static readonly string FinalName = InstallerFileName.For(Latest);
    private static readonly string PartName = InstallerFileName.PartFor(Latest);

    private static ReleaseManifest Manifest(
        string version = Latest, string sha = null, long size = 1000, string file = null, string url = "https://gh/i.exe") =>
        new ReleaseManifest
        {
            Version = version,
            FileName = file ?? InstallerFileName.For(version),
            DownloadUrl = url,
            Sha256 = sha ?? Sha,
            Size = size,
        };

    private static ReleaseLookup Found(ReleaseManifest manifest = null) => ReleaseLookup.Found(manifest ?? Manifest());

    private static IReadOnlyList<FolderEntry> Folder(params string[] names) =>
        names.Select(n => new FolderEntry(n, 1000)).ToList();

    private static UpdatePlanner Started(string current = Current)
    {
        var planner = new UpdatePlanner(current);
        planner.Begin();
        return planner;
    }

    private static UpdatePlanner Planned(ReleaseLookup lookup, IReadOnlyList<FolderEntry> folder,
        out IReadOnlyList<UpdateAction> actions, string current = Current)
    {
        var planner = Started(current);
        actions = planner.OnReleaseResolved(lookup, folder);
        return planner;
    }

    private static List<UpdateActionKind> Kinds(IEnumerable<UpdateAction> actions) =>
        actions.Select(a => a.Kind).ToList();

    private static UpdateAction Single(IEnumerable<UpdateAction> actions, UpdateActionKind kind) =>
        actions.Single(a => a.Kind == kind);

    private static FileFacts GoodFacts => new FileFacts(true, 1000, Sha);
    private static FileFacts BadFacts => new FileFacts(true, 1000, OtherSha);

    private static UpdatePlanner AtVerifyingExisting()
    {
        var planner = Planned(Found(), Folder(FinalName), out _);
        Assert.AreEqual(UpdatePhase.VerifyingExisting, planner.Phase);
        return planner;
    }

    private static UpdatePlanner AtDownloading()
    {
        var planner = Planned(Found(), Folder(), out _);
        Assert.AreEqual(UpdatePhase.Downloading, planner.Phase);
        return planner;
    }

    private static UpdatePlanner AtVerifyingDownload()
    {
        var planner = AtDownloading();
        planner.OnDownloadFinished(DownloadOutcome.Success());
        Assert.AreEqual(UpdatePhase.VerifyingDownload, planner.Phase);
        return planner;
    }

    private static UpdatePlanner AtAwaitingUser()
    {
        var planner = AtVerifyingExisting();
        planner.OnExistingVerified(GoodFacts);
        Assert.AreEqual(UpdatePhase.AwaitingUser, planner.Phase);
        return planner;
    }

    private static UpdatePlanner AtVerifyingBeforeApply()
    {
        var planner = AtAwaitingUser();
        planner.OnUserChoice(true);
        Assert.AreEqual(UpdatePhase.VerifyingBeforeApply, planner.Phase);
        return planner;
    }

    [Test]
    public void Begin_MovesToAwaitingRelease_AndAsksForNothing()
    {
        var planner = new UpdatePlanner(Current);
        Assert.AreEqual(UpdatePhase.Idle, planner.Phase);

        var actions = planner.Begin();

        Assert.IsEmpty(actions);
        Assert.AreEqual(UpdatePhase.AwaitingRelease, planner.Phase);
    }

    [Test]
    public void Begin_Twice_IsRejectedWithoutChangingThePhase()
    {
        var planner = Started();

        var actions = planner.Begin();

        Assert.AreEqual(UpdatePhase.AwaitingRelease, planner.Phase);
        Assert.AreEqual(UpdateLogLevel.Warning, actions.Single().Level);
    }

    [Test]
    public void ReleaseResolved_BeforeBegin_IsRejected()
    {
        var planner = new UpdatePlanner(Current);

        var actions = planner.OnReleaseResolved(Found(), Folder());

        Assert.AreEqual(UpdatePhase.Idle, planner.Phase);
        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log }, Kinds(actions));
        Assert.AreEqual(UpdateLogLevel.Warning, actions[0].Level);
    }

    [TestCase("")]
    [TestCase("abc")]
    [TestCase(null)]
    public void UnreadableCurrentVersion_SkipsEverything_EvenWithOldFilesInTheFolder(string current)
    {
        var old = InstallerFileName.For("0.1000");
        var planner = Planned(Found(), Folder(old), out var actions, current);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log }, Kinds(actions));
        Assert.AreEqual(UpdateLogLevel.Error, actions[0].Level);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void LookupFailed_LogsTheReason_TouchesNothing_AndPromisesARetryOnNextStart()
    {
        var old = InstallerFileName.For("0.1000");
        var planner = Planned(ReleaseLookup.Failed("HTTP 503"), Folder(old, PartName), out var actions);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log }, Kinds(actions),
            "сеть недоступна — неизвестно, последняя ли версия установлена, поэтому ничего не чистим");
        Assert.AreEqual(UpdateLogLevel.Error, actions[0].Level);
        StringAssert.Contains("HTTP 503", actions[0].Message);
        StringAssert.Contains("при следующем запуске", actions[0].Message);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void NoRelease_LogsAWarning_AndTouchesNothing()
    {
        var planner = Planned(ReleaseLookup.NoRelease("HTTP 404"), Folder(InstallerFileName.For("0.1000")), out var actions);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log }, Kinds(actions));
        Assert.AreEqual(UpdateLogLevel.Warning, actions[0].Level);
        StringAssert.Contains("HTTP 404", actions[0].Message);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void NewerRelease_FirstLine_AnnouncesTheNewVersion()
    {
        Planned(Found(), Folder(), out var actions);

        Assert.AreEqual(UpdateActionKind.Log, actions[0].Kind);
        Assert.AreEqual(UpdateLogLevel.Info, actions[0].Level);
        Assert.AreEqual("обнаружена новая версия " + Latest, actions[0].Message);
    }

    [Test]
    public void NewerRelease_WithoutInstallerAsset_AnnouncesIt_ThenRefuses()
    {
        var planner = Planned(ReleaseLookup.NoInstallerAsset(Latest, "нет"), Folder(), out var actions);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.Log }, Kinds(actions));
        Assert.AreEqual(UpdateLogLevel.Error, actions[1].Level);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [TestCase("KitchenDesigner-Setup-0.2099-x64.exe", TestName = "ForeignVersion_IsRefused")]
    [TestCase("kitchendesigner-setup-0.2100-x64.exe", TestName = "WrongCase_IsRefused")]
    [TestCase("KitchenDesigner-Setup-0.2100-x86.exe", TestName = "WrongArchitecture_IsRefused")]
    [TestCase("KitchenDesigner-Setup-0.21000-x64.exe", TestName = "LongerVersionSharingThePrefix_IsRefused")]
    [TestCase("Other-Setup-0.2100-x64.exe", TestName = "WrongProduct_IsRefused")]
    [TestCase("KitchenDesigner-Setup-0.2100-x64.exe.part", TestName = "PartNameAsAsset_IsRefused")]
    public void NewerRelease_WithAForeignAssetName_IsRefused_NothingIsDownloaded(string assetName)
    {
        var planner = Planned(Found(Manifest(file: assetName)), Folder(), out var actions);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.Log }, Kinds(actions));
        Assert.AreEqual(UpdateLogLevel.Error, actions[1].Level);
        StringAssert.Contains(assetName, actions[1].Message);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void NewerRelease_WithoutDownloadUrl_IsRefused()
    {
        var planner = Planned(Found(Manifest(url: "  ")), Folder(), out var actions);

        Assert.IsFalse(Kinds(actions).Contains(UpdateActionKind.Download));
        Assert.AreEqual(UpdateLogLevel.Error, actions.Last().Level);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [TestCase("0.2100-beta")]
    [TestCase("1.2.3.4")]
    [TestCase("v0.2100")]
    public void ReleaseVersionThatCannotBeAFileName_IsRefused(string version)
    {
        var lookup = ReleaseLookup.Found(new ReleaseManifest
        {
            Version = version, FileName = "x", DownloadUrl = "u", Sha256 = Sha, Size = 1,
        });
        var planner = Planned(lookup, Folder(), out var actions);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log }, Kinds(actions));
        Assert.AreEqual(UpdateLogLevel.Error, actions[0].Level);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void NewerRelease_WithDigest_EmptyFolder_StartsTheDownloadStraightAway()
    {
        var planner = Planned(Found(), Folder(), out var actions);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.Download }, Kinds(actions));
        var download = Single(actions, UpdateActionKind.Download);
        Assert.AreEqual(Latest, download.Version);
        Assert.AreEqual("https://gh/i.exe", download.Url);
        Assert.AreEqual(PartName, download.PartName, "качаем в .part, а не в готовое имя");
        Assert.AreEqual(1, download.Attempt);
        Assert.AreEqual(IntegrityMethod.Sha256, download.Expectation.Method);
        Assert.AreEqual(UpdatePhase.Downloading, planner.Phase);
    }

    [Test]
    public void NewerRelease_WithoutDigest_FallsBackToSize_AndSaysSo()
    {
        var planner = Planned(Found(Manifest(sha: "", size: 4242)), Folder(), out var actions);

        var download = Single(actions, UpdateActionKind.Download);
        Assert.AreEqual(IntegrityMethod.Size, download.Expectation.Method);
        Assert.AreEqual(4242, download.Expectation.Size);
        var warning = actions.Single(a => a.Kind == UpdateActionKind.Log && a.Level == UpdateLogLevel.Warning);
        StringAssert.Contains("SHA-256", warning.Message);
        StringAssert.Contains("4242", warning.Message);
        Assert.AreEqual(UpdatePhase.Downloading, planner.Phase);
    }

    [Test]
    public void NewerRelease_WithDigest_DoesNotWarnAboutSize()
    {
        Planned(Found(), Folder(), out var actions);

        Assert.IsFalse(actions.Any(a => a.Kind == UpdateActionKind.Log && a.Level == UpdateLogLevel.Warning));
    }

    [Test]
    public void NewerRelease_WithDigestAndSize_PrefersTheDigest()
    {
        Planned(Found(Manifest(sha: Sha, size: 4242)), Folder(), out var actions);

        Assert.AreEqual(IntegrityMethod.Sha256, Single(actions, UpdateActionKind.Download).Expectation.Method);
    }

    [TestCase("")]
    [TestCase("not-a-hash")]
    [TestCase("abc")]
    public void NewerRelease_WithNeitherADigestNorASize_IsRefused_NothingIsDownloaded(string sha)
    {
        var planner = Planned(Found(Manifest(sha: sha, size: 0)), Folder(), out var actions);

        Assert.IsFalse(Kinds(actions).Contains(UpdateActionKind.Download), "непроверяемый установщик не качаем вовсе");
        Assert.IsFalse(Kinds(actions).Contains(UpdateActionKind.Apply));
        Assert.AreEqual(UpdateLogLevel.Error, actions.Last().Level);
        StringAssert.Contains("не будет загружен и запущен", actions.Last().Message);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void NewerRelease_WithAFinishedFileAlreadyOnDisk_VerifiesItBeforeAnythingElse()
    {
        var planner = Planned(Found(), Folder(FinalName), out var actions);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.VerifyExisting }, Kinds(actions));
        var verify = Single(actions, UpdateActionKind.VerifyExisting);
        Assert.AreEqual(FinalName, verify.FileName);
        Assert.AreEqual(IntegrityMethod.Sha256, verify.Expectation.Method);
        Assert.AreEqual(UpdatePhase.VerifyingExisting, planner.Phase);
    }

    [Test]
    public void NewerRelease_WithAFinishedFileAndAStalePart_DeletesThePart_AndVerifiesTheFile()
    {
        Planned(Found(), Folder(FinalName, PartName), out var actions);

        CollectionAssert.AreEqual(
            new[] { UpdateActionKind.Log, UpdateActionKind.Log, UpdateActionKind.Cleanup, UpdateActionKind.VerifyExisting },
            Kinds(actions));
        CollectionAssert.AreEqual(new[] { PartName }, Single(actions, UpdateActionKind.Cleanup).Files);
    }

    [Test]
    public void NewerRelease_WithOnlyAPartOfTheTarget_DeletesIt_AndDownloadsAgainFromScratch()
    {
        var planner = Planned(Found(), Folder(PartName), out var actions);

        CollectionAssert.AreEqual(
            new[] { UpdateActionKind.Log, UpdateActionKind.Log, UpdateActionKind.Cleanup, UpdateActionKind.Download },
            Kinds(actions));
        CollectionAssert.AreEqual(new[] { PartName }, Single(actions, UpdateActionKind.Cleanup).Files);
        Assert.AreEqual(UpdatePhase.Downloading, planner.Phase);
    }

    [Test]
    public void NewerRelease_CleansOldInstallersAndEveryPart_ButKeepsFutureOnesAndLookalikes()
    {
        var folder = Folder(
            InstallerFileName.For("0.1500"),
            InstallerFileName.For(Current),
            InstallerFileName.For("0.2050"),
            InstallerFileName.PartFor("0.2050"),
            InstallerFileName.For("0.2200"),
            InstallerFileName.For("0.2100") + ".bak",
            "KitchenDesigner-Setup-0.1500-x86.exe",
            "KitchenDesigner-Setup-0.1500-x64.log",
            "notes.txt");

        Planned(Found(), folder, out var actions);

        var cleanup = Single(actions, UpdateActionKind.Cleanup).Files;
        CollectionAssert.AreEquivalent(new[]
        {
            InstallerFileName.For("0.1500"),
            InstallerFileName.For(Current),
            InstallerFileName.For("0.2050"),
            InstallerFileName.PartFor("0.2050"),
        }, cleanup);
        Assert.AreEqual(UpdateActionKind.Download, actions.Last().Kind);
    }

    [Test]
    public void SameVersion_IsUpToDate_AndCleansEveryInstallerNotNewerThanCurrent_AndEveryPart()
    {
        var folder = Folder(
            InstallerFileName.For("0.1500"),
            InstallerFileName.For(Current),
            InstallerFileName.PartFor(Current),
            InstallerFileName.PartFor("0.2100"),
            InstallerFileName.For("0.2100"),
            "readme.txt");

        var planner = Planned(Found(Manifest(Current)), folder, out var actions);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.Log, UpdateActionKind.Cleanup },
            Kinds(actions));
        StringAssert.Contains("актуальная версия", actions[0].Message);
        CollectionAssert.AreEquivalent(new[]
        {
            InstallerFileName.For("0.1500"),
            InstallerFileName.For(Current),
            InstallerFileName.PartFor(Current),
            InstallerFileName.PartFor("0.2100"),
        }, Single(actions, UpdateActionKind.Cleanup).Files);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void OlderRelease_IsTreatedAsUpToDate_NoDownload_NoDialog()
    {
        var planner = Planned(Found(Manifest("0.1900")), Folder(InstallerFileName.For("0.1800")), out var actions);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.Log, UpdateActionKind.Cleanup },
            Kinds(actions));
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void UpToDate_WithNothingStale_OnlyLogs()
    {
        Planned(Found(Manifest(Current)), Folder("readme.txt", InstallerFileName.For("0.2500")), out var actions);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log }, Kinds(actions));
    }

    [Test]
    public void UpToDate_EvenWhenTheReleaseHasNoInstallerAsset_StillCleans()
    {
        var planner = Planned(ReleaseLookup.NoInstallerAsset(Current, "нет"),
            Folder(InstallerFileName.For("0.1500")), out var actions);

        Assert.IsTrue(Kinds(actions).Contains(UpdateActionKind.Cleanup));
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void ExistingVerified_WithMatchingDigest_GoesStraightToTheDialog_NoDownload()
    {
        var planner = AtVerifyingExisting();

        var actions = planner.OnExistingVerified(GoodFacts);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.ShowDialog }, Kinds(actions));
        var dialog = Single(actions, UpdateActionKind.ShowDialog);
        Assert.AreEqual(Latest, dialog.Version);
        Assert.AreEqual(FinalName, dialog.FileName);
        Assert.AreEqual(UpdatePhase.AwaitingUser, planner.Phase);
    }

    [Test]
    public void ExistingVerified_WithAWrongDigest_DeletesTheFile_AndDownloadsAgain()
    {
        var planner = AtVerifyingExisting();

        var actions = planner.OnExistingVerified(BadFacts);

        CollectionAssert.AreEqual(
            new[] { UpdateActionKind.Log, UpdateActionKind.Delete, UpdateActionKind.Download }, Kinds(actions));
        Assert.AreEqual(FinalName, Single(actions, UpdateActionKind.Delete).FileName);
        Assert.AreEqual(1, Single(actions, UpdateActionKind.Download).Attempt);
        Assert.AreEqual(UpdatePhase.Downloading, planner.Phase);
    }

    [Test]
    public void ExistingVerified_WhenTheFileVanished_DownloadsWithoutDeleting()
    {
        var planner = AtVerifyingExisting();

        var actions = planner.OnExistingVerified(FileFacts.Absent);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.Download }, Kinds(actions));
        Assert.AreEqual(UpdatePhase.Downloading, planner.Phase);
    }

    [Test]
    public void ExistingVerified_BySize_AcceptsEqualSize_RejectsAnyOther()
    {
        var accepted = Planned(Found(Manifest(sha: "", size: 777)), Folder(FinalName), out _);
        var okActions = accepted.OnExistingVerified(new FileFacts(true, 777, null));
        Assert.AreEqual(UpdateActionKind.ShowDialog, okActions.Last().Kind);

        var rejected = Planned(Found(Manifest(sha: "", size: 777)), Folder(FinalName), out _);
        var badActions = rejected.OnExistingVerified(new FileFacts(true, 776, null));
        Assert.AreEqual(UpdateActionKind.Download, badActions.Last().Kind);
        Assert.IsTrue(Kinds(badActions).Contains(UpdateActionKind.Delete), "обрезанный файл нужно убрать");
    }

    [Test]
    public void DownloadFinished_Failure_LogsTheReason_DeletesThePart_AndStops()
    {
        var planner = AtDownloading();

        var actions = planner.OnDownloadFinished(DownloadOutcome.Failure("HTTP 404"));

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.Delete }, Kinds(actions));
        Assert.AreEqual(UpdateLogLevel.Error, actions[0].Level);
        StringAssert.Contains("HTTP 404", actions[0].Message);
        StringAssert.Contains("при следующем запуске", actions[0].Message);
        Assert.AreEqual(PartName, Single(actions, UpdateActionKind.Delete).FileName);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void DownloadFinished_Success_VerifiesThePart_NotTheFinalName()
    {
        var planner = AtDownloading();

        var actions = planner.OnDownloadFinished(DownloadOutcome.Success());

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.VerifyDownloaded }, Kinds(actions));
        var verify = Single(actions, UpdateActionKind.VerifyDownloaded);
        Assert.AreEqual(PartName, verify.FileName);
        Assert.AreEqual(IntegrityMethod.Sha256, verify.Expectation.Method);
        Assert.AreEqual(UpdatePhase.VerifyingDownload, planner.Phase);
    }

    [Test]
    public void DownloadVerified_Ok_PromotesThePartToTheFinalName_OnlyThen()
    {
        var planner = AtVerifyingDownload();

        var actions = planner.OnDownloadVerified(GoodFacts);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.Promote }, Kinds(actions));
        var promote = Single(actions, UpdateActionKind.Promote);
        Assert.AreEqual(PartName, promote.PartName);
        Assert.AreEqual(FinalName, promote.FileName);
        Assert.AreEqual(UpdatePhase.Promoting, planner.Phase);
    }

    [Test]
    public void DownloadVerified_FirstMismatch_DeletesThePart_AndDownloadsOnceMore()
    {
        var planner = AtVerifyingDownload();

        var actions = planner.OnDownloadVerified(BadFacts);

        CollectionAssert.AreEqual(
            new[] { UpdateActionKind.Delete, UpdateActionKind.Log, UpdateActionKind.Download }, Kinds(actions));
        Assert.AreEqual(PartName, Single(actions, UpdateActionKind.Delete).FileName);
        Assert.AreEqual(2, Single(actions, UpdateActionKind.Download).Attempt);
        Assert.AreEqual(UpdatePhase.Downloading, planner.Phase);
    }

    [Test]
    public void DownloadVerified_SecondMismatch_GivesUp_NeverPromotes_NeverApplies()
    {
        var planner = AtVerifyingDownload();
        planner.OnDownloadVerified(BadFacts);
        planner.OnDownloadFinished(DownloadOutcome.Success());

        var actions = planner.OnDownloadVerified(BadFacts);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Delete, UpdateActionKind.Log }, Kinds(actions));
        Assert.AreEqual(UpdateLogLevel.Error, actions[1].Level);
        StringAssert.Contains("дважды", actions[1].Message);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void DownloadVerified_FirstMismatchThenSuccess_PromotesTheSecondDownload()
    {
        var planner = AtVerifyingDownload();
        planner.OnDownloadVerified(BadFacts);
        planner.OnDownloadFinished(DownloadOutcome.Success());

        var actions = planner.OnDownloadVerified(GoodFacts);

        Assert.AreEqual(UpdateActionKind.Promote, actions.Last().Kind);
    }

    [Test]
    public void DownloadVerified_AMissingPart_CountsAsAFailedAttempt()
    {
        var planner = AtVerifyingDownload();

        var actions = planner.OnDownloadVerified(FileFacts.Absent);

        Assert.AreEqual(UpdateActionKind.Download, actions.Last().Kind);
        Assert.AreEqual(2, actions.Last().Attempt);
    }

    [Test]
    public void BrokenExistingFile_DoesNotSpendTheDownloadAttempts()
    {
        var planner = AtVerifyingExisting();
        planner.OnExistingVerified(BadFacts);
        planner.OnDownloadFinished(DownloadOutcome.Success());

        var firstMismatch = planner.OnDownloadVerified(BadFacts);

        Assert.AreEqual(UpdateActionKind.Download, firstMismatch.Last().Kind,
            "битый файл с прошлого запуска не должен отнимать у свежей загрузки её единственную повторную попытку");
    }

    [Test]
    public void SizeOnlyExpectation_JudgesTheDownloadedPartBySize()
    {
        var planner = Planned(Found(Manifest(sha: "", size: 500)), Folder(), out _);
        planner.OnDownloadFinished(DownloadOutcome.Success());

        var actions = planner.OnDownloadVerified(new FileFacts(true, 500, null));

        Assert.AreEqual(UpdateActionKind.Promote, actions.Last().Kind);
    }

    [Test]
    public void Promoted_Ok_ShowsTheDialogForTheFinalFile()
    {
        var planner = AtVerifyingDownload();
        planner.OnDownloadVerified(GoodFacts);

        var actions = planner.OnPromoted(true, "");

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.ShowDialog }, Kinds(actions));
        Assert.AreEqual(FinalName, Single(actions, UpdateActionKind.ShowDialog).FileName);
        Assert.AreEqual(UpdatePhase.AwaitingUser, planner.Phase);
    }

    [Test]
    public void Promoted_Failure_LogsTheReason_RemovesThePart_AndStops()
    {
        var planner = AtVerifyingDownload();
        planner.OnDownloadVerified(GoodFacts);

        var actions = planner.OnPromoted(false, "диск занят");

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.Delete }, Kinds(actions));
        StringAssert.Contains("диск занят", actions[0].Message);
        Assert.AreEqual(UpdateLogLevel.Error, actions[0].Level);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void UserDeclines_KeepsTheFile_AndStops()
    {
        var planner = AtAwaitingUser();

        var actions = planner.OnUserChoice(false);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log }, Kinds(actions));
        StringAssert.Contains(FinalName, actions[0].Message);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void UserAccepts_VerifiesTheFileAgain_BeforeRunningAnything()
    {
        var planner = AtAwaitingUser();

        var actions = planner.OnUserChoice(true);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.VerifyBeforeApply }, Kinds(actions));
        Assert.AreEqual(FinalName, Single(actions, UpdateActionKind.VerifyBeforeApply).FileName);
        Assert.IsFalse(Kinds(actions).Contains(UpdateActionKind.Apply));
        Assert.AreEqual(UpdatePhase.VerifyingBeforeApply, planner.Phase);
    }

    [Test]
    public void ApplyVerified_Ok_RunsTheInstaller()
    {
        var planner = AtVerifyingBeforeApply();

        var actions = planner.OnApplyVerified(GoodFacts);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.Apply }, Kinds(actions));
        Assert.AreEqual(FinalName, Single(actions, UpdateActionKind.Apply).FileName);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void ApplyVerified_ChangedFile_IsDeleted_AndNeverRun()
    {
        var planner = AtVerifyingBeforeApply();

        var actions = planner.OnApplyVerified(BadFacts);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log, UpdateActionKind.Delete }, Kinds(actions));
        Assert.AreEqual(UpdateLogLevel.Error, actions[0].Level);
        Assert.AreEqual(FinalName, Single(actions, UpdateActionKind.Delete).FileName);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void ApplyVerified_FileGone_IsNeverRun_NothingToDelete()
    {
        var planner = AtVerifyingBeforeApply();

        var actions = planner.OnApplyVerified(FileFacts.Absent);

        CollectionAssert.AreEqual(new[] { UpdateActionKind.Log }, Kinds(actions));
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void ApplyVerified_BySizeOnly_ChecksTheSizeAgain()
    {
        var planner = Planned(Found(Manifest(sha: "", size: 500)), Folder(FinalName), out _);
        planner.OnExistingVerified(new FileFacts(true, 500, null));
        planner.OnUserChoice(true);

        var actions = planner.OnApplyVerified(new FileFacts(true, 501, null));

        Assert.IsFalse(Kinds(actions).Contains(UpdateActionKind.Apply));
    }

    [Test]
    public void TheInstallerIsNeverRun_WithoutAFreshPositiveVerification()
    {
        var planner = AtAwaitingUser();

        var early = planner.OnApplyVerified(GoodFacts);

        Assert.IsFalse(Kinds(early).Contains(UpdateActionKind.Apply),
            "Apply возможен только после согласия пользователя И свежей проверки");
        Assert.AreEqual(UpdatePhase.AwaitingUser, planner.Phase);
    }

    private static IEnumerable<TestCaseData> WrongPhaseCalls()
    {
        yield return Call("OnExistingVerified", p => p.OnExistingVerified(GoodFacts));
        yield return Call("OnDownloadFinished", p => p.OnDownloadFinished(DownloadOutcome.Success()));
        yield return Call("OnDownloadVerified", p => p.OnDownloadVerified(GoodFacts));
        yield return Call("OnPromoted", p => p.OnPromoted(true, ""));
        yield return Call("OnUserChoice", p => p.OnUserChoice(true));
        yield return Call("OnApplyVerified", p => p.OnApplyVerified(GoodFacts));
    }

    private static TestCaseData Call(string name, System.Func<UpdatePlanner, IReadOnlyList<UpdateAction>> call) =>
        new TestCaseData(name, call).SetName("WrongPhase_" + name + "_IsRejected");

    [TestCaseSource(nameof(WrongPhaseCalls))]
    public void EventInTheWrongPhase_AnswersWithOneWarning_WithoutMovingTheMachine(
        string name, System.Func<UpdatePlanner, IReadOnlyList<UpdateAction>> call)
    {
        var planner = Started();

        var actions = call(planner);

        Assert.AreEqual(UpdatePhase.AwaitingRelease, planner.Phase, name + " в чужой фазе не двигает автомат");
        Assert.AreEqual(1, actions.Count);
        Assert.AreEqual(UpdateActionKind.Log, actions[0].Kind);
        Assert.AreEqual(UpdateLogLevel.Warning, actions[0].Level);
    }

    [Test]
    public void EachEventOutOfOrder_AnswersWithASingleWarning_AndNothingElse()
    {
        var planner = AtVerifyingExisting();

        var answers = new[]
        {
            planner.OnDownloadFinished(DownloadOutcome.Success()),
            planner.OnDownloadVerified(GoodFacts),
            planner.OnPromoted(true, ""),
            planner.OnUserChoice(true),
            planner.OnApplyVerified(GoodFacts),
            planner.OnReleaseResolved(Found(), Folder()),
        };

        foreach (var answer in answers)
        {
            Assert.AreEqual(1, answer.Count);
            Assert.AreEqual(UpdateActionKind.Log, answer[0].Kind);
            Assert.AreEqual(UpdateLogLevel.Warning, answer[0].Level);
        }
        Assert.AreEqual(UpdatePhase.VerifyingExisting, planner.Phase);
    }

    [Test]
    public void HappyPath_FromANewRelease_ToTheInstaller()
    {
        var planner = Planned(Found(), Folder(), out var first);
        Assert.AreEqual(UpdateActionKind.Download, first.Last().Kind);

        planner.OnDownloadFinished(DownloadOutcome.Success());
        planner.OnDownloadVerified(GoodFacts);
        var dialog = planner.OnPromoted(true, "");
        Assert.AreEqual(UpdateActionKind.ShowDialog, dialog.Last().Kind);

        planner.OnUserChoice(true);
        var applied = planner.OnApplyVerified(GoodFacts);

        Assert.AreEqual(UpdateActionKind.Apply, applied.Last().Kind);
        Assert.AreEqual(UpdatePhase.Finished, planner.Phase);
    }

    [Test]
    public void NextStart_AfterAFailedDownload_SeesTheLeftoversAndStartsFromScratch()
    {
        var firstRun = AtDownloading();
        var failure = firstRun.OnDownloadFinished(DownloadOutcome.Failure("нет сети"));
        Assert.AreEqual(UpdateActionKind.Delete, failure.Last().Kind);

        var secondRun = Planned(Found(), Folder(PartName), out var actions);

        Assert.AreEqual(UpdateActionKind.Download, actions.Last().Kind);
        Assert.AreEqual(1, actions.Last().Attempt, "новый запуск получает полный набор попыток");
        CollectionAssert.AreEqual(new[] { PartName }, Single(actions, UpdateActionKind.Cleanup).Files);
        Assert.AreEqual(UpdatePhase.Downloading, secondRun.Phase);
    }
}
