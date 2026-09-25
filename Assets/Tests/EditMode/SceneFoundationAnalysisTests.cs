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
}
