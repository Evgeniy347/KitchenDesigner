using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Construction;

/// <summary>F5: FoundationRules подключён к SceneAnalyzer через SceneFoundationSnapshot.
/// Геометрия обхода уже доказана на голых WallCentreline в FoundationLayoutTests и
/// FoundationCoverageTests — здесь та же геометрия проверяется через реальные Wall и
/// FoundationElement, построенные ElementFactory, чтобы поймать разрыв именно в
/// проводке (снимок сцены → FoundationRules.Collect → IssueCatalog), а не в правилах
/// самих по себе.</summary>
public class SceneFoundationAnalysisTests
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
        LevelRegistry.Reset();
    }

    private GameObject SpawnWall(Vector3Int dimensionsMm, string name, Vector3 position)
    {
        var go = ElementFactory.CreateWall(dimensionsMm, name, position);
        _spawned.Add(go);
        return go;
    }

    private FoundationElement SpawnFoundation()
    {
        var go = ElementFactory.CreateFoundation(FoundationElement.DEFAULT_WIDTH_MM,
            FoundationElement.DEFAULT_DEPTH_MM, "Foundation", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<FoundationElement>();
    }

    // Тот же замкнутый прямоугольник 4000×3000 мм, что и в
    // FoundationLayoutTests.MergeIntoPolylines_ClosedBox — периметр 14000 мм, четыре угла
    // делят ровно по две стены, обход обязан остановиться в стартовом узле.
    private void SpawnClosedBoxOfFourLoadBearingWalls()
    {
        var a = SpawnWall(new Vector3Int(4000, 2700, 250), "WallA", new Vector3(2f, 1.35f, 0f));
        var b = SpawnWall(new Vector3Int(250, 2700, 3000), "WallB", new Vector3(4f, 1.35f, 1.5f));
        var c = SpawnWall(new Vector3Int(4000, 2700, 250), "WallC", new Vector3(2f, 1.35f, 3f));
        var d = SpawnWall(new Vector3Int(250, 2700, 3000), "WallD", new Vector3(0f, 1.35f, 1.5f));

        foreach (var wallGo in new[] { a, b, c, d })
            Assert.IsTrue(wallGo.GetComponent<Wall>().LoadBearing,
                "ElementFactory.CreateWall по умолчанию ставит несущую кирпичную стену — "
                + "тест полагается на этот дефолт, а не переопределяет его сам");
    }

    [Test]
    public void FourLoadBearingBrickWalls_BuildExactlyOneStrip()
    {
        SpawnClosedBoxOfFourLoadBearingWalls();
        var foundation = SpawnFoundation();

        Assert.AreEqual(1, foundation.BuiltPolylines.Count,
            "четыре несущие стены замкнутого контура сливаются в ОДНУ ленту "
            + "(FoundationLayout.MergeIntoPolylines): FoundationElement.ApplyDimensions "
            + "обязан был закэшировать именно её в BuiltPolylines для FND-05");
        Assert.AreEqual(5, foundation.BuiltPolylines[0].Points.Count,
            "четыре угла плюс возврат в стартовую точку — пять точек одной замкнутой "
            + "полилинии, как и в FoundationLayoutTests");
    }

    [Test]
    public void ConcreteVolume_MatchesAHandCalculation_PerimeterTimesWidthTimesDepth()
    {
        SpawnClosedBoxOfFourLoadBearingWalls();
        var foundation = SpawnFoundation();

        var centrelines = FoundationWallSurvey.LoadBearingCentrelines(PartRegistry.GetAll());
        var quantities = FoundationQuantities.OfLoadBearingWalls(foundation.SoilKind, centrelines,
            FoundationElement.DEFAULT_WIDTH_MM, FoundationElement.DEFAULT_DEPTH_MM,
            foundation.SandMm, foundation.GravelMm, foundation.RebarDiameterMm,
            foundation.RebarStepMm, foundation.CoverMm);

        double expectedConcreteM3 = 14d * (FoundationElement.DEFAULT_WIDTH_MM * 0.001d)
            * (FoundationElement.DEFAULT_DEPTH_MM * 0.001d);
        Assert.AreEqual(expectedConcreteM3, quantities.ConcreteM3, 0.01,
            "бетон ленты, посчитанный руками: периметр 14 м × ширина "
            + $"{FoundationElement.DEFAULT_WIDTH_MM} мм × высота {FoundationElement.DEFAULT_DEPTH_MM} мм");
    }

    /// <summary>test-results/review-construction.md #3: FoundationWallSurvey scanned load-bearing
    /// walls of EVERY storey with no level filter, so a two-storey house with identical wall
    /// layouts on both floors doubled the concrete, excavation and rebar quantities and drew the
    /// strip twice (z-fighting). Only the LOWEST storey's walls sit on the strip.</summary>
    [Test]
    public void ConcreteVolume_TwoIdenticalStoreys_CountsOnlyTheGroundFloorWalls_NotBothStoreys()
    {
        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 2700),
            new Level("2", "2 этаж", 2700, 2700),
        });

        SpawnClosedBoxOfFourLoadBearingWalls();
        foreach (var go in _spawned) go.GetComponent<KitchenElement>().LevelId = "1";

        int groundFloorCount = _spawned.Count;
        var a2 = SpawnWall(new Vector3Int(4000, 2700, 250), "WallA2", new Vector3(2f, 4.05f, 0f));
        var b2 = SpawnWall(new Vector3Int(250, 2700, 3000), "WallB2", new Vector3(4f, 4.05f, 1.5f));
        var c2 = SpawnWall(new Vector3Int(4000, 2700, 250), "WallC2", new Vector3(2f, 4.05f, 3f));
        var d2 = SpawnWall(new Vector3Int(250, 2700, 3000), "WallD2", new Vector3(0f, 4.05f, 1.5f));
        foreach (var wallGo in new[] { a2, b2, c2, d2 })
        {
            wallGo.GetComponent<KitchenElement>().LevelId = "2";
            Assert.IsTrue(wallGo.GetComponent<Wall>().LoadBearing);
        }

        var foundation = SpawnFoundation();

        var centrelines = FoundationWallSurvey.LoadBearingCentrelinesOnLowestLevel(PartRegistry.GetAll());
        Assert.AreEqual(groundFloorCount, centrelines.Count,
            "второй этаж поставил ещё четыре несущие стены на ТЕХ ЖЕ координатах X/Z — "
            + "фильтр по нижнему этажу обязан оставить только первые четыре");

        var quantities = FoundationQuantities.OfLoadBearingWalls(foundation.SoilKind, centrelines,
            FoundationElement.DEFAULT_WIDTH_MM, FoundationElement.DEFAULT_DEPTH_MM,
            foundation.SandMm, foundation.GravelMm, foundation.RebarDiameterMm,
            foundation.RebarStepMm, foundation.CoverMm);

        double expectedConcreteM3 = 14d * (FoundationElement.DEFAULT_WIDTH_MM * 0.001d)
            * (FoundationElement.DEFAULT_DEPTH_MM * 0.001d);
        Assert.AreEqual(expectedConcreteM3, quantities.ConcreteM3, 0.01,
            "бетон обязан остаться таким же, как для одного этажа (14 м периметра) — второй "
            + "этаж с идентичной планировкой не должен удваивать смету");

        Assert.AreEqual(1, foundation.BuiltPolylines.Count,
            "лента строится один раз по нижнему этажу, а не рисуется дважды поверх себя "
            + "(z-fighting из финдинга)");
    }

    /// <summary>review-perf-tests-tooling.md #11: SpecificationCoverageGuardTests exempts
    /// FoundationElement because its EveryElementType specimen has no wall next to it and
    /// honestly returns zero rows — but nothing in EditMode then ran
    /// SpecificationManager.Build over a FoundationElement WITH a load-bearing wall, so a
    /// break in FoundationWallSurvey.LoadBearingCentrelinesOnLowestLevel (called from
    /// FoundationElement.GetSpecItems) could silently return an empty ведомость in the real
    /// app and every guard would stay green.</summary>
    [Test]
    public void SpecificationManager_Build_ProducesFoundationLines_WhenALoadBearingWallExists()
    {
        SpawnClosedBoxOfFourLoadBearingWalls();
        SpawnFoundation();

        var result = SpecificationManager.Build(PartRegistry.GetAll());

        var foundationLines = result.lines.Where(l => l.section == SpecSections.Foundation).ToList();
        Assert.IsNotEmpty(foundationLines,
            "лента фундамента рядом с четырьмя несущими стенами обязана дать хотя бы одну "
            + "строку в разделе «Фундамент» через настоящий маршрут SpecificationManager.Build "
            + "(FoundationElement.GetSpecItems → FoundationWallSurvey), а не только через "
            + "FoundationSpecItems.Of на руками собранных числах (Pure)");
    }

    [Test]
    public void AWallAddedAfterTheStripWasBuilt_Raises_Fnd05()
    {
        var a = SpawnWall(new Vector3Int(4000, 2700, 250), "WallA", new Vector3(2f, 1.35f, 0f));
        var b = SpawnWall(new Vector3Int(250, 2700, 3000), "WallB", new Vector3(4f, 1.35f, 1.5f));
        Assert.IsTrue(a.GetComponent<Wall>().LoadBearing);
        Assert.IsTrue(b.GetComponent<Wall>().LoadBearing);

        SpawnFoundation();

        var before = SceneAnalyzer.Analyze();
        Assert.IsFalse(before.Any(i => i.Code == IssueCatalog.CodeFoundationWallNotCovered),
            "обе стены вошли в построение ленты при её создании — покрытие полное, "
            + "FND-05 звучать не должен");

        SpawnWall(new Vector3Int(250, 2700, 2000), "WallAddedLater", new Vector3(4f, 1.35f, -1f));

        var after = SceneAnalyzer.Analyze();
        Assert.IsTrue(after.Any(i => i.Code == IssueCatalog.CodeFoundationWallNotCovered),
            "пятая стена появилась ПОСЛЕ того, как FoundationElement построил ленту только "
            + "по A и B: FoundationElement.ApplyDimensions на неё не среагировал (ничего "
            + "своего у неё не менялось), BuiltPolylines остался прежним, и её осевая не "
            + "лежит ни на одном отрезке — FND-05 обязан это назвать");
    }

    /// <summary>test-results/review-construction.md #10: before FoundationHostLink, the ONLY
    /// in-app remedy for the FND-05 raised above was to nudge the width/depth and back — the
    /// scene's own settle pass never rebuilt the strip. Now SceneChangeTracker.SettleDerivedLinks
    /// (the same pass a wall move or an MCP edit already runs through CommandStack/PartRegistry)
    /// picks it up automatically, and FND-05 clears itself without any manual dimension edit.</summary>
    [Test]
    public void WallAddedAfterTheStripWasBuilt_SettleDerivedLinks_RebuildsTheStripAndClearsFnd05()
    {
        SpawnWall(new Vector3Int(4000, 2700, 250), "WallA", new Vector3(2f, 1.35f, 0f));
        SpawnWall(new Vector3Int(250, 2700, 3000), "WallB", new Vector3(4f, 1.35f, 1.5f));
        SpawnFoundation();

        SpawnWall(new Vector3Int(250, 2700, 2000), "WallAddedLater", new Vector3(4f, 1.35f, -1f));

        SceneChangeTracker.SettleDerivedLinks();

        var issues = SceneAnalyzer.Analyze();
        Assert.IsFalse(issues.Any(i => i.Code == IssueCatalog.CodeFoundationWallNotCovered),
            "SettleDerivedLinks обязан перестроить ленту по актуальному списку несущих стен "
            + "через FoundationHostLink.ApplyAll — после этого пятая стена больше не "
            + "«не покрыта», а FND-05 не звучит без ручной правки ширины/глубины");
    }
}
