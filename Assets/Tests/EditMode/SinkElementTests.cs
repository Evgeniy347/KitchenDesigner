using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Врезная мойка: привязка к детали-столешнице, сквозной проём в её
/// пласти и пересчёт проёма при перемещении/ресайзе детали.</summary>
public class SinkElementTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    /// <summary>Столешница: деталь, положенная плашмя (локальная +Z смотрит вверх).</summary>
    private KitchenElement CreateCountertop(int widthMM = 1200, int depthMM = 600, int thicknessMM = 38)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = "Countertop";
        el.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        el.DimensionsMM = new Vector3Int(widthMM, depthMM, thicknessMM);
        PartRegistry.Register(el);
        return el;
    }

    private SinkElement CreateSink(Vector3 position)
    {
        var go = new GameObject("Sink");
        _spawned.Add(go);
        go.transform.position = position;
        var sink = go.AddComponent<SinkElement>();
        sink.PartName = "Sink";
        sink.DimensionsMM = new Vector3Int(
            SinkElement.OUTER_WIDTH_MM, SinkElement.TotalHeightMM, SinkElement.OUTER_DEPTH_MM);
        PartRegistry.Register(sink);
        return sink;
    }

    // ── Размеры модели ──────────────────────────────────────────────────

    [Test]
    public void Cutout_IsSmallerThanRim_AndLargerThanBowl()
    {
        int bowlOuter = SinkElement.OUTER_WIDTH_MM - 2 * SinkElement.RIM_WIDTH_MM;

        Assert.Less(SinkElement.CutoutWidthMM, SinkElement.OUTER_WIDTH_MM,
            "борт обязан перекрывать срез столешницы");
        Assert.Greater(SinkElement.CutoutWidthMM, bowlOuter,
            "чаша должна проходить в проём");
        Assert.AreEqual(560, SinkElement.CutoutWidthMM);
        Assert.AreEqual(460, SinkElement.CutoutDepthMM);
        Assert.AreEqual(SinkElement.RIM_HEIGHT_MM + SinkElement.BOWL_DEPTH_MM,
            SinkElement.TotalHeightMM);
    }

    // ── Привязка ────────────────────────────────────────────────────────

    [Test]
    public void SnapToPart_HorizontalBoard_AttachesAndSitsOnTopFace()
    {
        var top = CreateCountertop();
        var sink = CreateSink(new Vector3(0.1f, 0.5f, 0f));

        sink.SnapToPart();

        Assert.IsTrue(top.HasSink(sink), "мойка врезана в столешницу");
        Assert.AreEqual("Countertop", sink.AttachedPartName);
        // Борт лежит на верхней пласти: начало координат мойки = плоскость среза.
        float topY = 38 * 0.5f * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(topY, sink.transform.position.y, 1e-4f);
    }

    [Test]
    public void SnapToPart_VerticalBoard_NotSuitableHost()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var upright = go.AddComponent<KitchenElement>();
        upright.PartName = "Side";
        upright.DimensionsMM = new Vector3Int(1200, 600, 18); // стойка: пласть вертикальна
        PartRegistry.Register(upright);

        Assert.IsFalse(SinkElement.IsSuitableHost(upright), "мойку врезают в столешницу, не в стойку");
    }

    [Test]
    public void SnapToPart_TooSmallBoard_NotSuitableHost()
    {
        var small = CreateCountertop(SinkElement.MinPartWidthMM - 1, 600);
        Assert.IsFalse(SinkElement.IsSuitableHost(small),
            "проём не помещается — деталь под мойку не годится");
    }

    [Test]
    public void SnapToPart_ClampsSinkInsidePart()
    {
        var top = CreateCountertop();
        // Утаскиваем мойку далеко вправо за пределы столешницы.
        var sink = CreateSink(new Vector3(5f, 0.5f, 0f));

        sink.SnapToPart();

        int maxX = (1200 - SinkElement.CutoutWidthMM) / 2 - SinkElement.MIN_EDGE_MM;
        Assert.AreEqual(maxX, sink.OffsetXMM, "смещение упирается в край столешницы");

        var rect = sink.CutoutRectIn(top);
        Assert.LessOrEqual(rect.xMax, 0.5f);
        Assert.GreaterOrEqual(rect.xMin, -0.5f);
    }

    [Test]
    public void MovingPart_CarriesSinkAlong()
    {
        var top = CreateCountertop();
        var sink = CreateSink(new Vector3(0.2f, 0.5f, 0f));
        sink.SnapToPart();

        var before = sink.transform.position;
        int offsetBefore = sink.OffsetXMM;

        var delta = new Vector3(1.5f, 0f, 0.7f);
        top.transform.position += delta;
        sink.SnapToPart();

        Assert.AreEqual(offsetBefore, sink.OffsetXMM, "смещение относительно детали не меняется");
        Assert.AreEqual(before + delta, sink.transform.position);
    }

    // ── Проём ───────────────────────────────────────────────────────────

    [Test]
    public void CutoutRect_KeepsAbsoluteSize_WhenPartResized()
    {
        var top = CreateCountertop();
        var sink = CreateSink(new Vector3(0f, 0.5f, 0f));
        sink.SnapToPart();

        var before = sink.CutoutRectIn(top);
        Assert.AreEqual(SinkElement.CutoutWidthMM, (before.xMax - before.xMin) * 1200, 0.01f);

        top.DimensionsMM = new Vector3Int(2000, 600, 38);
        sink.SnapToPart();

        var after = sink.CutoutRectIn(top);
        Assert.AreEqual(SinkElement.CutoutWidthMM, (after.xMax - after.xMin) * 2000, 0.01f,
            "проём остаётся 560 мм — меняется его ДОЛЯ от новой ширины");
        Assert.Less(after.xMax - after.xMin, before.xMax - before.xMin);
    }

    [Test]
    public void PartMesh_HasThroughHole_UnderSink()
    {
        var top = CreateCountertop();
        var sink = CreateSink(new Vector3(0f, 0.5f, 0f));
        sink.SnapToPart();

        var mesh = top.GetComponent<MeshFilter>().sharedMesh;
        var rect = sink.CutoutRectIn(top);

        Assert.AreNotEqual("Cube", mesh.name, "деталь получила собственный меш с вырезом");
        // Вырез сквозной: пусто и на лицевой, и на задней пласти.
        Assert.AreEqual(0, CountInsideFace(mesh, rect, 0.5f), "лицевая пласть прорезана");
        Assert.AreEqual(0, CountInsideFace(mesh, rect, -0.5f), "задняя пласть прорезана");
        AssertHasBoundaryVertex(mesh, rect);
    }

    [Test]
    public void PartMesh_ReturnsToPlainBox_WhenSinkRemoved()
    {
        var top = CreateCountertop();
        var sink = CreateSink(new Vector3(0f, 0.5f, 0f));
        sink.SnapToPart();
        Assert.IsTrue(top.HasSink(sink));
        Assert.AreNotEqual("Cube", top.GetComponent<MeshFilter>().sharedMesh.name);

        top.UnregisterSink(sink);

        Assert.IsFalse(top.HasSink(sink));
        Assert.AreEqual("Cube", top.GetComponent<MeshFilter>().sharedMesh.name,
            "без мойки и пазов деталь возвращается на встроенный куб");
    }

    private static void AssertHasBoundaryVertex(Mesh mesh, GrooveMesh.Rect2 rect)
    {
        foreach (var v in mesh.vertices)
            if (Mathf.Abs(v.x - rect.xMin) < 1e-4f &&
                v.y >= rect.yMin - 1e-4f && v.y <= rect.yMax + 1e-4f)
                return;
        Assert.Fail("нет вершин по границе проёма — вырез не построен");
    }

    /// <summary>Вершины пласти z, лежащие строго внутри проёма.</summary>
    private static int CountInsideFace(Mesh mesh, GrooveMesh.Rect2 rect, float z)
    {
        int count = 0;
        foreach (var v in mesh.vertices)
            if (Mathf.Abs(v.z - z) < 1e-4f &&
                v.x > rect.xMin + 1e-4f && v.x < rect.xMax - 1e-4f &&
                v.y > rect.yMin + 1e-4f && v.y < rect.yMax - 1e-4f)
                count++;
        return count;
    }

    // ── Сериализация ────────────────────────────────────────────────────

    [Test]
    public void FromElement_Sink_StoresHostAndOffsets()
    {
        var top = CreateCountertop();
        var sink = CreateSink(new Vector3(0.2f, 0.5f, 0.05f));
        sink.SnapToPart();

        var d = ElementData.FromElement(sink);

        Assert.IsTrue(d.isSink);
        Assert.IsFalse(d.isPillar);
        Assert.AreEqual(top.PartName, d.sinkAttachedPartName);
        Assert.AreEqual(sink.OffsetXMM, d.sinkOffsetXMM);
        Assert.AreEqual(sink.OffsetYMM, d.sinkOffsetYMM);
    }

    [Test]
    public void Validator_SinkInCountertop_NoViolation()
    {
        var top = CreateCountertop();
        var sink = CreateSink(new Vector3(0f, 0.5f, 0f));
        sink.SnapToPart();

        var result = ConstraintValidator.Validate(new List<KitchenElement> { top, sink });

        Assert.IsFalse(result.violations.Contains(sink),
            "мойка врезана в столешницу — это не пересечение");
    }
}
