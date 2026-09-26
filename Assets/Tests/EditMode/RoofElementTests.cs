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
    public void ApplyDimensions_NoWallsYet_GetSpecItemsIsEmpty()
    {
        var go = ElementFactory.CreateRoof("Roof", Vector3.zero);
        _spawned.Add(go);
        var roof = go.GetComponent<RoofElement>();

        var items = new List<SpecItem>(roof.GetSpecItems(new List<KitchenElement>()));

        Assert.IsEmpty(items,
            "без единой стены RoofPitchPlanes.Build раньше всё равно строил каркас 1x1 м "
            + "из чистого свеса (footprint вырожден в точку) и ведомость показывала "
            + "покрытие/стропила/карниз/водосток фантомной крыши (review-construction.md #13); "
            + "пустой footprint обязан оставить и ведомость пустой, как уже пуст меш");
    }

    [Test]
    public void ApplyDimensions_FootprintNarrowInX_LongInZ_BoxIsNotTransposed()
    {
        SpawnWall(new Vector3Int(3000, 2700, 250), "WallA", new Vector3(1.5f, 1.35f, 0f));
        SpawnWall(new Vector3Int(250, 2700, 9000), "WallB", new Vector3(3f, 1.35f, 4.5f));
        SpawnWall(new Vector3Int(3000, 2700, 250), "WallC", new Vector3(1.5f, 1.35f, 9f));
        SpawnWall(new Vector3Int(250, 2700, 9000), "WallD", new Vector3(0f, 1.35f, 4.5f));

        var go = ElementFactory.CreateRoof("Roof", Vector3.zero);
        _spawned.Add(go);
        var roof = go.GetComponent<RoofElement>();

        var vertices = roof.GetVertices();
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
        foreach (var v in vertices)
        {
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.z < minZ) minZ = v.z;
            if (v.z > maxZ) maxZ = v.z;
        }

        Assert.Less(maxX - minX, maxZ - minZ,
            "footprint узкий по X (3 м) и длинный по Z (9 м) - конёк по Auto встаёт вдоль "
            + "длинной стороны (Z), а короб обязан остаться X-короткий/Z-длинный; раньше "
            + "Data.DimensionsMM писался как (span, rise, slope) без учёта того, вдоль какой "
            + "оси на самом деле лежит конёк, и короб выходил повёрнутым на 90° "
            + "(review-construction.md #14)");
    }

    [Test]
    public void Type_Setter_ClampsAnOutOfRangeValue_InsteadOfCrashingOnLoad()
    {
        SpawnClosedBoxOfFourLoadBearingWalls();
        var go = ElementFactory.CreateRoof("Roof", Vector3.zero);
        _spawned.Add(go);
        var roof = go.GetComponent<RoofElement>();

        Assert.DoesNotThrow(() => roof.Type = (RoofType)7,
            "повреждённый save с roofType:7 обязан осесть на ближайшем валидном значении, "
            + "как уже клампятся SoilKind/ConcreteGrade/SheetMark, а не уронить "
            + "SceneRestorer.Restore через RoofPitchPlanes.Build (review-construction.md #16)");
        Assert.IsTrue(System.Enum.IsDefined(typeof(RoofType), roof.Type));
    }

    [Test]
    public void RidgeAxis_Setter_ClampsAnOutOfRangeValue_InsteadOfCrashingOnLoad()
    {
        SpawnClosedBoxOfFourLoadBearingWalls();
        var go = ElementFactory.CreateRoof("Roof", Vector3.zero);
        _spawned.Add(go);
        var roof = go.GetComponent<RoofElement>();

        Assert.DoesNotThrow(() => roof.RidgeAxis = (RoofRidgeAxis)9);
        Assert.IsTrue(System.Enum.IsDefined(typeof(RoofRidgeAxis), roof.RidgeAxis));
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
