using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Баг-репорт: пользователь уже работает в проекте (SaveLoadManager.HasLastPath
/// истинен), но список недавних пуст — и окно «Загрузить» говорит «Недавних проектов пока
/// нет». RecentProjects.Paths() обязан подставить текущий проект в этом случае, а не
/// показывать пустой список при заведомо существующем текущем проекте.</summary>
public class RecentProjectsTests
{
    private string[]? _recentBackup;
    private string? _prevLastPath;

    [SetUp]
    public void SetUp()
    {
        _recentBackup = RecentProjectsTestBackup.Capture();
        _prevLastPath = SaveLoadManager.LastPath;
    }

    [TearDown]
    public void TearDown()
    {
        RecentProjectsTestBackup.Restore(_recentBackup!);
        SaveLoadManager.LastPath = _prevLastPath!;
    }

    [Test]
    public void Paths_WhenListIsEmpty_ButACurrentProjectIsOpen_SeedsWithIt()
    {
        RecentProjectsTestBackup.Restore(new string[0]);
        SaveLoadManager.LastPath = @"C:\temp\seeded_current_project.kdproj";

        CollectionAssert.AreEqual(new[] { @"C:\temp\seeded_current_project.kdproj" },
            RecentProjects.Paths(),
            "список пуст, но текущий проект есть — он обязан появиться, не пустой список");
    }

    [Test]
    public void Paths_WhenListIsEmpty_AndNoCurrentProject_StaysEmpty()
    {
        RecentProjectsTestBackup.Restore(new string[0]);
        SaveLoadManager.LastPath = "";

        Assert.AreEqual(0, RecentProjects.Paths().Length,
            "ни списка, ни текущего проекта — показывать нечего, это не баг");
    }

    [Test]
    public void Paths_WhenTheStoredListAlreadyHasEntries_DoesNotSeed_AndReturnsThemAsIs()
    {
        RecentProjectsTestBackup.Restore(new[] { "a.kdproj", "b.kdproj" });
        SaveLoadManager.LastPath = @"C:\temp\some_other_current_project.kdproj";

        CollectionAssert.AreEqual(new[] { "a.kdproj", "b.kdproj" }, RecentProjects.Paths(),
            "список не пуст — сидинг не подменяет и не дополняет настоящую историю");
    }

    [Test]
    public void Remember_AddsThePath_ToTheFrontOfPaths()
    {
        RecentProjectsTestBackup.Restore(new string[0]);
        SaveLoadManager.LastPath = "";

        RecentProjects.Remember(@"C:\temp\remembered.kdproj");

        CollectionAssert.AreEqual(new[] { @"C:\temp\remembered.kdproj" }, RecentProjects.Paths());
    }
}
