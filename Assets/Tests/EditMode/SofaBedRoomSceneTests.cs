using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>SOF-01 на живой сцене: диван и стол — настоящие элементы, ответ читается из
/// <c>SceneAnalyzer.Analyze</c>, то есть оттуда же, откуда его видит окно «Ошибки» и MCP.
/// Арифметика объёма раскладки — на быстром пути в <c>SofaBedRoomTests</c>; здесь
/// проверяется то, чего сцена-без-Unity проверить не может: что снимок берёт позу из
/// настоящего <c>transform</c>, что этап дивана ничего не меняет и что границу объёма
/// проводит ровно там, где настоящее сиденье останавливается.</summary>
public class SofaBedRoomSceneTests
{
    private const float FrontMM = SofaElement.DefaultDepthMM * 0.5f;
    private const float TableDepthMM = 800f;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        PartRegistry.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        LogAssert.ignoreFailingMessages = false;
    }

    private SofaElement DefaultSofa(Vector3 position)
    {
        var go = ElementFactory.CreateSofa(new Vector3Int(SofaElement.DefaultWidthMM,
                SofaElement.DefaultHeightMM, SofaElement.DefaultDepthMM),
            SofaElement.DefaultCornerRadiusMM, SofaElement.DefaultSeatHeightMM, "Диван-1", position);
        _spawned.Add(go);
        return go.GetComponent<SofaElement>();
    }

    private KitchenElement Table(Vector3 centre)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Стол-1";
        var table = go.AddComponent<KitchenElement>();
        table.PartName = go.name;
        table.DimensionsMM = new Vector3Int(800, 750, (int)TableDepthMM);
        table.transform.position = centre;
        PartRegistry.Register(table);
        _spawned.Add(go);
        return table;
    }

    private KitchenElement TableAhead(SofaElement sofa, float gapFromFrontMM) =>
        Table(sofa.transform.position + sofa.transform.rotation * new Vector3(0f, 0f,
            (FrontMM + gapFromFrontMM + TableDepthMM * 0.5f) * AppConstants.MM_TO_UNITS));

    private static List<AnalysisIssue> Sof01()
        => SceneAnalyzer.Analyze().Where(i => i.Code == IssueCatalog.CodeSofaNoRoomToUnfold).ToList();

    [Test]
    public void Analyze_TableThreeHundredMillimetresInFrontOfTheSofa_WarnsSof01AndNamesBoth()
    {
        var sofa = DefaultSofa(Vector3.zero);
        var table = TableAhead(sofa, 300f);

        var found = Sof01();

        Assert.AreEqual(1, found.Count, "стол на пути выезжающего сиденья — одно предупреждение");
        Assert.AreEqual(IssueLevel.Warning, found[0].Level,
            "нехватка места для раскладки — предупреждение, а не ошибка: диван можно оставить сложенным");
        Assert.AreSame(sofa, found[0].Target, "строка принадлежит дивану");
        Assert.AreSame(table, found[0].Secondary, "а мешающая деталь названа вторым участником");
        StringAssert.Contains(table.PartName, found[0].Message,
            "в тексте названа деталь, которую надо отодвинуть");
    }

    [Test]
    public void Analyze_TheSameTableFifteenHundredMillimetresAway_DoesNotWarn()
    {
        var sofa = DefaultSofa(Vector3.zero);
        TableAhead(sofa, 1500f);

        Assert.IsEmpty(Sof01(), "до стола в 1500 мм выдвинутое сиденье не дотягивается");
    }

    [Test]
    public void Analyze_FoldedAndBedStage_GiveTheSameAnswer()
    {
        var sofa = DefaultSofa(Vector3.zero);
        TableAhead(sofa, 300f);
        var table2 = TableAhead(sofa, 1500f);
        table2.PartName = "Стол-2";

        string Answer(SofaStage stage)
        {
            sofa.SnapToStage(stage);
            return string.Join("\n", Sof01().Select(i => i.Detail + "|" + i.Message));
        }

        string folded = Answer(SofaStage.Folded);
        string extended = Answer(SofaStage.Extended);
        string bed = Answer(SofaStage.Bed);

        Assert.IsNotEmpty(folded, "контроль: в сложенном виде предупреждение есть — пользователь "
            + "узнаёт про нехватку места ДО раскладки");
        Assert.AreEqual(folded, extended, "этап «сиденье выдвинуто» отвечает так же");
        Assert.AreEqual(folded, bed, "и «кровать» тоже: объём раскладки не зависит от текущего этапа");
    }

    [Test]
    public void Analyze_TurnedSofa_ReachFollowsTheFrontOfTheSofa()
    {
        var sofa = DefaultSofa(new Vector3(2f, 0f, 3f));
        sofa.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        var ahead = TableAhead(sofa, 300f);

        Assert.AreEqual(1, Sof01().Count,
            "стол стоит перед повёрнутым диваном (по +X): выезд идёт туда, куда смотрит диван");

        ahead.transform.position = sofa.transform.position + new Vector3(0f, 0f,
            (FrontMM + 300f + TableDepthMM * 0.5f) * AppConstants.MM_TO_UNITS);

        Assert.IsEmpty(Sof01(),
            "тот же стол на той же дистанции, но сбоку от повёрнутого дивана, раскладке не мешает");
    }

    [Test]
    public void Analyze_TableInTheWayOrNot_ErrorsAreTheSame_SoTheWarningNeverEscalates()
    {
        var sofa = DefaultSofa(Vector3.zero);
        var table = TableAhead(sofa, 300f);

        string Errors() => string.Join("\n", SceneAnalyzer.Analyze()
            .Where(i => i.Level == IssueLevel.Error).Select(i => i.Code + "|" + i.Detail));

        string near = Errors();
        Assert.AreEqual(1, Sof01().Count, "контроль: стол мешает раскладке");
        table.transform.position = sofa.transform.position + new Vector3(0f, 0f,
            (FrontMM + 1500f + TableDepthMM * 0.5f) * AppConstants.MM_TO_UNITS);
        string far = Errors();
        Assert.IsEmpty(Sof01(), "контроль: стол отодвинут, помехи нет");

        Assert.AreEqual(far, near,
            "уровень ошибок не зависит от того, мешает ли стол раскладке: SOF-01 остаётся "
            + "предупреждением и ничего не добавляет к COL-xx");
    }

    [Test]
    public void Reach_EndsExactlyWhereTheRealSeatStopsInTheBedStage()
    {
        var sofa = DefaultSofa(Vector3.zero);
        sofa.SnapToStage(SofaStage.Bed);

        var elements = new List<KitchenElement> { sofa };
        var snapshots = new List<ValidationElement>();
        ValidationSnapshot.Build(elements, snapshots);
        var reach = snapshots[0].BedReach;

        Assert.IsNotNull(reach, "снимок дивана несёт объём раскладки");
        var seatBounds = sofa.DecorRenderer!.bounds;
        Assert.AreEqual(seatBounds.max.z, reach!.Max.z, 0.001f,
            "настоящее сиденье в положении «кровать» упирается ровно в дальнюю грань объёма: "
            + "иначе предупреждение считает место для другого дивана, не для этого");
        Assert.AreEqual(seatBounds.max.y, reach.Max.y, 0.001f,
            "верх объёма — верх сиденья");
    }
}
