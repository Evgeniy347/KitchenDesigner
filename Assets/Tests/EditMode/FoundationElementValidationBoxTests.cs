using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>review-construction.md #1: FoundationElement kept `transform.localScale = Vector3.one`
/// and never overrode EffectiveScale. The sidebar spawns it at Vector3.zero
/// (UI/ElementSpawner.cs), so its validation body used to be the 1x1x1 m cube around the WORLD
/// ORIGIN - unrelated to where FoundationStripMesh actually draws the strip (along the
/// load-bearing walls, wherever they are). A carcass sitting in that phantom origin cube raised
/// a false Overlap, and a carcass genuinely sitting inside the real strip was never caught.
/// FoundationElement.ApplyDimensions now computes the strip's own bounding footprint
/// (Pure/Construction/FoundationFootprint) and validates against THAT, through
/// ValidationPositionAt/EffectiveScale - the same override point AttachRider uses for a
/// logical position that differs from transform.position.</summary>
public class FoundationElementValidationBoxTests
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

    private void SpawnClosedBoxOfFourLoadBearingWalls()
    {
        SpawnWall(new Vector3Int(4000, 2700, 250), "WallA", new Vector3(7f, 1.35f, 5f));
        SpawnWall(new Vector3Int(250, 2700, 3000), "WallB", new Vector3(9f, 1.35f, 6.5f));
        SpawnWall(new Vector3Int(4000, 2700, 250), "WallC", new Vector3(7f, 1.35f, 8f));
        SpawnWall(new Vector3Int(250, 2700, 3000), "WallD", new Vector3(5f, 1.35f, 6.5f));
    }

    private FoundationElement SpawnFoundationAtOrigin()
    {
        var go = ElementFactory.CreateFoundation(FoundationElement.DEFAULT_WIDTH_MM,
            FoundationElement.DEFAULT_DEPTH_MM, "Foundation", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<FoundationElement>();
    }

    private GameObject SpawnBox(Vector3Int dimensionsMm, string name, Vector3 position)
    {
        var go = ElementFactory.CreatePart(dimensionsMm, name, position);
        _spawned.Add(go);
        return go;
    }

    [Test]
    public void GetVertices_SitsOverTheRealWalls_NotOverTheWorldOrigin()
    {
        SpawnClosedBoxOfFourLoadBearingWalls();
        var foundation = SpawnFoundationAtOrigin();

        var vertices = foundation.GetVertices();
        float minX = vertices.Min(v => v.x), maxX = vertices.Max(v => v.x);

        Assert.Greater(minX, 4f,
            "стены стоят на X от 5 до 9 м - короб ленты обязан лежать там же, а не на кубе "
            + "вокруг мирового начала координат, куда её поставил сайдбар (review-construction.md #1)");
        Assert.Less(maxX, 10f);
    }

    [Test]
    public void CabinetAtTheWorldOrigin_FarFromTheRealWalls_IsNotCaught()
    {
        SpawnClosedBoxOfFourLoadBearingWalls();
        SpawnFoundationAtOrigin();
        SpawnBox(new Vector3Int(400, 400, 400), "Cabinet", new Vector3(0f, -1.15f, 0f));

        var issues = SceneAnalyzer.Analyze();

        Assert.IsFalse(issues.Any(i => i.Code == IssueCatalog.CodeOverlap),
            "стены стоят на X 5..9, Z 5..8 - мировое начало координат к реальной ленте не "
            + "имеет отношения; куб 1x1x1 м вокруг ТРАНСФОРМА фундамента (Vector3.zero, как "
            + "его ставит сайдбар) раньше ловил здесь ложное пересечение");
    }

    [Test]
    public void CabinetInsideTheRealStrip_NearAWall_IsCaught()
    {
        SpawnClosedBoxOfFourLoadBearingWalls();
        SpawnFoundationAtOrigin();
        SpawnBox(new Vector3Int(200, 200, 200), "Cabinet", new Vector3(5f, -1.15f, 6.5f));

        var issues = SceneAnalyzer.Analyze();

        Assert.IsTrue(issues.Any(i => i.Code == IssueCatalog.CodeOverlap),
            "шкаф стоит ровно на осевой стены WallD (X=5, Z=6.5) - внутри реальной ленты "
            + "шириной 700 мм; куб вокруг Vector3.zero никогда её не видел, сколько бы "
            + "карcасов ни стояло прямо в ленте (review-construction.md #1)");
    }
}
