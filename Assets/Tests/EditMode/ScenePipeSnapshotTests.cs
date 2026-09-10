using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Переходник между сценой и чистым ядром прокладки труб.
///
/// Ядро (<c>PipeRules</c>) ничего не знает про Unity и проверяется под dotnet;
/// здесь проверяется ровно то, чего ядро увидеть не может — КАК сцена
/// превращается в порты, отрезки и препятствия. Три вопроса, и все три уже
/// один раз стоили бы отладки, если бы переходник соврал:
///
/// 1. Труба не препятствие сама себе. Иначе стык двух труб выдавал бы PIP-03 в
///    каждом соединении, и правило про пересечение стало бы бесполезным шумом.
/// 2. Прокладка ВНУТРИ стены и пола законна, внутри детали — нет. Это не
///    оттенок: труба в стене — норма монтажа, труба в столешнице — брак.
/// 3. Свободный конец трубы — PIP-01 у КАЖДОГО конца, а состыкованные концы
///    молчат. Пока фитингов нет, это единственный способ отличить «трасса не
///    закончена» от «трасса собрана».
///
/// Имена элементов здесь ЛАТИНСКИЕ, и это не вкус: <c>ElementNaming.Rule</c>
/// разрешает в PartName только латиницу, цифры, '-' и '_', а фабрика прогоняет
/// любое имя через <c>ElementNaming.Normalize</c>. Кириллическое «Стояк»
/// доезжает до отчёта как «Stoyak», поэтому сверять ElementId с кириллическим
/// литералом — значит проверять не переходник, а транслитерацию: тест краснел
/// на разнице длин 5 и 6 при полностью исправном коде.</summary>
public class ScenePipeSnapshotTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private PipeElement Pipe(string name, int lengthMM, Vector3 pos, string sizeId)
    {
        var go = ElementFactory.CreatePipe(sizeId, lengthMM, name, pos);
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private KitchenElement Board(string name, Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreatePart(dims, name, pos);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private KitchenElement Wall(string name, Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreateWall(dims, name, pos);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private static List<PipeFinding> Findings(params KitchenElement[] scene) =>
        PipeRules.Collect(new ScenePipeSnapshot(scene)).ToList();

    private static List<PipeFinding> Of(IEnumerable<PipeFinding> findings, string code) =>
        findings.Where(f => f.Code == code).ToList();

    private static float Units(int mm) => mm * AppConstants.MM_TO_UNITS;

    [Test]
    public void ASinglePipe_ReportsBothOfItsEndsAsOpen_AndNothingElse()
    {
        var pipe = Pipe("Run", 600, new Vector3(0f, Units(300), 0f), PipeSpec.Dn20);

        var findings = Findings(pipe);

        Assert.AreEqual(2, Of(findings, PipeIssueCatalog.CodeOpenEnd).Count,
            "у трубы два конца, и без фитинга открыт каждый: один PIP-01 на трассу означал бы, "
            + "что второй конец закрывать не надо");
        Assert.IsEmpty(Of(findings, PipeIssueCatalog.CodeObstacleCrossed),
            "препятствий в сцене нет — пересекать нечего");
    }

    /// <summary>Труба к трубе — не стык: <c>PipeConnectionRule</c> требует между двумя
    /// отрезками фитинг, и торцы, сведённые вплотную, остаются свободными ОБА.
    /// Раньше здесь стояло «стык закрыл по одному концу каждой трубы» — это описание
    /// сети, которой больше нет.</summary>
    [Test]
    public void TwoPipesButtedEndToEnd_AreNotAJoint_AndStillDoNotCrossEachOther()
    {
        var lower = Pipe("Lower", 600, new Vector3(0f, Units(300), 0f), PipeSpec.Dn20);
        var upper = Pipe("Upper", 600, new Vector3(0f, Units(900), 0f), PipeSpec.Dn20);

        var findings = Findings(lower, upper);

        Assert.AreEqual(4, Of(findings, PipeIssueCatalog.CodeOpenEnd).Count,
            "без муфты между ними стыка нет вовсе: у каждой трубы открыты ОБА торца");
        Assert.IsEmpty(Of(findings, PipeIssueCatalog.CodeObstacleCrossed),
            "труба не препятствие для трубы: иначе КАЖДОЕ примыкание трассы читалось бы "
            + "как PIP-03, и правило про пересечение утонуло бы в собственном шуме");
    }

    private PipeFittingElement Fitting(GameObject go)
    {
        _spawned.Add(go);
        return go.GetComponent<PipeFittingElement>();
    }

    private static float UnitsMm(float mm) => mm * AppConstants.MM_TO_UNITS;

    /// <summary>На сколько первый порт фитинга отстоит от его центра по оси Y.
    /// Взято из той же арифметики, что строит меш и считает порты: выпиши сюда
    /// число руками — и тест зафиксирует СВОЁ представление о размере фитинга,
    /// а на разъехавшейся геометрии просто позеленеет, ничего не состыковав.
    /// Знак снят намеренно: у заглушки порт смотрит вверх, у муфты первый —
    /// вниз, а нужна тут длина, а не направление.</summary>
    private static float PortReachMm(PipeNodeKind kind) =>
        Mathf.Abs(PipeFittingSpec.PortOffsetMm(kind, PipeSpec.Dn20, 0).YMm);

    [Test]
    public void ACapUnderARun_ClosesThatEnd_AndLeavesTheOtherOneOpen()
    {
        var pipe = Pipe("Riser", 600, new Vector3(0f, Units(300), 0f), PipeSpec.Dn20);
        var cap = Fitting(ElementFactory.CreatePipeCap("Plug",
            new Vector3(0f, -UnitsMm(PortReachMm(PipeNodeKind.Cap)), 0f)));

        var openEnds = Of(Findings(pipe, cap), PipeIssueCatalog.CodeOpenEnd);

        Assert.AreEqual(1, openEnds.Count,
            "заглушка встала ровно на нижний конец — там PIP-01 обязан замолчать, а верхний "
            + "конец остаётся свободным и обязан ругаться дальше");
        Assert.AreEqual("Riser", openEnds[0].ElementId,
            "PIP-01 остался у трубы: сам по себе фитинг открытых концов не имеет — "
            + "свободный порт фитинга это не «трасса не закончена», а «сюда ещё не подвели»");
    }

    /// <summary>Обе стороны PIP-02 на настоящей сцене, одними и теми же трубами.
    ///
    /// Переходная муфта — это и есть железка, которой сводят разные ДУ: два порта
    /// соосно, диаметры сторон независимы и оба выводятся с подведённых труб. А
    /// тройник бывает только одного диаметра (<c>PipeNodePorts.RequiresOneSize</c>),
    /// и те же ДУ 20 с ДУ 32 на нём — отказ. Порознь эти половины ничего не стоят:
    /// правило, которое всегда молчит, пройдёт первую, а правило, которое всегда
    /// ругается, — вторую.
    ///
    /// Второй половиной раньше стояли ДВЕ ТРУБЫ ВСТЫК. С появлением
    /// <c>PipeConnectionRule</c> труба с трубой не соединяется вовсе, значит и
    /// сравнивать на несуществующем узле нечего (там теперь PIP-01 × 4, см.
    /// <see cref="TwoPipesButtedEndToEnd_AreNotAJoint_AndStillDoNotCrossEachOther"/>):
    /// тот сценарий проверял не PIP-02, а сеть, которой больше нет.</summary>
    [Test]
    public void ATransitionCoupling_SilencesPip02_WhileATeeOnTheSameTwoBoresFiresIt()
    {
        float reach = PortReachMm(PipeNodeKind.Coupling);

        PipeElement Lower() => Pipe("Lower", 600, new Vector3(0f, Units(300), 0f), PipeSpec.Dn20);
        PipeFittingElement Sleeve() => Fitting(ElementFactory.CreatePipeCoupling("Sleeve",
            new Vector3(0f, Units(600) + UnitsMm(reach), 0f)));
        PipeElement Upper(string sizeId) => Pipe("Upper", 600,
            new Vector3(0f, Units(600) + UnitsMm(2f * reach) + Units(300), 0f), sizeId);

        Assert.IsEmpty(Of(Findings(Lower(), Sleeve(), Upper(PipeSpec.Dn32)),
                PipeIssueCatalog.CodeSizeMismatch),
            "переходная муфта для того и стоит: ДУ 20 и ДУ 32 сведены на ней законно");

        Assert.IsEmpty(Of(Findings(Lower(), Sleeve(), Upper(PipeSpec.Dn20)),
                PipeIssueCatalog.CodeSizeMismatch),
            "контроль: одним диаметром через ту же муфту тоже молчит — иначе первая "
            + "половина зелена просто потому, что правило перестало срабатывать вообще");

        var mismatched = TeeSpan(PipeSpec.Dn32, out var branch);
        var onTee = Of(Findings(mismatched), PipeIssueCatalog.CodeSizeMismatch);

        Assert.AreEqual(1, onTee.Count,
            "а тройник — не переходник: он одного диаметра, и свести на нём ДУ 20 с ДУ 32 "
            + "нельзя — ровно ради этого стыка PIP-02 и написан");
        Assert.AreEqual(branch.PartName, onTee[0].ElementId,
            "виноват узел, который сводит два размера, а не труба, которая честно "
            + "объявила свой");

        Assert.IsEmpty(Of(Findings(TeeSpan(PipeSpec.Dn20, out _)),
                PipeIssueCatalog.CodeSizeMismatch),
            "контроль: тот же тройник одним диаметром молчит");
    }

    /// <summary>Тройник с двумя трубами на соосных портах 0 и 1. Устья тройника
    /// отстоят от его центра и по Y, и по X (ступица не в центре габарита), поэтому
    /// центр сдвинут на ту же X-составляющую — иначе устья не легли бы на ось труб.
    /// Боковой порт остаётся свободным: это PIP-01, а не PIP-02.</summary>
    private KitchenElement[] TeeSpan(string upperSizeId, out PipeFittingElement tee)
    {
        var mouth = PipeFittingSpec.PortOffsetMm(PipeNodeKind.Tee, PipeSpec.Dn20, 0);
        float reachMm = Mathf.Abs(mouth.YMm);

        var lower = Pipe("Lower", 600, new Vector3(0f, Units(300), 0f), PipeSpec.Dn20);
        tee = Fitting(ElementFactory.CreatePipeTee("Branch",
            new Vector3(-UnitsMm(mouth.XMm), Units(600) + UnitsMm(reachMm), 0f)));
        var upper = Pipe("Upper", 600,
            new Vector3(0f, Units(600) + UnitsMm(2f * reachMm) + Units(300), 0f), upperSizeId);

        return new KitchenElement[] { lower, tee, upper };
    }

    /// <summary>И вторая половина решения: диаметры сторон переходной муфты
    /// действительно РАЗНЫЕ и оба доезжают до её свойств. Одно число здесь
    /// означало бы, что муфта опять соосная и одноразмерная.</summary>
    [Test]
    public void ATransitionCoupling_CarriesBothBoresIntoItsProperties()
    {
        float reach = PortReachMm(PipeNodeKind.Coupling);

        var lower = Pipe("Lower", 600, new Vector3(0f, Units(300), 0f), PipeSpec.Dn20);
        var sleeve = Fitting(ElementFactory.CreatePipeCoupling("Sleeve",
            new Vector3(0f, Units(600) + UnitsMm(reach), 0f)));
        var upper = Pipe("Upper", 600,
            new Vector3(0f, Units(600) + UnitsMm(2f * reach) + Units(300), 0f), PipeSpec.Dn32);

        KitchenDesigner.Core.Analysis.PipeFittingSizeLink.ApplyAll(
            new KitchenElement[] { lower, sleeve, upper });

        CollectionAssert.AreEqual(new[] { PipeSpec.Dn20, PipeSpec.Dn32 }, sleeve.BoreSizeIds,
            "«если трубы разного диаметра подводят к сгону — в свойствах должны быть "
            + "написаны диаметры»: их два, и они разные");
    }

    [Test]
    public void APipeCrossingABoard_IsPip03()
    {
        var pipe = Pipe("Riser", 600, new Vector3(0f, Units(300), 0f), PipeSpec.Dn20);
        var board = Board("Countertop", new Vector3Int(600, 400, 18), Vector3.zero);

        var crossings = Of(Findings(pipe, board), PipeIssueCatalog.CodeObstacleCrossed);

        Assert.AreEqual(1, crossings.Count,
            "труба проходит сквозь деталь — под неё сверлят отверстие, а не топят её в пласти");
        Assert.AreEqual("Riser", crossings[0].ElementId);
        Assert.AreEqual("Countertop", crossings[0].OtherElementId,
            "в отчёте обязаны стоять ОБА участника: по одному имени виновника не найти");
    }

    [Test]
    public void APipeInsideAWall_IsLegal_BecauseThatIsHowPipesAreLaid()
    {
        var pipe = Pipe("Riser", 600, new Vector3(0f, Units(300), 0f), PipeSpec.Dn20);
        var wall = Wall("Partition", new Vector3Int(2000, 2500, 100),
            new Vector3(0f, Units(1250), 0f));

        Assert.IsEmpty(Of(Findings(pipe, wall), PipeIssueCatalog.CodeObstacleCrossed),
            "прокладка в стене — норма монтажа; запретив её, правило заставило бы вести "
            + "трассу по воздуху вдоль стены");
    }

    [Test]
    public void APipeInsideTheFloorSlab_IsLegalToo()
    {
        var pipe = Pipe("Underfloor", 600, new Vector3(0f, Units(-50), 0f), PipeSpec.Dn20);
        var go = ElementFactory.CreateFloor(new Vector3Int(3000, 200, 3000), "Floor",
            new Vector3(0f, Units(-100), 0f));
        _spawned.Add(go);
        var floor = go.GetComponent<KitchenElement>();

        Assert.IsEmpty(Of(Findings(pipe, floor), PipeIssueCatalog.CodeObstacleCrossed),
            "стяжка — то же самое, что стена: трубу в неё кладут, а не считают браком");
    }

    [Test]
    public void ObstacleKind_SplitsTheSceneIntoWhatMayBeCrossedAndWhatMayNot()
    {
        var board = Board("Shelf", new Vector3Int(600, 18, 300), Vector3.zero);
        var table = ElementFactory.CreateTable(new Vector3Int(1200, 750, 700), "Table",
            new Vector3(3f, 0f, 0f));
        _spawned.Add(table);
        var wall = Wall("Partition", new Vector3Int(2000, 2500, 100), new Vector3(6f, 0f, 0f));
        var floorGo = ElementFactory.CreateFloor(new Vector3Int(3000, 200, 3000), "Floor",
            new Vector3(9f, 0f, 0f));
        _spawned.Add(floorGo);

        Assert.AreEqual(PipeObstacleKind.Part, ScenePipeSnapshot.KindOf(board));
        Assert.AreEqual(PipeObstacleKind.Part,
            ScenePipeSnapshot.KindOf(table.GetComponent<KitchenElement>()),
            "мебель отвечает на тот же вопрос, что и деталь: сквозь неё трубу не ведут. "
            + "PipeObstacleKind.Furniture ядро различает, а сцена этой границы пока не "
            + "проводит — единственного признака «это мебель» в слое нет, а деление наугад "
            + "врало бы про фасады и полки");
        Assert.AreEqual(PipeObstacleKind.Wall, ScenePipeSnapshot.KindOf(wall));
        Assert.AreEqual(PipeObstacleKind.Floor,
            ScenePipeSnapshot.KindOf(floorGo.GetComponent<KitchenElement>()));

        Assert.IsTrue(PipeRules.BlocksRouting(PipeObstacleKind.Part));
        Assert.IsFalse(PipeRules.BlocksRouting(PipeObstacleKind.Wall),
            "контроль: разница между Part и Wall — это и есть всё правило PIP-03");
        Assert.IsFalse(PipeRules.BlocksRouting(PipeObstacleKind.Floor));
    }
}
