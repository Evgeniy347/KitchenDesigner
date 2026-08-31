using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Врезная мойка: захват столешницы сверху и отрыв вниз, сквозной проём
/// в её пласти, пересчёт проёма при перемещении/ресайзе детали и упор в боковины.</summary>
public class SinkElementTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ProjectLoadStateGuard? _guard;

    [SetUp]
    public void SetUp() => _guard = ProjectLoadStateGuard.Capture();

    private const float ToU = AppConstants.MM_TO_UNITS;
    private const int TopThicknessMM = 38;
    /// <summary>Мировая высота верхней пласти столешницы, созданной CreateCountertop.</summary>
    private const float TopY = TopThicknessMM * 0.5f * ToU;

    [TearDown]
    public void TearDown()
    {
        _guard?.Restore();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    /// <summary>Столешница: деталь, положенная плашмя (локальная +Z смотрит вверх),
    /// центр в начале координат.</summary>
    private KitchenElement CreateCountertop(int widthMM = 1200, int depthMM = 600, string name = "Countertop")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        el.DimensionsMM = new Vector3Int(widthMM, depthMM, TopThicknessMM);
        PartRegistry.Register(el);
        return el;
    }

    /// <summary>Боковина: вертикальная деталь под столешницей.</summary>
    private KitchenElement CreateSidePanel(float x, string name = "Side")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        // Пласть смотрит вдоль X: боковина стоит поперёк столешницы.
        el.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        el.DimensionsMM = new Vector3Int(560, 700, 18);
        el.transform.position = new Vector3(x, -0.35f, 0f);
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

    /// <summary>Мойка, уже севшая на столешницу (заходит из полосы захвата).</summary>
    private SinkElement CreateSeatedSink(KitchenElement top, float localX = 0f)
    {
        var sink = CreateSink(new Vector3(localX, TopY + 0.05f, 0f));
        sink.SnapToPart();
        Assert.IsTrue(sink.IsAttached, "мойка должна сесть на столешницу");
        return sink;
    }

    // ── Размеры модели ──────────────────────────────────────────────────

    [Test]
    public void Sink_FitsSingle600Module()
    {
        // Мойка ≠ модуль: 600 мм тумбы минус боковины и запас на крепёж.
        Assert.Less(SinkElement.OUTER_WIDTH_MM, SinkElement.MODULE_WIDTH_MM);
        // Столешница одного модуля 600×600 обязана принимать мойку — иначе
        // на самой типовой тумбе мойка не находит хозяина и режет чужую деталь.
        Assert.LessOrEqual(SinkElement.MinPartWidthMM, SinkElement.MODULE_WIDTH_MM);
        Assert.LessOrEqual(SinkElement.MinPartDepthMM, SinkElement.MODULE_WIDTH_MM);
    }

    [Test]
    public void Cutout_IsSmallerThanRim_AndLargerThanBowl()
    {
        int bowlOuter = SinkElement.OUTER_WIDTH_MM - 2 * SinkElement.RIM_WIDTH_MM;

        Assert.Less(SinkElement.CutoutWidthMM, SinkElement.OUTER_WIDTH_MM,
            "борт обязан перекрывать срез столешницы");
        Assert.Greater(SinkElement.CutoutWidthMM, bowlOuter,
            "чаша должна проходить в проём");
        Assert.AreEqual(SinkElement.RIM_HEIGHT_MM + SinkElement.BOWL_DEPTH_MM,
            SinkElement.TotalHeightMM);
    }

    [Test]
    public void Sink_FitsSingleModuleCountertop()
    {
        var top = CreateCountertop(600, 600);
        Assert.IsTrue(SinkElement.IsSuitableHost(top), "столешница модуля 600 годится под мойку");

        var sink = CreateSeatedSink(top);
        Assert.IsTrue(top.HasCutout(sink), "на столешнице одного модуля мойка врезается");
    }

    // ── Реальный проект (docs/example.save.json) ────────────────────────

    /// <summary>Столешницу в проектах набирают не повёрнутой доской, а коробом:
    /// у Countertop_B габарит 2570×40×600 при НУЛЕВОМ повороте, то есть толщина
    /// лежит по локальной Y, а вовсе не по пласти ±Z. Мойка обязана врезаться и
    /// в такую деталь — на этой сцене вырез как раз и не появлялся.</summary>
    [Test]
    public void RealProject_Countertop_B_GetsCutOut()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var top = go.AddComponent<KitchenElement>();
        top.PartName = "Countertop_B";
        top.transform.SetPositionAndRotation(
            new Vector3(-0.300f, 0.840f, -3.320f), Quaternion.identity);
        top.DimensionsMM = new Vector3Int(2570, 40, 600);
        PartRegistry.Register(top);

        Assert.IsTrue(SinkElement.IsSuitableHost(top),
            "столешница-короб (толщина по Y) обязана годиться под мойку");
        Assert.AreEqual(1, SinkElement.HoleAxisFor(top), "резать её надо поперёк Y");

        var sink = CreateSink(new Vector3(0.7019751667976379f, 0.85999995470047f, -3.2985825538635256f));
        sink.transform.rotation = new Quaternion(0f, 1f, 0f, -4.371139e-8f);

        sink.SnapToPart();

        Assert.IsTrue(sink.IsAttached, "мойка садится на столешницу");
        Assert.IsTrue(top.HasCutout(sink));
        // Верх столешницы 840 + 20 = 860 мм — ровно там, где мойка и стояла.
        Assert.AreEqual(0.860f, sink.transform.position.y, 1e-4f);

        var mesh = top.GetComponent<MeshFilter>().sharedMesh;
        // Коробка без выреза — ровно 24 вершины (6 граней × 4).
        Assert.Greater(mesh.vertexCount, 24, "деталь получила меш с вырезом");
        // Дырка сквозная по ВЕРТИКАЛИ: пусто и сверху, и снизу плиты (|y| = 0.5).
        var rect = sink.CutoutRectIn(top);
        Assert.AreEqual(0, CountInsidePlane(mesh, rect, 0.5f, 0, 2), "верх прорезан");
        Assert.AreEqual(0, CountInsidePlane(mesh, rect, -0.5f, 0, 2), "низ прорезан");
    }

    // ── Захват и отрыв ──────────────────────────────────────────────────

    [Test]
    public void HoveringHigh_DoesNotAttach()
    {
        var top = CreateCountertop();
        var sink = CreateSink(new Vector3(0f, TopY + (SinkElement.SNAP_CATCH_MM + 50) * ToU, 0f));

        sink.SnapToPart();

        Assert.IsFalse(sink.IsAttached, "над столешницей мойка просто висит");
        Assert.AreEqual(0, top.AttachedCutouts.Count, "проём не режется");
    }

    [Test]
    public void LoweredFromAbove_AttachesOnce_ThenReleasesWhenPulledThrough()
    {
        var top = CreateCountertop();
        var sink = CreateSink(new Vector3(0f, TopY + 0.3f, 0f));
        sink.SnapToPart();
        Assert.IsFalse(sink.IsAttached, "с 300 мм над пластью — ещё не ловится");

        // Спускаем шагами по 50 мм: где-то в полосе захвата мойка садится.
        int attachedAtStep = -1;
        for (int step = 1; step <= 8 && attachedAtStep < 0; step++)
        {
            sink.transform.position -= new Vector3(0f, 0.05f, 0f);
            sink.SnapToPart();
            if (sink.IsAttached) attachedAtStep = step;
        }

        Assert.Greater(attachedAtStep, 0, "спускаясь сверху, мойка обязана прилипнуть");
        Assert.AreEqual(TopY, sink.transform.position.y, 1e-4f,
            "борт сел ровно на пласть");
        Assert.IsTrue(top.HasCutout(sink), "проём прорезан");
        Assert.Greater(top.GetComponent<MeshFilter>().sharedMesh.vertexCount, 24);

        // Продолжаем тянуть вниз — мойка держится, пока не пройден порог отрыва.
        sink.transform.position -= new Vector3(0f, SinkElement.SNAP_RELEASE_MM * 0.5f * ToU, 0f);
        sink.SnapToPart();
        Assert.IsTrue(sink.IsAttached, "на полпути к порогу мойка ещё держится");
        Assert.AreEqual(TopY, sink.transform.position.y, 1e-4f, "и снова притянута к пласти");

        sink.transform.position -= new Vector3(0f, SinkElement.SNAP_RELEASE_MM * ToU, 0f);
        sink.SnapToPart();

        Assert.IsFalse(sink.IsAttached, "протащили ниже порога — мойка отлипла");
        Assert.Less(sink.transform.position.y, TopY, "и ушла ниже пласти");
        Assert.AreEqual(0, top.AttachedCutouts.Count, "проём закрылся");
        Assert.AreEqual(24, top.GetComponent<MeshFilter>().sharedMesh.vertexCount,
            "меш снова простая коробка");

        // Дальше вниз она идёт свободно и обратно не прилипает.
        for (int step = 0; step < 3; step++)
        {
            sink.transform.position -= new Vector3(0f, 0.05f, 0f);
            sink.SnapToPart();
            Assert.IsFalse(sink.IsAttached, "ниже столешницы мойка больше не ловится");
        }
    }

    [Test]
    public void SinkBelowCountertop_DoesNotAttachFromUnderneath()
    {
        var top = CreateCountertop();
        var sink = CreateSink(new Vector3(0f, TopY - 0.4f, 0f));

        sink.SnapToPart();

        Assert.IsFalse(sink.IsAttached, "снизу мойка в столешницу не врезается");
    }

    [Test]
    public void SinkBesideCountertop_DoesNotAttach()
    {
        var top = CreateCountertop();
        // По высоте — в полосе захвата, но в стороне от детали.
        var sink = CreateSink(new Vector3(3f, TopY + 0.02f, 0f));

        sink.SnapToPart();

        Assert.IsFalse(sink.IsAttached, "мойка ловится только над самой столешницей");
    }

    // ── Пригодность детали ──────────────────────────────────────────────

    [Test]
    public void VerticalBoard_IsNotSuitableHost()
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
    public void TooSmallBoard_IsNotSuitableHost()
    {
        var small = CreateCountertop(SinkElement.MinPartWidthMM - 1, 600);
        Assert.IsFalse(SinkElement.IsSuitableHost(small),
            "проём не помещается — деталь под мойку не годится");
    }

    // ── Коллизии с боковинами ───────────────────────────────────────────

    [Test]
    public void CutoutBlocked_WhenSidePanelCrossesIt()
    {
        var top = CreateCountertop();
        CreateSidePanel(0f);

        var sink = CreateSink(new Vector3(0.6f, TopY + 0.02f, 0f));
        sink.SnapToPart();
        Assert.IsTrue(sink.IsAttached, "в стороне от боковины мойка садится");

        // Проём ровно над боковиной запрещён, рядом — разрешён.
        Assert.IsTrue(sink.CutoutBlocked(top, 0, 0), "боковина проходит сквозь проём");
        Assert.IsFalse(sink.CutoutBlocked(top, 500, 0), "правее боковины место свободно");
    }

    [Test]
    public void MovingSink_StopsAtSidePanel_InsteadOfCuttingThroughIt()
    {
        var top = CreateCountertop();
        CreateSidePanel(0f);
        var sink = CreateSeatedSink(top, 0.6f);
        int startOffset = sink.OffsetXMM;
        Assert.Greater(startOffset, 0);

        // Тянем мойку влево, прямо на боковину.
        for (int step = 0; step < 12; step++)
        {
            sink.transform.position -= new Vector3(0.05f, 0f, 0f);
            sink.SnapToPart();
        }

        Assert.IsTrue(sink.IsAttached, "мойка осталась на столешнице");
        Assert.Less(sink.OffsetXMM, startOffset, "подъехать к боковине мойка успела");
        // Боковина 18 мм стоит по центру столешницы: её правая пласть на +9 мм.
        // Левый край проёма не имеет права зайти за неё — иначе вырез рассёк бы
        // боковину пополам. Считаем по сырой геометрии, а не через CutoutBlocked,
        // чтобы тест ловил и поломку самой проверки.
        float cutoutLeftMM = sink.OffsetXMM - SinkElement.CutoutWidthMM * 0.5f;
        Assert.GreaterOrEqual(cutoutLeftMM, 9f - 1f,
            $"проём дошёл до {cutoutLeftMM} мм и рассёк боковину");
    }

    // ── Проём ───────────────────────────────────────────────────────────

    [Test]
    public void MovingPart_CarriesSinkAlong()
    {
        var top = CreateCountertop();
        var sink = CreateSeatedSink(top, 0.2f);

        var before = sink.transform.position;
        int offsetBefore = sink.OffsetXMM;

        var delta = new Vector3(1.5f, 0f, 0.7f);
        top.transform.position += delta;
        sink.SnapToPart();

        Assert.AreEqual(offsetBefore, sink.OffsetXMM, "смещение относительно детали не меняется");
        Assert.AreEqual(before + delta, sink.transform.position);
    }

    [Test]
    public void CutoutRect_KeepsAbsoluteSize_WhenPartResized()
    {
        var top = CreateCountertop();
        var sink = CreateSeatedSink(top);

        var before = sink.CutoutRectIn(top);
        Assert.AreEqual(SinkElement.CutoutWidthMM, (before.xMax - before.xMin) * 1200, 0.01f);

        top.DimensionsMM = new Vector3Int(2000, 600, TopThicknessMM);
        sink.SnapToPart();

        var after = sink.CutoutRectIn(top);
        Assert.AreEqual(SinkElement.CutoutWidthMM, (after.xMax - after.xMin) * 2000, 0.01f,
            "проём остаётся прежним в мм — меняется его ДОЛЯ от новой ширины");
        Assert.Less(after.xMax - after.xMin, before.xMax - before.xMin);
    }

    [Test]
    public void PartMesh_HasThroughHole_UnderSink()
    {
        var top = CreateCountertop();
        var sink = CreateSeatedSink(top);

        var mesh = top.GetComponent<MeshFilter>().sharedMesh;
        var rect = sink.CutoutRectIn(top);

        Assert.Greater(mesh.vertexCount, 24, "деталь получила собственный меш с вырезом");
        Assert.AreEqual(0, CountInsideFace(mesh, rect, 0.5f), "лицевая пласть прорезана");
        Assert.AreEqual(0, CountInsideFace(mesh, rect, -0.5f), "задняя пласть прорезана");
        AssertHasBoundaryVertex(mesh, rect);
    }

    [Test]
    public void PartMesh_ReturnsToPlainBox_WhenSinkRemoved()
    {
        var top = CreateCountertop();
        var sink = CreateSeatedSink(top);
        Assert.Greater(top.GetComponent<MeshFilter>().sharedMesh.vertexCount, 24);

        top.UnregisterCutout(sink);

        Assert.IsFalse(top.HasCutout(sink));
        Assert.AreEqual(24, top.GetComponent<MeshFilter>().sharedMesh.vertexCount,
            "без мойки и пазов деталь возвращается на простую коробку");
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
    private static int CountInsideFace(Mesh mesh, GrooveMesh.Rect2 rect, float z) =>
        CountInsidePlane(mesh, rect, z, 0, 1);

    /// <summary>То же для детали, у которой проём режется поперёк другой оси:
    /// faceAxis задаётся неявно (та, что не a и не b), a/b — оси плоскости выреза.</summary>
    private static int CountInsidePlane(Mesh mesh, GrooveMesh.Rect2 rect, float faceCoord, int a, int b)
    {
        int faceAxis = 3 - a - b;
        int count = 0;
        foreach (var v in mesh.vertices)
            if (Mathf.Abs(v[faceAxis] - faceCoord) < 1e-4f &&
                v[a] > rect.xMin + 1e-4f && v[a] < rect.xMax - 1e-4f &&
                v[b] > rect.yMin + 1e-4f && v[b] < rect.yMax - 1e-4f)
                count++;
        return count;
    }

    // ── Габарит для ручек ───────────────────────────────────────────────

    [Test]
    public void HandleBox_IsRimPlateAboveSurface_NotBuriedBowl()
    {
        var top = CreateCountertop();
        var sink = CreateSeatedSink(top);

        // Ручки строятся из GetFaces: центры боковых граней должны лежать НАД
        // пластью, иначе стрелки замурованы в столешнице.
        foreach (var f in sink.GetFaces())
            Assert.GreaterOrEqual(f.center.y, TopY - 1e-4f,
                "грань габарита ушла под столешницу");

        // И это плита: тонкая ось — вертикаль, значит общий HandlePlacement
        // вынесет боковые ручки из плоскости, как у стены и полки.
        var box = HandlePlacement.BoxOf(sink.GetFaces());
        Assert.AreEqual(1, HandlePlacement.ThinAxis(box), "тонкая ось габарита — Y");
    }

    // ── Сериализация ────────────────────────────────────────────────────

    [Test]
    public void FromElement_Sink_StoresHostAndOffsets()
    {
        var top = CreateCountertop();
        var sink = CreateSeatedSink(top, 0.2f);

        var d = ElementCapture.FromElement(sink);

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
        var sink = CreateSeatedSink(top);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { top, sink });

        Assert.IsFalse(result.violations.Contains(sink),
            "мойка врезана в столешницу — это не пересечение");
    }
}
