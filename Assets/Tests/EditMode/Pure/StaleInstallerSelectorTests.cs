#nullable disable
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Чистка папки обновлений: список того, что можно удалить, обязан содержать ровно устаревшие
/// установщики и недокачанные .part — и ничего больше. Любая лишняя строка здесь — чужой
/// файл, удалённый у пользователя.
/// </summary>
public class StaleInstallerSelectorTests
{
    private const string Current = "0.2000";

    private static FolderEntry[] Folder(params string[] names) =>
        names.Select(n => new FolderEntry(n, 1)).ToArray();

    [Test]
    public void UpToDate_ListsInstallersNotNewerThanCurrent_AndEveryPart()
    {
        var folder = Folder(
            InstallerFileName.For("0.1000"),
            InstallerFileName.For("0.2000"),
            InstallerFileName.For("0.2001"),
            InstallerFileName.PartFor("0.1000"),
            InstallerFileName.PartFor("0.2001"));

        var stale = StaleInstallerSelector.Select(folder, Current, null);

        CollectionAssert.AreEqual(new[]
        {
            InstallerFileName.For("0.1000"),
            InstallerFileName.For("0.2000"),
            InstallerFileName.PartFor("0.1000"),
            InstallerFileName.PartFor("0.2001"),
        }.OrderBy(n => n, System.StringComparer.Ordinal), stale);
    }

    [Test]
    public void NewerRelease_AlsoListsInstallersBetweenCurrentAndTarget_ButNotTheTargetOrFutureOnes()
    {
        var folder = Folder(
            InstallerFileName.For("0.1500"),
            InstallerFileName.For("0.2050"),
            InstallerFileName.For("0.2100"),
            InstallerFileName.For("0.2200"));

        var stale = StaleInstallerSelector.Select(folder, Current, "0.2100");

        CollectionAssert.AreEquivalent(
            new[] { InstallerFileName.For("0.1500"), InstallerFileName.For("0.2050") }, stale);
    }

    [Test]
    public void Lookalikes_AndForeignFiles_AreNeverListed()
    {
        var folder = Folder(
            "KitchenDesigner-Setup-0.1000-x64.exe.bak",
            "KitchenDesigner-Setup-0.1000-x64.log",
            "KitchenDesigner-Setup-0.1000-x86.exe",
            "kitchendesigner-setup-0.1000-x64.exe",
            "KitchenDesigner-Setup--x64.exe",
            "KitchenDesigner-Setup-abc-x64.exe",
            "KitchenDesigner-Setup-0.1000-x64.exe.part.bak",
            "Other-Setup-0.1000-x64.exe",
            "..\\KitchenDesigner-Setup-0.1000-x64.exe",
            "sub\\KitchenDesigner-Setup-0.1000-x64.exe",
            "C:\\KitchenDesigner-Setup-0.1000-x64.exe",
            "notes.txt",
            "");

        Assert.IsEmpty(StaleInstallerSelector.Select(folder, Current, null));
        Assert.IsEmpty(StaleInstallerSelector.Select(folder, Current, "0.2100"));
    }

    [Test]
    public void Result_IsSortedOrdinally_ForStableLogsAndTests()
    {
        var folder = Folder(InstallerFileName.For("0.1200"), InstallerFileName.For("0.1100"),
            InstallerFileName.PartFor("0.0900"));

        var stale = StaleInstallerSelector.Select(folder, Current, null);

        CollectionAssert.AreEqual(stale.OrderBy(n => n, System.StringComparer.Ordinal), stale);
    }

    [Test]
    public void EmptyFolder_ListsNothing()
    {
        Assert.IsEmpty(StaleInstallerSelector.Select(new FolderEntry[0], Current, null));
    }

    [Test]
    public void VersionsAreComparedAsNumbers_NotAsText()
    {
        var folder = Folder(InstallerFileName.For("0.900"), InstallerFileName.For("0.10000"));

        var stale = StaleInstallerSelector.Select(folder, "0.2000", null);

        CollectionAssert.AreEqual(new[] { InstallerFileName.For("0.900") }, stale,
            "0.900 старее 0.2000, а 0.10000 новее — строковое сравнение перепутало бы их");
    }
}
