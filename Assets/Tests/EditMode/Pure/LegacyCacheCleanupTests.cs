#nullable disable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Разовая уборка прежнего места загрузки (кэш приложения в Temp): там лежали установщики под
/// ровно теми же именами, но оно общее с файлами самого Unity, поэтому удаляется только то, что
/// называется как наш установщик, его .part или его лог, и никакая версия не щадится — этой
/// папкой обновление больше не пользуется.
/// </summary>
public class LegacyCacheCleanupTests
{
    private TempUpdateFolder _temp;

    [SetUp]
    public void SetUp() => _temp = new TempUpdateFolder();

    [TearDown]
    public void TearDown() => _temp.Dispose();

    [Test]
    public void RemovesInstallersPartsAndLogsOfAnyVersion_AndNothingElse()
    {
        var kept = new[]
        {
            "notes.txt",
            "UnityShaderCache.bin",
            "KitchenDesigner-Setup-0.2100-x64.exe.bak",
            "KitchenDesigner-Setup-0.2100-x86.exe",
            "kitchendesigner-setup-0.2100-x64.exe",
        };
        foreach (var name in kept) _temp.Write(name, new byte[3]);
        _temp.Write(InstallerFileName.For("0.0100"), new byte[3]);
        _temp.Write(InstallerFileName.For("9.9"), new byte[3]);
        _temp.Write(InstallerFileName.PartFor("0.2100"), new byte[3]);
        _temp.Write(InstallerFileName.LogFor("0.2100"), new byte[3]);
        var console = new RecordingConsole();

        bool complete = LegacyCacheCleanup.Run(_temp.Folder, console);

        Assert.IsTrue(complete);
        CollectionAssert.AreEquivalent(kept, _temp.Names().ToArray());
        Assert.AreEqual(1, console.Lines.Count);
        Assert.AreEqual(UpdateLogLevel.Info, console.Lines[0].level);
        StringAssert.Contains("(4)", console.Lines[0].text);
    }

    [Test]
    public void NothingToRemove_IsSilent_AndComplete()
    {
        _temp.Write("notes.txt", new byte[1]);
        var console = new RecordingConsole();

        Assert.IsTrue(LegacyCacheCleanup.Run(_temp.Folder, console));

        Assert.IsEmpty(console.Lines);
    }

    [Test]
    public void AFolderThatDoesNotExist_IsComplete_AndSilent()
    {
        var console = new RecordingConsole();

        Assert.IsTrue(LegacyCacheCleanup.Run(_temp.Folder, console));

        Assert.IsEmpty(console.Lines);
    }

    [Test]
    public void ALockedFile_IsReported_AndTheRunIsNotComplete_SoItIsRetriedNextStart()
    {
        var locked = InstallerFileName.For("0.1000");
        var other = InstallerFileName.For("0.1100");
        _temp.Write(locked, new byte[3]);
        _temp.Write(other, new byte[3]);
        var console = new RecordingConsole();
        bool complete;

        using (new FileStream(_temp.PathOf(locked), FileMode.Open, FileAccess.Read, FileShare.None))
            complete = LegacyCacheCleanup.Run(_temp.Folder, console);

        Assert.IsFalse(complete);
        Assert.IsTrue(_temp.Has(locked));
        Assert.IsFalse(_temp.Has(other), "один занятый файл не мешает убрать остальные");
        Assert.IsTrue(console.Lines.Any(l => l.level == UpdateLogLevel.Warning && l.text.Contains(locked)));
    }

    private sealed class UnreadableFolder : IUpdateFolder
    {
        public string PathOf(string name) => name;
        public IReadOnlyList<FolderEntry> List() => throw new IOException("доступ запрещён");
        public void EnsureExists() { }
        public bool TryDelete(string name, out string error) { error = ""; return true; }
        public bool TryPromote(string partName, string finalName, out string error) { error = ""; return true; }
    }

    [Test]
    public void AnUnreadableFolder_IsAWarning_NotAnException_AndNotComplete()
    {
        var console = new RecordingConsole();

        bool complete = LegacyCacheCleanup.Run(new UnreadableFolder(), console);

        Assert.IsFalse(complete);
        Assert.AreEqual(UpdateLogLevel.Warning, console.Lines.Single().level);
        StringAssert.Contains("доступ запрещён", console.Lines.Single().text);
    }
}
