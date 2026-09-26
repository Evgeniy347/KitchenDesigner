using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>review-construction.md #1: DuctElement kept `transform.localScale = Vector3.one`
/// and never overrode EffectiveScale, so a 125 mm / 3000 mm duct validated as a 1x1x1 m cube
/// around its centre - a cabinet 1.2 m along the run went through it unnoticed, and a wall
/// 300 mm clear of the duct's real surface raised a false Overlap. PipeElement already shows
/// the fix for the same "long thin body" shape: EffectiveScale from FurnitureLayout.PhysicalScale
/// over DimensionsMM, which DuctElement already keeps in sync via SyncDimensions.</summary>
public class DuctElementValidationBoxTests
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

    private DuctElement SpawnDuct(Vector3 position)
    {
        var go = ElementFactory.CreateDuct(3000, "Duct", position);
        _spawned.Add(go);
        var duct = go.GetComponent<DuctElement>();
        duct.DiameterMm = 125;
        return duct;
    }

    private GameObject SpawnBox(Vector3Int dimensionsMm, string name, Vector3 position)
    {
        var go = ElementFactory.CreatePart(dimensionsMm, name, position);
        _spawned.Add(go);
        return go;
    }

    [Test]
    public void GetVertices_SpansTheRealPipe_NotAOneMeterCube()
    {
        var duct = SpawnDuct(Vector3.zero);

        var vertices = duct.GetVertices();
        float minX = vertices.Min(v => v.x), maxX = vertices.Max(v => v.x);
        float minY = vertices.Min(v => v.y), maxY = vertices.Max(v => v.y);

        Assert.AreEqual(3f, maxY - minY, 0.01f,
            "воздуховод 3000 мм длиной обязан проверяться на всю длину по оси прогона");
        Assert.Less(maxX - minX, 0.2f,
            "круглый воздуховод 125 мм в диаметре не должен раздуваться до метра в сечении");
    }

    [Test]
    public void CabinetOnePointTwoMetresAlongTheRun_ThroughTheDuct_IsCaught()
    {
        SpawnDuct(Vector3.zero);
        SpawnBox(new Vector3Int(200, 200, 200), "Cabinet", new Vector3(0f, 1.2f, 0f));

        var issues = SceneAnalyzer.Analyze();

        Assert.IsTrue(issues.Any(i => i.Code == IssueCatalog.CodeOverlap),
            "воздуховод 3000 мм длиной тянется на 1,5 м в каждую сторону от центра - шкаф "
            + "в 1,2 м вдоль прогона стоит прямо на пути; куб 1x1x1 м вокруг пивота (реальная "
            + "досягаемость 0,5 м) этого не видел (review-construction.md #1)");
    }

    [Test]
    public void WallThreeHundredMillimetresClearOfTheDuctSurface_IsNotCaught()
    {
        SpawnDuct(Vector3.zero);
        SpawnBox(new Vector3Int(100, 2700, 3000), "Wall", new Vector3(0.35f, 0f, 0f));

        var issues = SceneAnalyzer.Analyze();

        Assert.IsFalse(issues.Any(i => i.Code == IssueCatalog.CodeOverlap),
            "стена стоит гранью в 300 мм от оси воздуховода - при радиусе 62,5 мм до "
            + "поверхности воздуховода ещё 237,5 мм воздуха; куб 1x1x1 м вокруг пивота "
            + "(реальная досягаемость 0,5 м) раньше ловил здесь ложное пересечение "
            + "(review-construction.md #1)");
    }
}
