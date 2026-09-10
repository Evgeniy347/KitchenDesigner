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
/// без колонок Section/Unit/QtyPerItem/QtyTotal и без строк "Итого").
///
/// Эталон сдвигался один раз законно — когда стена стала <see cref="IQuantifies"/>
/// (d58fb3ca). До него стена не давала НИ ОДНОЙ строки: <c>IsFlatBoardElement</c>
/// отсекает всё, у чего есть сосед <c>Wall</c>, поэтому 44 стены этой сцены выпадали
/// из ведомости молча. Поэтому дифф чисто ДОБАВОЧНЫЙ: две строки раздела «Стены»
/// (кирпич в шт, раствор в м³) и два «Итого» к ним; «Total», м² и погонные метры
/// не сдвинулись ни на цифру — кладка не листовая деталь и в totalAreaM2 не попадает.
///
/// Оговорка про проёмы, важная при чтении чисел: <c>WallOpeningElement.SnapToWall</c>
/// зовётся из <c>Start()</c>, а EditMode колбэков не запускает — значит ни одно окно и
/// ни одна дверь этой фикстуры к стене не привязаны, и кладка считается по ПОЛНОМУ
/// объёму, без вычета проёмов. Вычет проёмов пинается отдельно, на пуре
/// (WallSpecItemsTests), где вход задаётся руками.</summary>
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
        // Раздел «Стены» — то, что изменилось законно (d58fb3ca): 44 стены сцены больше не
        // выпадают из ведомости молча, а отчитываются кирпичом и раствором. Числа ниже
        // выведены из формулы, а не списаны со снапшота, и арифметика записана в сообщениях:
        // одна стена — один вход в WallQuantities, все 44 складываются в две строки.
        var bricks = result.lines.Single(l => l.section == SpecSections.Walls
            && l.unit == SpecUnit.Pieces);
        Assert.AreEqual(44, bricks.count,
            "все стены сцены складываются в ОДНУ строку кирпича: ключ группировки — раздел, "
            + "имя формата, единица, материал и габарит КАМНЯ, и ни одного признака стены");
        Assert.AreEqual(new Vector3Int(250, 65, 120), bricks.dimensionsMM,
            "в колонках Ш|В|Г стоит формат камня (кирпич 250×120×65, ГОСТ 530-2012), "
            + "а не габарит стены");
        Assert.AreEqual(15659f, bricks.qtyTotal, 0.5f,
            "сумма по 44 стенам при кладке по умолчанию (кирпич одинарный, шов 10 мм, запас 5 %). "
            + "Пример — самая длинная стена W250_21, 7505×2700×250: объём 7,505·2,700·0,250 = "
            + "5,065875 м³, кирпич со швом 0,260·0,075·0,130 = 0,002535 м³, уложено "
            + "ceil(5,065875 / 0,002535) = ceil(1998,37) = 1999 шт, закупить "
            + "ceil(1999·1,05) = ceil(2098,95) = 2099 шт");
        Assert.AreEqual(8.6631f, result.lines.Single(l => l.section == SpecSections.Walls
                && l.unit == SpecUnit.VolumeM3).qtyTotal, 0.0005f,
            "раствор — разница между объёмом кладки и объёмом самих камней: по всей сцене "
            + "37,71225 м³ кладки минус 14 897 уложенных кирпичей по 0,00195 м³ "
            + "(0,250·0,065·0,120) = 8,6631 м³. Для W250_21 это 5,065875 − 1999·0,00195 = "
            + "1,167825 м³");

        Assert.IsFalse(result.lines.Any(l => l.isBoardArea && l.dimensionsMM.y == 2700),
            "стена не имеет права вернуться в ведомость листовой деталью: «площадь ЛДСП» на "
            + "кладку высотой 2 700 мм — цифра, которую нельзя ни заказать, ни проверить");

        Assert.Greater(result.totalsByUnit.Count, 1,
            "единиц больше одной: м² у досок, погонные метры у трубы и кромки, штуки у фитингов");
        Assert.AreEqual(result.totalAreaM2, result.totalsByUnit[SpecUnit.AreaM2], 0.0001f,
            "итог по м² в totalsByUnit обязан совпасть со старым totalAreaM2 — это одна и та же величина");

        Snapshot.Match(SpecificationExport.ToCsv(result), "spec_frozen_pipe_gap_scene");
    }
}
