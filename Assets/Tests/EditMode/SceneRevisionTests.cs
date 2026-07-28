using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Ревизия сцены — замена покадровому пересчёту: системы работают только когда версия
/// сдвинулась. Тесты сторожат ровно то, из-за чего механизм может подвести молча:
/// пропущенный бамп (UI останется устаревшим) и лишний бамп (тяжёлый пересчёт вернётся
/// в каждый кадр).
/// </summary>
public class SceneRevisionTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make()
    {
        var go = new GameObject("E");
        var e = go.AddComponent<KitchenElement>();
        _spawned.Add(go);
        return e;
    }

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        SceneRevision.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        PartRegistry.Clear();
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        SceneRevision.Reset();
    }

    [Test]
    public void Register_And_Unregister_BumpVersion()
    {
        var e = Make();          // Awake сам регистрирует элемент
        int afterRegister = SceneRevision.Version;
        Assert.Greater(afterRegister, 0, "Появление детали не сдвинуло ревизию");

        PartRegistry.Unregister(e);
        Assert.Greater(SceneRevision.Version, afterRegister, "Удаление детали не сдвинуло ревизию");
    }

    [Test]
    public void Unregister_Unknown_DoesNotBump()
    {
        Make();
        int before = SceneRevision.Version;
        var stranger = new GameObject("stranger").AddComponent<KitchenElement>();
        PartRegistry.Unregister(stranger);   // Awake уже снял его через Register/Unregister
        PartRegistry.Unregister(stranger);   // второй раз — реестр не менялся
        Object.DestroyImmediate(stranger.gameObject);

        Assert.AreEqual(before + 2, SceneRevision.Version,
            "Учтены должны быть только настоящие изменения реестра: регистрация в Awake и первое снятие");
    }

    [Test]
    public void Changed_ReportsOnce_PerBump()
    {
        int seen = -1;
        Assert.IsTrue(SceneRevision.Changed(ref seen), "Первый опрос обязан сообщить об изменении");
        Assert.IsFalse(SceneRevision.Changed(ref seen), "Без изменений повторный опрос должен молчать");

        SceneRevision.Bump();
        Assert.IsTrue(SceneRevision.Changed(ref seen));
        Assert.IsFalse(SceneRevision.Changed(ref seen));
    }

    [Test]
    public void Tracker_MovedElement_BumpsPoseVersionAndRevision()
    {
        var e = Make();
        SceneChangeTracker.Poll();      // погасить hasChanged после создания

        int poseBefore = e.PoseVersion;
        int revisionBefore = SceneRevision.Version;

        e.transform.position += Vector3.right;
        SceneChangeTracker.Poll();

        Assert.AreEqual(poseBefore + 1, e.PoseVersion, "Сдвиг детали не поднял её PoseVersion");
        Assert.Greater(SceneRevision.Version, revisionBefore, "Сдвиг детали не сдвинул ревизию сцены");
    }

    [Test]
    public void Tracker_NothingMoved_DoesNotBump()
    {
        var e = Make();
        SceneChangeTracker.Poll();

        int poseBefore = e.PoseVersion;
        int revisionBefore = SceneRevision.Version;

        SceneChangeTracker.Poll();
        SceneChangeTracker.Poll();

        Assert.AreEqual(poseBefore, e.PoseVersion, "Неподвижная деталь не должна поднимать PoseVersion");
        Assert.AreEqual(revisionBefore, SceneRevision.Version,
            "Спокойная сцена не должна двигать ревизию — иначе тяжёлый пересчёт вернётся в каждый кадр");
    }

    [Test]
    public void Tracker_Resize_BumpsPoseVersion()
    {
        var e = Make();
        SceneChangeTracker.Poll();
        int poseBefore = e.PoseVersion;

        // Ресайз идёт через localScale, то есть тоже через трансформ.
        e.DimensionsMM = new Vector3Int(600, 720, 18);
        e.ApplyDimensions();
        SceneChangeTracker.Poll();

        Assert.AreEqual(poseBefore + 1, e.PoseVersion, "Изменение габаритов не поднялоPoseVersion");
    }
}
