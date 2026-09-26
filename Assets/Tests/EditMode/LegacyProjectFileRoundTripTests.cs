using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Требование задачи: старые пользовательские проекты обязаны грузиться БЕЗ сдвига
/// координат, несмотря на новые аддитивные поля (createdAtUtc, версия проекта). Фикстура
/// — замороженная копия реальной сцены (`pillar-beside-plinth.save.json`, без appVersion и
/// createdAtUtc — ровно то, что лежит на диске у пользователя со старой версией
/// приложения), а не docs/example.save.json: правило agents/TESTS.md → «NEVER TOUCH IT»
/// и «Корректный фикс для теста — ЗАМОРОЖЕННАЯ копия сцены».
/// </summary>
public class LegacyProjectFileRoundTripTests
{
    private const string FixtureName = "pillar-beside-plinth.save.json";
    private string? _prevLastPath;
    private string[]? _recentBackup;

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
    }

    private static string FixturePath() =>
        Path.Combine(Application.dataPath, "Tests", "EditMode", "Fixtures", FixtureName);

    [Test]
    public void LoadingALegacyFile_PlacesEveryElement_AtExactlyItsStoredPosition()
    {
        string json = File.ReadAllText(FixturePath());
        var original = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(original);
        Assert.AreEqual("", original!.appVersion, "фикстура обязана оставаться СТАРОГО формата - без appVersion");
        Assert.AreEqual("", original.createdAtUtc, "и без даты создания - иначе тест ничего не проверяет");

        Assert.IsTrue(SaveLoadManager.LoadFromPath(FixturePath()));

        var restored = SceneElements.All();
        var byName = new System.Collections.Generic.Dictionary<string, KitchenElement>();
        foreach (var e in restored)
            if (e != null) byName[e.PartName] = e;

        foreach (var ed in original.elements)
        {
            Assert.IsTrue(byName.TryGetValue(ed.name, out var live),
                $"элемент {ed.name} из фикстуры обязан появиться в сцене");
            Assert.AreEqual(ed.position[0], live.transform.position.x, 1e-6f, ed.name + ".x");
            Assert.AreEqual(ed.position[1], live.transform.position.y, 1e-6f, ed.name + ".y");
            Assert.AreEqual(ed.position[2], live.transform.position.z, 1e-6f, ed.name + ".z");
            Assert.AreEqual(ed.rotation[0], live.transform.rotation.x, 1e-6f, ed.name + ".rot.x");
            Assert.AreEqual(ed.rotation[1], live.transform.rotation.y, 1e-6f, ed.name + ".rot.y");
            Assert.AreEqual(ed.rotation[2], live.transform.rotation.z, 1e-6f, ed.name + ".rot.z");
            Assert.AreEqual(ed.rotation[3], live.transform.rotation.w, 1e-6f, ed.name + ".rot.w");
        }
    }

    [Test]
    public void ResavingALegacyFile_KeepsEveryCoordinate_AndOnlyAddsTheNewFields()
    {
        string json = File.ReadAllText(FixturePath());
        var original = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(original);

        Assert.IsTrue(SaveLoadManager.LoadFromPath(FixturePath()));

        string resavedJson = SaveLoadManager.CaptureCurrentJson();
        var resaved = SaveLoadManager.Deserialize(resavedJson);
        Assert.IsNotNull(resaved);

        var byName = new System.Collections.Generic.Dictionary<string, ElementData>();
        foreach (var ed in resaved!.elements)
            if (ed != null) byName[ed.name] = ed;

        foreach (var before in original!.elements)
        {
            Assert.IsTrue(byName.TryGetValue(before.name, out var after),
                $"элемент {before.name} обязан пережить пересохранение");
            Assert.AreEqual(before.position[0], after.position[0], 1e-6f, before.name + ".x");
            Assert.AreEqual(before.position[1], after.position[1], 1e-6f, before.name + ".y");
            Assert.AreEqual(before.position[2], after.position[2], 1e-6f, before.name + ".z");
            Assert.AreEqual(before.dimensionsMM[0], after.dimensionsMM[0], before.name + ".w");
            Assert.AreEqual(before.dimensionsMM[1], after.dimensionsMM[1], before.name + ".h");
            Assert.AreEqual(before.dimensionsMM[2], after.dimensionsMM[2], before.name + ".d");
        }

        Assert.IsFalse(string.IsNullOrEmpty(resaved.appVersion),
            "новое поле версии обязано появиться при пересохранении - оно аддитивное");
        Assert.IsFalse(string.IsNullOrEmpty(resaved.createdAtUtc),
            "и дата создания - откуда бы взяться координатному сдвигу, эти поля к нему не имеют отношения");
    }
}
