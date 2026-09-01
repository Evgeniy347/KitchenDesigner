using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ValidationAdapterContractTests
{
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
    }

    private KitchenElement Part(string name, Vector3Int dims, Vector3 position)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        go.transform.position = position;
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = dims;
        return element;
    }

    private static readonly Vector3Int BoardDims = new Vector3Int(800, 400, 18);

    private KitchenElement Floor() =>
        Part("Floor", new Vector3Int(3000, 18, 3000), new Vector3(0f, -0.009f, 0f));

    private const float TooSmallGapMm = 1f;

    private const float NearContactMinGapMm = 2f;

    private const float NearContactMaxGapMm = 4f;

    private KitchenElement TwinOneMillimetreInFrontOf(KitchenElement neighbour, string name)
    {
        var box = neighbour.ToGeometry();
        var dims = neighbour.DimensionsMM;
        float halfDepth = dims.z * 0.5f * AppConstants.MM_TO_UNITS;
        float centreX = (box.Min.x + box.Max.x) * 0.5f;
        float centreY = (box.Min.y + box.Max.y) * 0.5f;
        float gap = TooSmallGapMm * AppConstants.MM_TO_UNITS;
        return Part(name, dims, new Vector3(centreX, centreY, box.Max.z + gap + halfDepth));
    }

    private KitchenElement BoardOnFloor(string name) =>
        Part(name, BoardDims, new Vector3(0f, 0.2f, 0f));

    [Test]
    public void Validate_ValidScene_LeavesDiagnosticsNull_ButFillsThemWhenSomethingIsWrong()
    {
        var floor = Floor();
        var board = BoardOnFloor("Board");

        var valid = ConstraintValidator.Validate(new List<KitchenElement> { floor, board });
        Assume.That(valid.isValid, Is.True, "сцена «деталь на полу» обязана быть валидной");
        Assert.IsNull(valid.diagnostics,
            "на валидной сцене список причин остаётся НЕ созданным: Validate идёт каждый "
            + "кадр перетаскивания, и пустой List на кадр — это мусор в GC на ровном месте");

        var lonely = Part("Lonely", BoardDims, new Vector3(5f, 5f, 5f));
        var broken = ConstraintValidator.Validate(new List<KitchenElement> { floor, board, lonely });
        Assert.IsNotNull(broken.diagnostics,
            "ленивая инициализация не должна съесть сами причины: как только нарушение "
            + "есть, список обязан появиться");
    }

    [Test]
    public void Diagnostics_NameThePartnerOfAnOverlap_AndLeaveItNullForAnUnsupportedPart()
    {
        var floor = Floor();
        var a = BoardOnFloor("A");
        var b = Part("B", BoardDims, new Vector3(0.1f, 0.2f, 0f));

        var overlap = ConstraintValidator.Validate(new List<KitchenElement> { floor, a, b });
        var overlapDiag = overlap.diagnostics!.Find(d => d.kind == ViolationKind.Overlap);
        Assert.IsNotNull(overlapDiag.element, "пересечение обязано попасть в причины");
        Assert.IsNotNull(overlapDiag.other,
            "у пересечения ВСЕГДА есть вторая деталь — окно ошибок показывает пару, "
            + "и без партнёра сообщение «пересекается с ...» договорить нечем");

        var lonely = Part("Lonely", BoardDims, new Vector3(5f, 5f, 5f));
        var unsupported = ConstraintValidator.Validate(new List<KitchenElement> { floor, a, lonely });
        var unsupportedDiag = unsupported.diagnostics!.Find(d => d.kind == ViolationKind.Unsupported);
        Assert.IsNull(unsupportedDiag.other,
            "у «висит в воздухе» партнёра нет по смыслу: нарушает одна деталь, и "
            + "второе поле остаётся null");
    }

    [Test]
    public void Validate_AfterABrokenScene_ReportsTheNextSceneCleanly()
    {
        var floor = Floor();
        var board = BoardOnFloor("Board");
        var lonely = Part("Lonely", BoardDims, new Vector3(5f, 5f, 5f));

        var broken = ConstraintValidator.Validate(new List<KitchenElement> { floor, board, lonely });
        Assume.That(broken.isValid, Is.False, "первая сцена намеренно с нарушением");

        var clean = ConstraintValidator.Validate(new List<KitchenElement> { floor, board });

        Assert.IsTrue(clean.isValid,
            "скратч (списки деталей, снимков и результат ядра) переиспользуется между "
            + "вызовами ради горячего пути перетаскивания — но результат ПРЕДЫДУЩЕЙ "
            + "сцены не имеет права протечь в следующую");
        Assert.IsEmpty(clean.violations, "нарушения прошлой сцены не переносятся");
        Assert.IsNull(clean.diagnostics, "и причины прошлой сцены тоже");
    }

    [Test]
    public void HasViolationNear_MeasuresByBounds_NotByCentres()
    {
        var floor = Floor();
        var board = BoardOnFloor("Board");
        var lonely = Part("Lonely", BoardDims, new Vector3(2f, 0f, 0f));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { floor, board, lonely });
        Assume.That(result.violations, Does.Contain(lonely), "одинокая деталь — нарушение");

        float boundsGap = lonely.ToGeometry().Min.x - board.ToGeometry().Max.x;
        float centreGap = lonely.transform.position.x - board.transform.position.x;
        Assume.That(boundsGap, Is.LessThan(centreGap),
            "габариты ближе центров — иначе тест не различает две меры");

        Assert.IsTrue(ConstraintValidator.HasViolationNear(result, board, boundsGap),
            "близость меряется ПО ГАБАРИТАМ: у крупных деталей центры соседей дальше "
            + "любого разумного радиуса, и проверка по центрам молча пропускала "
            + "нарушения, стоящие вплотную");
    }

    [Test]
    public void HasViolationNear_AtExactlyTheRadius_CountsAsNear_AndJustBeyondItDoesNot()
    {
        var floor = Floor();
        var board = BoardOnFloor("Board");
        var lonely = Part("Lonely", BoardDims, new Vector3(2f, 0f, 0f));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { floor, board, lonely });
        Assume.That(result.violations, Does.Contain(lonely), "одинокая деталь — нарушение");

        float exact = lonely.ToGeometry().Min.x - board.ToGeometry().Max.x;

        Assert.IsTrue(ConstraintValidator.HasViolationNear(result, board, exact),
            "порог ВКЛЮЧИТЕЛЬНЫЙ: деталь, стоящая ровно в радиусе, уже соседняя");
        Assert.IsFalse(ConstraintValidator.HasViolationNear(result, board, exact - 0.03125f),
            "а на 31 мм ближе радиуса — уже нет: без отрицательного случая «всё "
            + "соседнее» выглядело бы так же");
    }

    [Test]
    public void HasViolationNear_TheElementItself_IsAlwaysNear()
    {
        var floor = Floor();
        var lonely = Part("Lonely", BoardDims, new Vector3(5f, 5f, 5f));

        var result = ConstraintValidator.Validate(new List<KitchenElement> { floor, lonely });

        Assert.IsTrue(ConstraintValidator.HasViolationNear(result, lonely, 0f),
            "сама нарушающая деталь соседствует с собой при любом радиусе — на этом "
            + "стоит подсветка перетаскиваемой детали");
    }

    [Test]
    public void FindNearContacts_SkipsARecessedAppliance_ButReportsAPlainPartInItsPlace()
    {
        var sinkGo = ElementFactory.CreateSink("Sink", new Vector3(0f, 0.9f, 0f));
        _spawned.Add(sinkGo);
        var sink = sinkGo.GetComponent<KitchenElement>()!;

        Assume.That(TooSmallGapMm, Is.LessThan(NearContactMinGapMm),
            "зазор контроля обязан лежать ВНЕ зелёной зоны [2..4] мм: 3 мм — это "
            + "нормальный зазор, на нём правило GAP молчит по замыслу, и контроль "
            + "на нём ничего не доказывал бы");
        Assume.That(TooSmallGapMm, Is.GreaterThan(Tolerance.ContactMm),
            "и при этом быть больше порога контакта, иначе пара считается касающейся "
            + "и до правила GAP вообще не доходит");

        var board = TwinOneMillimetreInFrontOf(sink, "BoardBySink");
        var withSink = ConstraintValidator.FindNearContacts(
            new List<KitchenElement> { sink, board }, NearContactMinGapMm, NearContactMaxGapMm);

        var plain = Part("Plain", sink.DimensionsMM, new Vector3(3f, 0.9f, 0f));
        var plainTwin = TwinOneMillimetreInFrontOf(plain, "BoardByPlain");
        var withPlainPart = ConstraintValidator.FindNearContacts(
            new List<KitchenElement> { plain, plainTwin }, NearContactMinGapMm, NearContactMaxGapMm);

        Assert.IsNotEmpty(withPlainPart,
            "контроль: две обычные детали одного габарита в 1 мм друг от друга — "
            + "недожатый снап, GAP-01 обязан сработать");
        Assert.AreEqual(ConstraintValidator.NearContactKind.TooSmall, withPlainPart[0].kind,
            "и именно как «зазор меньше минимума», а не как «слишком большой»");
        Assert.IsEmpty(withSink,
            "врезная техника в парных проверках не участвует: мойка по конструкции "
            + "сидит В столешнице, и зазор до соседа для неё ничего не значит");
    }

    [Test]
    public void Snapshot_KeepsTheOrderOfTheInputList()
    {
        var first = Part("First", BoardDims, new Vector3(0f, 0.2f, 0f));
        var second = Part("Second", BoardDims, new Vector3(2f, 0.2f, 0f));
        var third = Part("Third", BoardDims, new Vector3(4f, 0.2f, 0f));

        var into = new List<ValidationElement>();
        ValidationSnapshot.Build(new List<KitchenElement> { second, third, first }, into);

        Assert.AreEqual(3, into.Count, "снимок строится для каждой детали списка");
        Assert.AreEqual("Second", into[0].Name,
            "порядок снимков = порядок входного списка: индексы, которыми ядро "
            + "отвечает, адаптер переводит обратно ПО НОМЕРУ, и перестановка выдала "
            + "бы нарушение не той детали");
        Assert.AreEqual("Third", into[1].Name, "второй снимок — вторая деталь списка");
        Assert.AreEqual("First", into[2].Name, "третий снимок — третья деталь списка");
    }

    [Test]
    public void Window_InsideALoweredWall_IsStillWithinItsBounds()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2500, 3000), "Wall_Lowered", new Vector3(0f, 1.25f, 0f));
        _spawned.Add(wallGo);
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_InLoweredWall", new Vector3(0f, 1f, 0f));
        _spawned.Add(winGo);

        var wallElement = wallGo.GetComponent<KitchenElement>()!;
        var window = winGo.GetComponent<WindowElement>()!;
        window.SnapToWall();

        var wall = wallGo.GetComponent<Wall>()!;
        wall.SetLowered(true, 0.1f);
        Assume.That(wall.FullPosition.y - wallGo.transform.position.y, Is.GreaterThan(0.5f),
            "стена действительно опущена — иначе тест не различает две позы");

        var result = ConstraintValidator.Validate(
            new List<KitchenElement> { wallElement, window });

        Assert.IsFalse(result.violations.Contains(window),
            "высота стены меряется от ЛОГИЧЕСКОЙ позы (Wall.FullPosition): визуально "
            + "стена бывает подрезана для обзора, но проём обязан помещаться в "
            + "НАСТОЯЩУЮ стену, а не в её обрезок");
    }
}
