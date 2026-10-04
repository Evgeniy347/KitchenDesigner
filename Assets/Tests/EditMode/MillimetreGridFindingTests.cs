using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>GRD-01 на СЦЕНЕ: край детали не на целом миллиметре. Парный к
/// <c>MmGridFindingTests</c> (быстрый слой, там проверяется текст и порог) — здесь
/// проверяется, что полный анализ сцены до этой находки вообще доходит и что
/// загрузка файла деталь при этом НЕ двигает.
///
/// Почему находка, а не тихая починка: до 2026-09-12 открытие проекта прогоняло
/// <c>MmGrid.Snap</c> по каждой детали (<c>SnapElementEdgesToMillimetreGrid</c>) и
/// сдвигало её до 0,5 мм без шага отмены — пользователь ничего не делал, а его
/// данные менялись, и следующее сохранение закрепляло сдвиг. Правило «загрузка
/// чинит СТЫК, а не РАЗМЕР» (conventions/SERIALIZATION.md) не выполнялось ровно
/// здесь; починка стыков (<c>RepairAutoSeatedJoints</c>) осталась, она законна.
///
/// Считается это в ПОЛНОМ анализе сцены, а не на кадре жеста: путь кадра
/// инкрементальный и дорогой, и лишний проход по всем деталям ради подсветки там
/// платить нечем.</summary>
public class MillimetreGridFindingTests
{
    private const float U = AppConstants.MM_TO_UNITS;

    private static readonly Vector3Int BoardDims = new Vector3Int(600, 18, 500);

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
    }

    /// <summary>Деталь, чей МИНИМАЛЬНЫЙ угол стоит в названных миллиметрах — тот же
    /// угол, который берёт <c>MmGrid</c>.</summary>
    private KitchenElement BoardWithItsMinCornerAt(string name, Vector3 minCornerMm)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = BoardDims;
        el.transform.position = new Vector3(
            (minCornerMm.x + BoardDims.x * 0.5f) * U,
            (minCornerMm.y + BoardDims.y * 0.5f) * U,
            (minCornerMm.z + BoardDims.z * 0.5f) * U);
        PartRegistry.Register(el);
        _spawned.Add(go);
        return el;
    }

    private static List<AnalysisIssue> Grd01() =>
        SceneAnalyzer.Analyze()
            .Where(i => i.Code == IssueCatalog.CodeOffMillimetreGrid).ToList();

    [Test]
    public void Analyze_EdgeAt124_4_YieldsExactlyOneGrd01_NamingThePartAndTheAxis()
    {
        var board = BoardWithItsMinCornerAt("OffGrid", new Vector3(124.4f, 500f, 300f));

        var found = Grd01();

        Assert.AreEqual(1, found.Count,
            "одна деталь мимо сетки — ровно одна находка, а не по одной на ось и не ни одной");
        Assert.AreSame(board, found[0].Target,
            "находка обязана указывать на деталь: по ней человек её выделяет в списке");
        Assert.AreEqual(IssueLevel.Warning, found[0].Level,
            "0,4 мм не мешают сборке стоять — они искажают спецификацию и раскрой");
        StringAssert.Contains("X 124,4", found[0].Message,
            "ось и координата грани обязаны быть в строке: без них двигать нечего");
        StringAssert.DoesNotContain("Y ", found[0].Message,
            "оси без отклонения в находке не перечисляются — иначе строка тонет в шуме");
    }

    [Test]
    public void Analyze_EdgeAt124_9997_YieldsNoGrd01()
    {
        BoardWithItsMinCornerAt("FloatTail", new Vector3(124.9997f, 500f, 300f));

        CollectionAssert.IsEmpty(Grd01(),
            "0,0003 мм — накопленный хвост float на координатах в метрах, а не сдвиг детали. "
            + "Противоположный вход: правило, которое умеет только срабатывать, не описывает "
            + "зелёного состояния, а список, жалующийся на каждую деталь, не читают вовсе");
    }

    [Test]
    public void Analyze_PartOnWholeMillimetres_YieldsNoGrd01()
    {
        BoardWithItsMinCornerAt("OnGrid", new Vector3(124f, 500f, 300f));

        CollectionAssert.IsEmpty(Grd01(),
            "деталь, стоящая гранями на целых миллиметрах, — норма");
    }

    [Test]
    public void Restore_LeavesTheOffGridPartExactlyWhereTheFileHadIt_AndReportsIt()
    {
        var board = BoardWithItsMinCornerAt("OffGrid", new Vector3(124.4f, 500f, 300f));
        var savedPosition = board.transform.position;

        var json = SaveLoadManager.Serialize(
            SaveLoadManager.CaptureScene(new List<KitchenElement> { board }));

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();

        var data = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(data);
        var objs = SaveLoadManager.RestoreScene(data!);
        _spawned.AddRange(objs);
        var restored = objs.Select(g => g.GetComponent<KitchenElement>())
            .FirstOrDefault(e => e != null && e.PartName == "OffGrid");
        Assert.IsNotNull(restored, "деталь обязана вернуться после загрузки");

        float toMm = 1f / U;
        Assert.AreEqual(savedPosition.x * toMm, restored!.transform.position.x * toMm, 1e-4f,
            "открытие файла не жест пользователя: раньше здесь деталь уезжала до 0,5 мм без "
            + "шага отмены, и следующее сохранение закрепляло сдвиг в его проекте");
        Assert.AreEqual(savedPosition.y * toMm, restored.transform.position.y * toMm, 1e-4f,
            "то же по Y");
        Assert.AreEqual(savedPosition.z * toMm, restored.transform.position.z * toMm, 1e-4f,
            "то же по Z");

        Assert.AreEqual(1, Grd01().Count,
            "а несовпадение с сеткой обязано ПОЯВИТЬСЯ в списке: тихая правка заменена на "
            + "находку, решение осталось за человеком");
    }

    /// <summary>Контроль 1 к исключению, которого НЕ БЫЛО СДЕЛАНО. Настенный прибор
    /// выглядел деталью, которой находка неисполнима: подвинь его — и посадка на стену
    /// вернёт его обратно. Разбор показал обратное: <c>WallSeating.Seat</c> правит ТОЛЬКО
    /// расстояние вдоль нормали и оставляет поперечные координаты как есть, а вдоль
    /// нормали край прибора ложится ровно на ГРАНЬ СТЕНЫ. Стена стоит на сетке — значит
    /// и край прибора на целом миллиметре, по построению. Находка исполнима, глушить её
    /// нечем и незачем.
    ///
    /// Красный здесь означает ровно одно: разбор неверен и исключение всё-таки нужно.</summary>
    [Test]
    public void Analyze_WallMixerSeatedOnAWallThatIsOnTheGrid_YieldsNoGrd01()
    {
        var wallGo = ElementFactory.CreateWall(new Vector3Int(3000, 2500, 100), "Stena",
            new Vector3(0f, 1250f * U, -50f * U));
        _spawned.Add(wallGo);

        var mixerGo = ElementFactory.CreateBathMixer(BathMixerSpec.Default, "Smesitel",
            new Vector3(0f, 1100.37f * U, 300f * U));
        _spawned.Add(mixerGo);
        var mixer = mixerGo.GetComponent<KitchenElement>();

        MmGrid.Snap(mixer);
        ((IWallMounted)mixer).SnapToWall();

        CollectionAssert.IsEmpty(Grd01(),
            "конец жеста ставит прибор на сетку поперёк стены, а посадка — вдоль неё, и"
            + " обе координаты выходят целыми: поперечные их и оставили, монтажная легла"
            + " на грань стены, которая сама на целом. Предупреждение, на которое нельзя"
            + " ответить, было бы шумом — но отвечать тут есть чем");
    }

    /// <summary>Контроль 2: дверь и окно — тоже IWallMounted, но они РЕЖУТ стену, а не
    /// стоят на её грани, габариты у них целые, и находка на них настоящая. Возьми
    /// признак «всё, что связано со стеной» — и этот тест погаснет вместе с сигналом.</summary>
    [Test]
    public void Analyze_DoorOffTheGrid_StillYieldsGrd01()
    {
        var doorGo = ElementFactory.CreateDoor(new Vector3Int(800, 2000, 100), "Dver",
            new Vector3(400.3f * U, 1000f * U, 0f));
        _spawned.Add(doorGo);

        var found = Grd01();

        Assert.AreEqual(1, found.Count, "дверь мимо сетки обязана остаться в отчёте");
        StringAssert.Contains("X", found[0].Message, "и обязана назвать ось");
    }

    /// <summary>Контроль 3: признак не должен превратиться в «всё, что касается стены».
    /// Обычная доска, придвинутая к стене вплотную, ничем на стене не держится — её
    /// позу выбрал человек, и правило с неё не снимается.</summary>
    [Test]
    public void Analyze_PlainBoardStandingAgainstAWall_StaysUnderTheRule()
    {
        var wallGo = ElementFactory.CreateWall(new Vector3Int(3000, 2500, 100), "Stena",
            new Vector3(0f, 1250f * U, -50f * U));
        _spawned.Add(wallGo);

        BoardWithItsMinCornerAt("Doska", new Vector3(124.4f, 500f, 0f));

        Assert.AreEqual(1, Grd01().Count,
            "доска у стены — не настенный прибор: стена её не держит и не пересаживает, "
            + "так что подвинуть её человек может, и находка обязана остаться");
    }
}
