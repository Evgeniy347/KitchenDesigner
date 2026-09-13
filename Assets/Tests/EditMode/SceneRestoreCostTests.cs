using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Цена `rebuildScene` — это теперь ВЕСЬ `load_project` (21,85 мс из 21,85),
/// но мерена она на проекте в ОДНУ деталь. На проекте пользователя в 411 деталей её не
/// мерил никто, и именно поэтому здесь сначала ПРИБОР, а не починка.
///
/// Вопрос ровно один и он про форму кривой, а не про миллисекунды: платит ли
/// восстановление ЗА ДЕТАЛЬ то, что считается ПО ВСЕЙ СЦЕНЕ. Сегодня это находилось уже
/// трижды — `EdgeSubstrate.Sync`, `MembersOf`, `FindAttachedWall`, — и у всех троих
/// след одинаковый: число обходов сцены растёт вместе с числом деталей. Счётчик
/// `SceneScanCounter` монотонен и называет обходивших поимённо, поэтому он и отвечает.
///
/// Тест печатает замеры с маркером `[RestoreCost]` — их можно выгрести из лога прогона
/// грепом, не читая его целиком (`AGENTS.md` → «Output discipline»).</summary>
public class SceneRestoreCostTests
{
    private const int Few = 10;
    private const int Many = 60;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private void Make(string name)
    {
        var go = new GameObject(name);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = new Vector3Int(800, 400, 18);
        PartRegistry.Register(e);
        _spawned.Add(go);
    }

    private long ScansToRestore(int elementCount, out string scanners)
    {
        PartRegistry.Clear();
        for (int i = 0; i < elementCount; i++) Make($"Board_{elementCount}_{i}");
        var data = SaveLoadManager.CaptureScene(PartRegistry.GetAll());

        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();

        long mark = SceneScanCounter.Scans;
        var created = SaveLoadManager.RestoreScene(data);
        long scans = SceneScanCounter.Scans - mark;
        scanners = SceneScanCounter.Since(mark);

        foreach (var go in created) if (go != null) _spawned.Add(go);
        return scans;
    }

    [Test]
    public void RestoringAScene_ScansIt_AFixedNumberOfTimes_NotOncePerElement()
    {
        long few = ScansToRestore(Few, out string fewScanners);
        long many = ScansToRestore(Many, out string manyScanners);

        TestContext.WriteLine($"[RestoreCost] деталей={Few} обходов={few} [{fewScanners}]");
        TestContext.WriteLine($"[RestoreCost] деталей={Many} обходов={many} [{manyScanners}]");

        long grewBy = many - few;
        long elementsAdded = Many - Few;

        Assert.Less(grewBy, elementsAdded,
            $"обходов сцены на {Few} деталей — {few}, на {Many} — {many}; рост {grewBy} "
            + $"на {elementsAdded} добавленных деталей. Рост, идущий вровень с числом "
            + "деталей, и есть «за деталь считаем по всей сцене» — на 411 деталях это "
            + $"квадрат. Кто обходит: [{manyScanners}]");
    }

    [Test]
    public void RestoringTwiceAsMuch_DoesNotScanTwiceAsOften()
    {
        long few = ScansToRestore(Few, out _);
        long many = ScansToRestore(Many, out string manyScanners);

        Assert.LessOrEqual(many, few * 2,
            $"{Few} деталей — {few} обходов, {Many} деталей — {many}. Шестикратный рост "
            + "сцены не смеет давать кратный рост обходов: обход стоит 0,7–2,2 МБ мусора "
            + $"на сцене пользователя. Кто обходит: [{manyScanners}]");
    }
}
