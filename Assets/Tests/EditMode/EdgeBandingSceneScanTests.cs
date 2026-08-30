using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Проход по сцене, которым EdgeBanding считает перекрытие торцов:
/// слепок граней, широкая фаза по габаритной сфере и список соседей, которые
/// торец НЕ закрывают.
///
/// Здесь живут причины, которые раньше были комментариями в EdgeBanding.cs:
/// техника и лампа торец не закрывают, слепок считает грани один раз, а сфера
/// строится по ГРАНЯМ — иначе широкая фаза теряет соседа, чьи грани стоят не
/// там, где его трансформ (опущенная стена в режиме обзора).</summary>
public class EdgeBandingSceneScanTests
{
    private const float U = AppConstants.MM_TO_UNITS;
    private static readonly Vector3Int ShelfDims = new Vector3Int(800, 18, 400);
    private const float ShelfEndX = 0.4f;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        PartRegistry.Clear();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
    }

    private KitchenElement CreatePart(string name, Vector3Int dims, Vector3 pos = default)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = dims;
        go.transform.position = pos;
        return element;
    }

    private KitchenElement Adopt(GameObject go)
    {
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private static Face MinusXFace(KitchenElement e) => e.GetFaces()[1];

    private static void SlideAgainstShelfEnd(KitchenElement e)
    {
        float dx = ShelfEndX - MinusXFace(e).center.x;
        e.transform.position += new Vector3(dx, 0f, 0f);
    }

    // --- Кто торец НЕ закрывает ---

    [Test]
    public void Lamp_Sink_Cooktop_Oven_AndDishwasher_AtTheEnd_LeaveTheEdge()
    {
        var makers = new (string name, System.Func<GameObject> make)[]
        {
            ("лампа", () => ElementFactory.CreateLightSource("Лампа", Vector3.zero)),
            ("мойка", () => ElementFactory.CreateSink("Мойка", Vector3.zero)),
            ("варочная", () => ElementFactory.CreateCooktop("Варочная", Vector3.zero)),
            ("духовка", () => ElementFactory.CreateOven("Духовка", Vector3.zero)),
            ("посудомойка", () => ElementFactory.CreateDishwasher("ПММ", Vector3.zero)),
        };

        foreach (var (name, make) in makers)
        {
            var shelf = CreatePart($"Shelf_{name}", ShelfDims);
            var neighbour = Adopt(make());
            SlideAgainstShelfEnd(neighbour);

            var coverage = EdgeBanding.Coverage(shelf,
                new List<KitchenElement> { shelf, neighbour });

            Assert.AreEqual(0f, coverage.Ratio(EdgeSide.W1), 1e-4f,
                $"{name} стоит вплотную к торцу, но закрытием не считается: "
                + "лампа — декор, техника врезана или открывается, и торец за ней виден");
            Assert.IsTrue(coverage.HasEdge(EdgeSide.W1), $"{name}: кромка на торце остаётся");
        }
    }

    [Test]
    public void PlainBoardWithTheSameFootprint_TakesTheEdgeAway()
    {
        var shelf = CreatePart("Shelf", ShelfDims);

        var sink = Adopt(ElementFactory.CreateSink("Мойка", Vector3.zero));
        SlideAgainstShelfEnd(sink);
        var sinkEnd = MinusXFace(sink);
        var probeDims = new Vector3Int(18,
            Mathf.RoundToInt(sinkEnd.size.x / U), Mathf.RoundToInt(sinkEnd.size.y / U));
        var probePos = sinkEnd.center + new Vector3(9f * U, 0f, 0f);
        Object.DestroyImmediate(sink.gameObject);

        var board = CreatePart("Доска", probeDims, probePos);

        float expected = Mathf.Min(1f, probeDims.y / (float)ShelfDims.y)
                       * Mathf.Min(1f, probeDims.z / (float)ShelfDims.z);

        var coverage = EdgeBanding.Coverage(shelf, new List<KitchenElement> { shelf, board });

        Assert.Greater(expected, 0f, "контрольная деталь обязана хоть чем-то накрывать торец");
        Assert.AreEqual(expected, coverage.Ratio(EdgeSide.W1), 1e-3f,
            "положительный контроль на ТОЙ ЖЕ геометрии: обычная деталь ровно на месте мойки "
            + "торец накрывает — значит предыдущий тест проверяет исключение, а не пустоту");
    }

    // --- Слепок сцены ---

    [Test]
    public void SceneSnapshot_HandsOutTheSameFaceArray_InsteadOfRebuildingIt()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        var side = CreatePart("Side", new Vector3Int(18, 700, 400), new Vector3(0.409f, 0f, 0f));

        Assert.AreNotSame(shelf.GetFaces(), shelf.GetFaces(),
            "GetFaces строит новый массив на каждый вызов — ради этого слепок и заведён");

        var scene = SceneFaces.Of(new List<KitchenElement> { shelf, side });

        Assert.AreEqual(2, scene.Count);
        Assert.AreSame(scene.FacesAt(0), scene.FacesAt(0),
            "грани каждой детали посчитаны ОДИН раз: перекрытие торцов попарно, и без "
            + "слепка проход по сцене звал GetFaces n² раз");
    }

    [Test]
    public void SceneSnapshot_AndRawList_AgreeOnCoverage()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        var side = CreatePart("Side", new Vector3Int(18, 700, 400), new Vector3(0.409f, 0f, 0f));
        var all = new List<KitchenElement> { shelf, side };

        var byList = EdgeBanding.Coverage(shelf, all);
        var bySnapshot = EdgeBanding.Coverage(shelf, SceneFaces.Of(all));

        foreach (EdgeSide s in System.Enum.GetValues(typeof(EdgeSide)))
            Assert.AreEqual(byList.Ratio(s), bySnapshot.Ratio(s), 1e-5f,
                $"{s}: слепок — только ускорение, ответ обязан быть тем же");
    }

    // --- Широкая фаза: сфера строится по граням ---

    [Test]
    public void BroadPhaseSphere_ContainsEveryFaceCorner_OfALoweredWall()
    {
        var wall = LoweredWall();
        var scene = SceneFaces.Of(new List<KitchenElement> { wall });
        var sphere = scene.SphereOf(wall);

        foreach (var f in wall.GetFaces())
            foreach (int sx in new[] { -1, 1 })
                foreach (int sy in new[] { -1, 1 })
                {
                    var corner = f.center + f.rightAxis * (sx * f.size.x * 0.5f)
                                          + f.upAxis * (sy * f.size.y * 0.5f);
                    Assert.LessOrEqual((corner - sphere.Center).magnitude, sphere.Radius + 1e-4f,
                        "сфера широкой фазы обязана накрывать ВСЕ грани детали: считать её по "
                        + "DimensionsMM вокруг трансформа нельзя — у опущенной стены грани "
                        + "остаются наверху, и сосед просто теряется");
                }
    }

    [Test]
    public void LoweredWall_StillTakesTheEdgeAway()
    {
        var wall = LoweredWall();
        var shelf = CreatePart("Shelf", ShelfDims, new Vector3(0f, 1.25f, 0f));

        var coverage = EdgeBanding.Coverage(shelf, new List<KitchenElement> { shelf, wall });

        Assert.IsFalse(coverage.HasEdge(EdgeSide.W1),
            "режим обзора опускает стену только ДЛЯ КАМЕРЫ — кромкование обязано считать "
            + "её по полной геометрии, иначе торец у стены вдруг получает кромку");
    }

    private KitchenElement LoweredWall()
    {
        var wallPart = CreatePart("Стена", new Vector3Int(100, 2500, 3000),
            new Vector3(0.45f, 1.25f, 0f));
        var wall = wallPart.gameObject.AddComponent<Wall>();
        wall.SetLowered(true, 0.2f);
        Assert.IsTrue(wall.IsLowered, "стена опущена");
        return wallPart;
    }

    // --- Повёрнутая деталь ---

    [Test]
    public void NeighbourTurnedAroundTheEndNormal_IsMeasuredByItsOwnAxes()
    {
        var shelf = CreatePart("Shelf", ShelfDims);

        var side = CreatePart("Side", new Vector3Int(18, 100, 700),
            new Vector3(0.409f, 0f, 0f));
        side.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var coverage = EdgeBanding.Coverage(shelf, new List<KitchenElement> { shelf, side });

        Assert.AreEqual(0.25f, coverage.Ratio(EdgeSide.W1), 1e-3f,
            "сосед повёрнут вокруг нормали торца: его 100×700 легли поперёк, и по глубине "
            + "торца остаётся 100 из 400 мм. Мерить перекрытие по размерам грани «как есть», "
            + "не проецируя её на оси торца, дало бы полное перекрытие и потерянную кромку");
        Assert.IsTrue(coverage.IsPartial(EdgeSide.W1), "перекрыт частично — это EDG-01");
    }
}
