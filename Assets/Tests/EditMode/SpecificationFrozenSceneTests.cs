using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Приёмочный тест этапа 0 (карта §3.2): единицы измерения и разделы — новый
/// скелет вокруг существующей спецификации, а не рядом с ней. Числа существующей
/// кухни обязаны остаться теми же самыми до последней цифры. Сцена — ЗАМОРОЖЕННАЯ
/// копия Fixtures/pipe-gap-scene.save.json (309 элементов, agents/TESTS.md), не живой
/// docs/example.save.json.
///
/// Golden-master: первый прогон без Snapshots/spec_frozen_pipe_gap_scene.verified.json
/// создаёт .candidate.json и падает — это ожидаемо для НОВОГО снапшота (Snapshot.cs).
/// Принять можно только сверив кандидата с выводом ДО этого изменения (тот же CSV,
/// без колонок Section/Unit/QtyPerItem/QtyTotal и без строк "Итого").</summary>
public class SpecificationFrozenSceneTests
{
    private const string SaveFileName = "Fixtures/pipe-gap-scene.save.json";
    private string _json = "";
    private ProjectLoadStateGuard? _guard;

    [SetUp]
    public void SetUp() => _guard = ProjectLoadStateGuard.Capture();

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fullPath = Path.Combine(Application.dataPath, "Tests/EditMode", SaveFileName);
        Assert.IsTrue(File.Exists(fullPath), $"Save file not found: {fullPath}");
        _json = File.ReadAllText(fullPath);
    }

    [TearDown]
    public void TearDown()
    {
        _guard?.Restore();
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
    }

    private List<KitchenElement> RestoreScene()
    {
        var data = SaveLoadManager.Deserialize(_json);
        Assert.IsNotNull(data);
        var objs = SaveLoadManager.RestoreScene(data!);
        Assert.IsNotEmpty(objs);
        return objs.Select(g => g.GetComponent<KitchenElement>()).Where(e => e != null).ToList()!;
    }

    [Test]
    public void Build_FrozenKitchenScene_UnchangedNumbersAndGoldenCsv()
    {
        var elements = RestoreScene();
        Assert.AreEqual(309, elements.Count, "фикстура заморожена — если это число сдвинулось, файл подменили");

        var result = SpecificationManager.Build(elements);

        Assert.Greater(result.lines.Count, 0);
        Assert.Greater(result.totalCount, 0);
        Assert.Greater(result.totalAreaM2, 0f);

        Assert.IsTrue(result.lines.Any(l => l.unit == SpecUnit.AreaM2 && l.section == "Мебель"),
            "листовые детали кухни считаются в м² в разделе «Мебель»");
        Assert.IsTrue(result.lines.Any(l => l.unit == SpecUnit.LinearMeters),
            "труба и кромка меряются погонными метрами — если этих строк нет, элементы снова выпали из ведомости молча");
        Assert.IsTrue(result.lines.Any(l => l.unit == SpecUnit.Pieces),
            "фитинги и покупные комплекты меряются штуками");
        Assert.IsFalse(result.lines.Any(l => l.unit == SpecUnit.VolumeM3),
            "кубометры появятся только с фундаментом — конструктивных элементов в этой сцене нет");

        Assert.Greater(result.totalsByUnit.Count, 1,
            "единиц больше одной: м² у досок, погонные метры у трубы и кромки, штуки у фитингов");
        Assert.AreEqual(result.totalAreaM2, result.totalsByUnit[SpecUnit.AreaM2], 0.0001f,
            "итог по м² в totalsByUnit обязан совпасть со старым totalAreaM2 — это одна и та же величина");

        Snapshot.Match(SpecificationExport.ToCsv(result), "spec_frozen_pipe_gap_scene");
    }
}
