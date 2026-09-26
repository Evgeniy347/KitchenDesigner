using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ProjectCreationDateRoundTripTests
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

    [Test]
    public void ResavingAnExistingProject_KeepsItsOriginalCreationDate()
    {
        _path = Path.Combine(Application.temporaryCachePath, "creation_date_roundtrip.kdproj");
        if (File.Exists(_path)) File.Delete(_path);

        Assert.IsTrue(SaveLoadManager.CreateEmptyProjectAt(_path));
        string createdAtFirstSave = ProjectFileCreatedAt.Of(_path);
        Assert.IsFalse(string.IsNullOrEmpty(createdAtFirstSave));

        var go = new GameObject("ExtraBoard");
        var e = go.AddComponent<KitchenElement>();
        e.PartName = "ExtraBoard";
        e.DimensionsMM = new Vector3Int(600, 400, 18);
        PartRegistry.Register(e);

        Assert.IsTrue(SaveLoadManager.SaveToPath(_path));
        string createdAtSecondSave = ProjectFileCreatedAt.Of(_path);

        Assert.AreEqual(createdAtFirstSave, createdAtSecondSave,
            "дата СОЗДАНИЯ не должна меняться от простого пересохранения");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void LoadingAFileWithoutACreationDate_KeepsCreatedAtEmpty_InsteadOfBakingTheFileSystemDate()
    {
        _path = Path.Combine(Application.temporaryCachePath, "creation_date_legacy.json");
        File.WriteAllText(_path, "{\"version\":1,\"appVersion\":\"0.1\",\"elements\":[]}");

        Assert.IsTrue(SaveLoadManager.LoadFromPath(_path));
        Assert.IsTrue(string.IsNullOrEmpty(ProjectCreationDate.Value),
            "test-results/review-persistence.md #2: NTFS-время создания файла - это дата "
            + "копирования/скачивания, а не дата СОЗДАНИЯ проекта; для файла без своего "
            + "createdAtUtc поле обязано остаться пустым в данных (UI вправе показать дату "
            + "файла отдельной подписью - ProjectFileCreatedAt.FallbackFromFileSystemUtc "
            + "остаётся для этого в RecentProjectRowSource), но печь её в createdAtUtc нельзя");
    }

    [Test]
    public void ResavingAFileWithoutACreationDate_KeepsCreatedAtEmpty_OnDisk()
    {
        _path = Path.Combine(Application.temporaryCachePath, "creation_date_legacy_resave.json");
        File.WriteAllText(_path, "{\"version\":1,\"appVersion\":\"0.1\",\"elements\":[]}");

        Assert.IsTrue(SaveLoadManager.LoadFromPath(_path));
        Assert.IsTrue(SaveLoadManager.SaveToPath(_path));

        Assert.IsTrue(string.IsNullOrEmpty(ProjectFileCreatedAt.Of(_path)),
            "пересохранение старого файла без даты создания не имеет права записать в него "
            + "НИКАКУЮ дату создания - ни из файловой системы, ни из момента открытия");
    }
}
