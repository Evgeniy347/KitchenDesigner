using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>R4/R5: RoofElement finds the load-bearing walls on the TOP level through
/// PartRegistry, wraps them in a bounding RoofFootprint (RoofContour) and builds the pitch
/// planes over it - the same shape as SceneFoundationAnalysisTests, proving the wiring rather
/// than the pure math (already covered by RoofPitchPlanesTests/RoofFrameGeometryTests).</summary>
public class RoofElementTests
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

    private static float MinY(Vector3[] vertices)
    {
        float min = float.PositiveInfinity;
        foreach (var v in vertices) if (v.y < min) min = v.y;
        return min;
    }

    private void SpawnClosedBoxOfFourLoadBearingWalls()
    {
        SpawnWall(new Vector3Int(4000, 2700, 250), "WallA", new Vector3(2f, 1.35f, 0f));
        SpawnWall(new Vector3Int(250, 2700, 3000), "WallB", new Vector3(4f, 1.35f, 1.5f));
        SpawnWall(new Vector3Int(4000, 2700, 250), "WallC", new Vector3(2f, 1.35f, 3f));
        SpawnWall(new Vector3Int(250, 2700, 3000), "WallD", new Vector3(0f, 1.35f, 1.5f));
    }

    [Test]
    public void ApplyDimensions_BuildsAFrameFromTheWallBoundingBox_AndSitsOnTopOfTheWalls()
    {
        SpawnClosedBoxOfFourLoadBearingWalls();

        var go = ElementFactory.CreateRoof("Roof", Vector3.zero);
        _spawned.Add(go);
        var roof = go.GetComponent<RoofElement>();
        Assert.IsNotNull(roof);

        Assert.IsNotEmpty(roof.BuiltFrame.Planes,
            "четыре несущие стены дают непустой ограничивающий прямоугольник - RoofFrame "
            + "обязан содержать хотя бы одну плоскость");

        float levelTopY = KitchenSettings.Instance.ConstructionFloorHeightMm * AppConstants.MM_TO_UNITS;
        float boxBottomY = MinY(roof.GetVertices());
        Assert.AreEqual(levelTopY, boxBottomY, 0.01f,
            "карниз крыши (низ её ЛОГИЧЕСКОГО короба, тот же, что видит GetVerticesAt для "
            + "проверки опоры COL-02) обязан сидеть РОВНО на верхней отметке ЭТАЖА "
            + "(floorElevationMm + heightMm уровня, дефолт ConstructionFloorHeightMm), а не "
            + "на полу и не на высоте конкретной стены (у стены своя, отдельная высота "
            + "2700 мм) - иначе крыша тонет в стенах, парит не на той отметке, или её "
            + "логический короб не касается стен и COL-01/COL-02 считает её висящей");

        Assert.AreEqual(2f, go.transform.position.x, 0.2f,
            "центр крыши по X обязан лежать над центром коробки стен (x от 0 до 4)");
        Assert.AreEqual(1.5f, go.transform.position.z, 0.2f,
            "центр крыши по Z обязан лежать над центром коробки стен (z от 0 до 3)");

        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        Assert.Greater(mesh.vertexCount, 0, "меш обязан построиться, а не остаться пустым");
    }

    [Test]
    public void ApplyDimensions_NoWallsYet_BuildsAnEmptyMesh_NotACrash()
    {
        var go = ElementFactory.CreateRoof("Roof", Vector3.zero);
        _spawned.Add(go);
        var roof = go.GetComponent<RoofElement>();

        Assert.IsNotNull(roof);
        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        Assert.AreEqual(0, mesh.vertexCount,
            "без единой стены ограничивающий прямоугольник вырожден в точку - крыша не "
            + "падает, а остаётся пустым мешем, как лента фундамента без стен");
    }

    [Test]
    public void PitchDeg_Setter_RebuildsTheMesh_AndTheRidgeRisesWithTheAngle()
    {
        SpawnClosedBoxOfFourLoadBearingWalls();
        var go = ElementFactory.CreateRoof("Roof", Vector3.zero);
        _spawned.Add(go);
        var roof = go.GetComponent<RoofElement>();

        float BoundsHeight() => go.GetComponent<MeshFilter>().sharedMesh.bounds.size.y;

        roof.PitchDeg = 10f;
        float lowPitchHeight = BoundsHeight();

        roof.PitchDeg = 45f;
        float highPitchHeight = BoundsHeight();

        Assert.Greater(highPitchHeight, lowPitchHeight,
            "круче уклон - выше конёк над карнизом; если меш не перестраивается на "
            + "PitchDeg, обе высоты совпадут");
    }
}
