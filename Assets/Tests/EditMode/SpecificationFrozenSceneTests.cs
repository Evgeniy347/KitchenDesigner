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
/// «Стена не вернулась листовой деталью» спрашивается по соседнему компоненту
/// <see cref="Wall"/>, а не по высоте — подробности у
/// <see cref="AssertNoWallCameBackAsABoard"/>. А что отчитаться есть чем у КАЖДОЙ стены, а не
/// у их суммы, стережёт отдельный тест: сумма по 44 стенам одной строкой скрывает потерю.
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

        AssertNoWallCameBackAsABoard(elements, result);

        Assert.Greater(result.totalsByUnit.Count, 1,
            "единиц больше одной: м² у досок, погонные метры у трубы и кромки, штуки у фитингов");
        Assert.AreEqual(result.totalAreaM2, result.totalsByUnit[SpecUnit.AreaM2], 0.0001f,
            "итог по м² в totalsByUnit обязан совпасть со старым totalAreaM2 — это одна и та же величина");

        Snapshot.Match(SpecificationExport.ToCsv(result), "spec_frozen_pipe_gap_scene");
    }

    /// <summary>Стена отличается от доски НЕ высотой, а соседним компонентом
    /// <see cref="Wall"/> — тем самым источником, по которому маршрутизирует и продукт
    /// (<c>IsFlatBoardElement</c> отсекает всё, у чего есть такой сосед). Первая редакция
    /// этой проверки спрашивала «нет ли листовой строки высотой 2 700 мм» и краснела на
    /// ЗАКОННОЙ детали: <c>Cab_L_Side_L/R</c>, 600×2700×16 — бока высокого шкафа, 1,62 м²
    /// ЛДСП каждый, которые стоят в эталоне ещё с тех времён, когда стен в ведомости не
    /// было вовсе. Высота 2 700 мм у листа заказуема и проверяема; отличительный признак
    /// кладки — не она.
    ///
    /// Спрашивается сразу по двум колонкам одной строки, и это не дубль: имя листовой
    /// строки — это <c>PartName</c> ПЕРВОГО элемента группы, поэтому стена, слившаяся в
    /// группу с уже существующей доской, ушла бы под чужим именем и проверка по имени её
    /// бы не увидела; а габарит группы — ключ группировки, и он совпадёт с габаритом
    /// стены. Каждая закрывает слепое пятно другой.
    ///
    /// Разделение труда с сенсором
    /// <see cref="EveryWallOfTheFrozenScene_HasSomethingToReportInTheSpecification"/>: здесь
    /// стена опознаётся ПО КОМПОНЕНТУ, поэтому проверка краснеет, когда стена с компонентом
    /// уходит листовым маршрутом (например, у <c>Wall</c> отобрали <see cref="IQuantifies"/>
    /// или в <c>Taken</c> переставили порядок). Обратный случай — стена БЕЗ компонента, для
    /// которой <c>IsFlatBoardElement</c> честно истинен, — этой проверке не виден вообще, и
    /// его ловит сенсор, сверяя число стен в файле с числом компонентов в сцене.</summary>
    private static void AssertNoWallCameBackAsABoard(List<KitchenElement> elements, SpecResult result)
    {
        var walls = elements.Where(IsWall).ToList();
        var wallNames = new HashSet<string>(walls.Select(w => w.PartName), System.StringComparer.Ordinal);
        var wallDims = new HashSet<Vector3Int>(walls.Select(w => w.DimensionsMM));

        var boards = result.lines
            .Where(l => l.isBoardArea && (wallNames.Contains(l.name) || wallDims.Contains(l.dimensionsMM)))
            .Select(l => $"{l.name} {Dims(l.dimensionsMM)} ×{l.count} = {l.totalAreaM2:F4} м²")
            .ToList();

        Assert.IsEmpty(boards,
            "стена не имеет права вернуться в ведомость листовой деталью: «площадь ЛДСП» на "
            + "кладку — цифра, которую нельзя ни заказать, ни проверить. Строка опознана как "
            + "стена по имени или по габариту одного из "
            + $"{walls.Count} элементов сцены с соседним компонентом Wall: "
            + string.Join("; ", boards));
    }

    private static bool IsWall(KitchenElement element) =>
        element != null && element.GetComponent<Wall>() != null;

    private static string Dims(Vector3Int dims) => $"{dims.x}×{dims.y}×{dims.z}";

    /// <summary>Сенсор «у каждой стены сцены есть чем отчитаться». Золотой CSV на этот
    /// вопрос не отвечает: он видит СУММУ по 44 стенам одной строкой, и стена, потерявшая
    /// счётчик, утонет в ней — пользователь увидит «часть стен в смете есть, часть нет», а
    /// эталон разойдётся на числа, которые никто не сможет истолковать. Поэтому спрашивается
    /// каждая стена отдельно, и у ПРОДУКТА функции, а не у наличия интерфейса
    /// (agents/TEST-DESIGN.md → «Сторож, спрашивающий „объявлен ли интерфейс“, не спрашивает
    /// ничего»): настоящий <see cref="SpecificationManager.Build"/> на ОДНОЙ этой стене
    /// обязан дать хотя бы одну строку раздела «Стены».
    ///
    /// Само «сколько в сцене стен» берётся не из счётчика компонентов — иначе стена, которой
    /// восстановление не навесило <see cref="Wall"/>, перестала бы считаться стеной и сенсор
    /// её бы не искал. Оно берётся из ФАЙЛА: сколько элементов объявлено стенами там, столько
    /// объектов с компонентом обязано оказаться в сцене.
    ///
    /// И маршрут обязан быть ИСКЛЮЧИТЕЛЬНО <see cref="SpecRoute.Quantifies"/>: стена, у
    /// которой истинен ещё и листовой маршрут, прошла бы <c>Build</c> молча (первый маршрут
    /// выигрывает и делает <c>continue</c>), а в ведомости не было бы ни площади, ни кладки —
    /// смотря какой из двух окажется первым в порядке.</summary>
    [Test]
    public void EveryWallOfTheFrozenScene_HasSomethingToReportInTheSpecification()
    {
        var elements = RestoreScene();

        int declaredInFile = CountOccurrences(_json, "\"isWall\": true");
        Assert.AreEqual(44, declaredInFile,
            "фикстура заморожена: если число объявленных стен в файле сдвинулось, файл подменили");

        var walls = elements.Where(IsWall).ToList();
        Assert.AreEqual(declaredInFile, walls.Count,
            $"файл объявляет {declaredInFile} стен, а компонент Wall оказался на {walls.Count} "
            + "объектах — восстановление навесило счётчик не на все стены, и разница уйдёт из "
            + "ведомости молча");

        var mute = new List<string>();
        var wrongRoute = new List<string>();

        foreach (var wall in walls)
        {
            var declared = ElementSpecCoverage.Declared(
                wall.GetComponents<IQuantifies>().Length > 0,
                wall is ISpecificationParts,
                wall.IsFlatBoardElement);
            if (declared != SpecRoute.Quantifies)
                wrongRoute.Add($"{wall.PartName}: объявлено {declared}, "
                    + $"Build берёт {ElementSpecCoverage.Taken(declared)}, "
                    + $"мёртв {ElementSpecCoverage.Dead(declared)}");

            var own = SpecificationManager.Build(new[] { wall });
            if (!own.lines.Any(l => l.section == SpecSections.Walls))
                mute.Add($"{wall.PartName} {Dims(wall.DimensionsMM)} "
                    + $"({own.lines.Count} стр. вне раздела «Стены»)");
        }

        Assert.IsEmpty(wrongRoute,
            "стена обязана попадать в ведомость ровно одним маршрутом — соседним компонентом "
            + "Wall через IQuantifies. Второй объявленный маршрут мёртв, и его строки не "
            + "появятся никогда: " + string.Join("; ", wrongRoute));

        Assert.IsEmpty(mute,
            $"из {walls.Count} стен сцены этим нечем отчитаться: настоящий Build на одной "
            + "такой стене не дал НИ ОДНОЙ строки раздела «Стены». Пользователь увидит это "
            + "как «часть стен в смете есть, часть нет», а сумма в золотом CSV сдвинется на "
            + "число, которое нельзя истолковать: " + string.Join("; ", mute));
    }

    private static int CountOccurrences(string text, string needle)
    {
        int count = 0;
        for (int i = text.IndexOf(needle, System.StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(needle, i + needle.Length, System.StringComparison.Ordinal))
            count++;
        return count;
    }
}
