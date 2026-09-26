using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class CreateEmptyProjectAtTests
{
    private string? _prevLastPath;
    private string[]? _recentBackup;
    private string _path = "";

    [SetUp]
    public void Setup()
    {
        _prevLastPath = SaveLoadManager.LastPath;
        _recentBackup = RecentProjectsTestBackup.Capture();
    }

    [TearDown]
    public void TearDown()
    {
        SaveLoadManager.LastPath = _prevLastPath!;
        RecentProjectsTestBackup.Restore(_recentBackup!);
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        if (!string.IsNullOrEmpty(_path) && File.Exists(_path)) File.Delete(_path);
    }

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        return e;
    }

    [Test]
    public void CreateEmptyProjectAt_WritesAFile_AndSetsItAsTheLastPath()
    {
        _path = Path.Combine(Application.temporaryCachePath, "new_empty_project.kdproj");
        if (File.Exists(_path)) File.Delete(_path);

        Assert.IsTrue(SaveLoadManager.CreateEmptyProjectAt(_path));

        Assert.IsTrue(File.Exists(_path));
        Assert.AreEqual(_path, SaveLoadManager.LastPath);
    }

    [Test]
    public void CreateEmptyProjectAt_AddsThePath_ToRecentProjects()
    {
        _path = Path.Combine(Application.temporaryCachePath, "new_empty_project_recent.kdproj");
        if (File.Exists(_path)) File.Delete(_path);
        RecentProjectsTestBackup.Restore(new string[0]);

        Assert.IsTrue(SaveLoadManager.CreateEmptyProjectAt(_path));

        CollectionAssert.Contains(RecentProjects.Paths(), _path,
            "«Новый проект» обязан появиться в «Загрузить» так же, как любой другой открытый проект");
    }

    [Test]
    public void CreateEmptyProjectAt_ClearsExistingBoards_ButKeepsTheBasePlate()
    {
        var plate = Make("BasePlate", new Vector3Int(3000, 18, 3000), Vector3.zero);
        plate.gameObject.AddComponent<BasePlate>();
        Make("SomeBoard", new Vector3Int(800, 400, 18), new Vector3(1, 0, 0));

        _path = Path.Combine(Application.temporaryCachePath, "new_empty_project_clears.kdproj");
        if (File.Exists(_path)) File.Delete(_path);

        Assert.IsTrue(SaveLoadManager.CreateEmptyProjectAt(_path));

        var remaining = Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None);
        foreach (var e in remaining)
            Assert.AreNotEqual("SomeBoard", e.PartName, "обычная доска обязана быть удалена");
        Assert.IsTrue(plate != null && plate.gameObject != null, "базовая плита остаётся");
    }

    [Test]
    public void CreateEmptyProjectAt_StampsAFreshCreationDate_NotTheOldOne()
    {
        _path = Path.Combine(Application.temporaryCachePath, "new_empty_project_date.kdproj");
        if (File.Exists(_path)) File.Delete(_path);

        ProjectCreationDate.Value = "2000-01-01T00:00:00Z";
        Assert.IsTrue(SaveLoadManager.CreateEmptyProjectAt(_path));

        Assert.AreNotEqual("2000-01-01T00:00:00Z", ProjectCreationDate.Value);
        Assert.IsFalse(string.IsNullOrEmpty(ProjectFileCreatedAt.Of(_path)));
    }
}
