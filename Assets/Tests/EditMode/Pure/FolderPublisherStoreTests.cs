using System;
using System.IO;
using System.Linq;
using KitchenDesigner.Core;
using NUnit.Framework;

// Перенос persistentDataPath LocalLow\DefaultCompany\KitchenDesigner2 → LocalLow\Evgeniy347\KitchenDesigner2
// на временной папке вместо LocalLow. Плеер открывает Player.log в НОВОЙ папке раньше любого
// скрипта, поэтому к моменту переноса она обычно уже есть и держит открытый лог: целиком её не
// переименовать, переезжает содержимое — кроме логов движка, которые и есть «пустота».
public class FolderPublisherStoreTests
{
    private const string Product = "KitchenDesigner2";

    private string _root = "";

    private string OldDir => Path.Combine(_root, "DefaultCompany", Product);
    private string NewDir => Path.Combine(_root, "Evgeniy347", Product);

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "kd-publisher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private FolderPublisherStore Store() =>
        new FolderPublisherStore(_root, PublisherRename.OldCompany, PublisherRename.NewCompany, Product);

    private static PublisherMigrationLog Quiet() => new PublisherMigrationLog(_ => { }, _ => { }, _ => { });

    private static readonly (string path, byte[] bytes)[] UserFiles =
    {
        (Path.Combine("saves", "Кухня.kdproj"), new byte[] { 0x7B, 0x00, 0xFF, 0xD0, 0x9A, 0x7D }),
        (Path.Combine("saves", "autosave.json"), System.Text.Encoding.UTF8.GetBytes("{\"elements\":[]}")),
        ("perf.csv", System.Text.Encoding.UTF8.GetBytes("frame;ms\r\n1;16,6\r\n")),
    };

    private static void SeedUserFiles(string dir)
    {
        foreach (var (path, bytes) in UserFiles)
        {
            var full = Path.Combine(dir, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, bytes);
        }
    }

    private static void AssertUserFilesIn(string dir)
    {
        foreach (var (path, bytes) in UserFiles)
        {
            var full = Path.Combine(dir, path);
            Assert.IsTrue(File.Exists(full), "не переехал " + path);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(full), path + " перенёсся не байт в байт");
        }
    }

    [Test]
    public void NewFolderMissing_TheWholeFolderMoves_AndEmptyDefaultCompanyGoes()
    {
        SeedUserFiles(OldDir);
        File.WriteAllText(Path.Combine(OldDir, "Player.log"), "old log");

        Assert.AreEqual(PublisherMoveOutcome.Moved, PublisherMigration.Run(Store(), Quiet()));

        AssertUserFilesIn(NewDir);
        Assert.IsTrue(File.Exists(Path.Combine(NewDir, "Player.log")), "при переносе целиком старый лог едет вместе с папкой");
        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "DefaultCompany")), "пустой DefaultCompany должен уйти");
    }

    [Test]
    public void NewFolderHoldsTheLogTheRunningPlayerKeepsOpen_UserFilesStillMove()
    {
        SeedUserFiles(OldDir);
        File.WriteAllText(Path.Combine(OldDir, "Player.log"), "old log");
        Directory.CreateDirectory(NewDir);
        using var runningLog = new FileStream(Path.Combine(NewDir, "Player.log"), FileMode.Create, FileAccess.Write, FileShare.Read);

        Assert.AreEqual(PublisherMoveOutcome.Moved, PublisherMigration.Run(Store(), Quiet()),
            "открытый Player.log — запись движка, а не данные пользователя");

        AssertUserFilesIn(NewDir);
        Assert.IsFalse(Directory.Exists(Path.Combine(OldDir, "saves")));
        Assert.AreEqual("old log", File.ReadAllText(Path.Combine(OldDir, "Player.log")),
            "старый лог не затирает открытый новый и не удаляется — он просто остаётся на старом месте");
    }

    [Test]
    public void SiblingProductUnderDefaultCompany_KeepsTheParent()
    {
        SeedUserFiles(OldDir);
        Directory.CreateDirectory(Path.Combine(_root, "DefaultCompany", "OtherGame"));

        PublisherMigration.Run(Store(), Quiet());

        Assert.IsFalse(Directory.Exists(OldDir));
        Assert.IsTrue(Directory.Exists(Path.Combine(_root, "DefaultCompany", "OtherGame")));
    }

    [Test]
    public void BothHoldData_NothingChanges()
    {
        SeedUserFiles(OldDir);
        Directory.CreateDirectory(Path.Combine(NewDir, "saves"));
        File.WriteAllText(Path.Combine(NewDir, "saves", "new.kdproj"), "new");

        Assert.AreEqual(PublisherMoveOutcome.LeftBothAlone, PublisherMigration.Run(Store(), Quiet()));

        AssertUserFilesIn(OldDir);
        Assert.AreEqual(new[] { "new.kdproj" }, Directory.GetFiles(Path.Combine(NewDir, "saves")).Select(Path.GetFileName));
    }

    [Test]
    public void SecondRun_FindsNothingToMove_AndChangesNothing()
    {
        SeedUserFiles(OldDir);
        PublisherMigration.Run(Store(), Quiet());

        Assert.AreEqual(PublisherMoveOutcome.NothingToMove, PublisherMigration.Run(Store(), Quiet()));
        AssertUserFilesIn(NewDir);
    }

    [Test]
    public void OneEntryCannotMove_EverythingAlreadyMovedComesBack()
    {
        SeedUserFiles(OldDir);
        var locked = Path.Combine(OldDir, "zz-open-by-the-old-version.json");
        File.WriteAllText(locked, "busy");
        Directory.CreateDirectory(NewDir);
        File.WriteAllText(Path.Combine(NewDir, "Player.log"), "engine");
        var errors = new System.Collections.Generic.List<string>();

        PublisherMoveOutcome outcome;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read))
            outcome = PublisherMigration.Run(Store(), new PublisherMigrationLog(_ => { }, _ => { }, errors.Add));

        Assert.AreEqual(PublisherMoveOutcome.Failed, outcome, "файл, открытый без FILE_SHARE_DELETE, не переносится");
        Assert.AreEqual(1, errors.Count, "сбой должен дойти до лога");
        AssertUserFilesIn(OldDir);
        Assert.AreEqual(new[] { "Player.log" }, Directory.GetFileSystemEntries(NewDir).Select(Path.GetFileName),
            "всё или ничего: уже перенесённые saves и perf.csv вернулись на старое место");
    }

    [Test]
    public void NoOldFolder_DoesNotCreateTheNewOne()
    {
        Assert.AreEqual(PublisherMoveOutcome.NothingToMove, PublisherMigration.Run(Store(), Quiet()));
        Assert.IsFalse(Directory.Exists(NewDir));
    }
}
