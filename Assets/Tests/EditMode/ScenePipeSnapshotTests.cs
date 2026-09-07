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
///    закончена» от «трасса собрана».</summary>
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
        var pipe = Pipe("Труба", 600, new Vector3(0f, Units(300), 0f), PipeSpec.Dn20);

        var findings = Findings(pipe);

        Assert.AreEqual(2, Of(findings, PipeIssueCatalog.CodeOpenEnd).Count,
            "у трубы два конца, и без фитинга открыт каждый: один PIP-01 на трассу означал бы, "
            + "что второй конец закрывать не надо");
        Assert.IsEmpty(Of(findings, PipeIssueCatalog.CodeObstacleCrossed),
            "препятствий в сцене нет — пересекать нечего");
    }

    [Test]
    public void TwoPipesButtedEndToEnd_HaveNoOpenEndAtTheJoint_AndDoNotCrossEachOther()
    {
        var lower = Pipe("Низ", 600, new Vector3(0f, Units(300), 0f), PipeSpec.Dn20);
        var upper = Pipe("Верх", 600, new Vector3(0f, Units(900), 0f), PipeSpec.Dn20);

        var findings = Findings(lower, upper);

        Assert.AreEqual(2, Of(findings, PipeIssueCatalog.CodeOpenEnd).Count,
            "стык закрыл по одному концу каждой трубы: свободными остались только крайние два");
        Assert.IsEmpty(Of(findings, PipeIssueCatalog.CodeObstacleCrossed),
            "труба не препятствие для трубы: иначе КАЖДЫЙ стык трассы читался бы как PIP-03, "
            + "и правило про пересечение утонуло бы в собственном шуме");
    }

    [Test]
    public void TwoPipesOfDifferentBore_ButtedTogether_AreReportedAsPip02()
    {
        var lower = Pipe("Низ", 600, new Vector3(0f, Units(300), 0f), PipeSpec.Dn20);
        var upper = Pipe("Верх", 600, new Vector3(0f, Units(900), 0f), PipeSpec.Dn32);

        var mismatches = Of(Findings(lower, upper), PipeIssueCatalog.CodeSizeMismatch);

        Assert.AreEqual(1, mismatches.Count,
            "ДУ 20 и ДУ 32 состыкованы напрямую — без переходника такой стык не собрать");
    }

    [Test]
    public void APipeCrossingABoard_IsPip03()
    {
        var pipe = Pipe("Стояк", 600, new Vector3(0f, Units(300), 0f), PipeSpec.Dn20);
        var board = Board("Столешница", new Vector3Int(600, 400, 18), Vector3.zero);

        var crossings = Of(Findings(pipe, board), PipeIssueCatalog.CodeObstacleCrossed);

        Assert.AreEqual(1, crossings.Count,
            "труба проходит сквозь деталь — под неё сверлят отверстие, а не топят её в пласти");
        Assert.AreEqual("Стояк", crossings[0].ElementId);
        Assert.AreEqual("Столешница", crossings[0].OtherElementId,
            "в отчёте обязаны стоять ОБА участника: по одному имени виновника не найти");
    }

    [Test]
    public void APipeInsideAWall_IsLegal_BecauseThatIsHowPipesAreLaid()
    {
        var pipe = Pipe("Стояк", 600, new Vector3(0f, Units(300), 0f), PipeSpec.Dn20);
        var wall = Wall("Стена", new Vector3Int(2000, 2500, 100),
            new Vector3(0f, Units(1250), 0f));

        Assert.IsEmpty(Of(Findings(pipe, wall), PipeIssueCatalog.CodeObstacleCrossed),
            "прокладка в стене — норма монтажа; запретив её, правило заставило бы вести "
            + "трассу по воздуху вдоль стены");
    }

    [Test]
    public void APipeInsideTheFloorSlab_IsLegalToo()
    {
        var pipe = Pipe("Разводка", 600, new Vector3(0f, Units(-50), 0f), PipeSpec.Dn20);
        var go = ElementFactory.CreateFloor(new Vector3Int(3000, 200, 3000), "Пол",
            new Vector3(0f, Units(-100), 0f));
        _spawned.Add(go);
        var floor = go.GetComponent<KitchenElement>();

        Assert.IsEmpty(Of(Findings(pipe, floor), PipeIssueCatalog.CodeObstacleCrossed),
            "стяжка — то же самое, что стена: трубу в неё кладут, а не считают браком");
    }

    [Test]
    public void ObstacleKind_SplitsTheSceneIntoWhatMayBeCrossedAndWhatMayNot()
    {
        var board = Board("Полка", new Vector3Int(600, 18, 300), Vector3.zero);
        var table = ElementFactory.CreateTable(new Vector3Int(1200, 750, 700), "Стол",
            new Vector3(3f, 0f, 0f));
        _spawned.Add(table);
        var wall = Wall("Стена", new Vector3Int(2000, 2500, 100), new Vector3(6f, 0f, 0f));
        var floorGo = ElementFactory.CreateFloor(new Vector3Int(3000, 200, 3000), "Пол",
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
