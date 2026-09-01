using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Measure;

/// <summary>
/// Реакция рулетки на луч, упирающийся в деталь/стену/пол (без близкой
/// вершины). PlaneHint — розовая подсказка того же цвета, что и вершинный
/// Hint; первый ЛКМ ставит туда якорь, второй — фиксирует осевую проекцию
/// от якоря. Геометрия рейкаста — реальный Physics (как у SelectionManager
/// и EyedropperController), поэтому и тесты интеграционные, с коллайдерами.
/// </summary>
public class MeasurePlaneHitTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private Camera? _camera;
    private MeasureController? _controller;

    [SetUp]
    public void SetUp()
    {
        MeasureMode.Reset();
        PartRegistry.Clear();

        var camGo = new GameObject("MeasureTestCam");
        camGo.tag = "MainCamera";
        _camera = camGo.AddComponent<Camera>();
        _camera.transform.position = new Vector3(0f, 0f, -10f);
        _camera.transform.LookAt(Vector3.zero);
        _spawned.Add(camGo);

        var ctrlGo = new GameObject("MeasureController");
        _controller = ctrlGo.AddComponent<MeasureController>();
        _spawned.Add(ctrlGo);
    }

    [TearDown]
    public void TearDown()
    {
        MeasureMode.Reset();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        _camera = null;
        _controller = null;
    }

    // Куб 1×1×1 в мировом (0,0,0); камера смотрит вдоль +Z, экранный центр
    // бьёт ровно в переднюю грань z=-0.5.
    private GameObject MakeCubeAt(string name, Vector3 worldPos, float sizeMeters = 1f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = worldPos;
        go.transform.localScale = new Vector3(sizeMeters, sizeMeters, sizeMeters);
        _spawned.Add(go);
        return go;
    }

    // ScreenPointToRay требует координат в пикселях; берём центр игрового вида.
    private Vector2 ScreenCenter()
    {
        var cam = _camera!;
        return new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f);
    }

    // ── PlaneHint: рейкаст по деталям/стенам/полу ────────────────────────

    [Test]
    public void UpdatePlaneHit_RayHitsBoxCollider_SetsPlaneHintAtFrontFace()
    {
        MakeCubeAt("Box", Vector3.zero);
        Physics.SyncTransforms();

        _controller!.UpdatePlaneHit(_camera!, ScreenCenter());

        Assert.IsTrue(_controller.PlaneHint.HasValue,
            "Луч из центра экрана попал в куб — PlaneHint обязан быть заполнен");
        Assert.AreEqual(-0.5f, _controller.PlaneHint!.Value.z, 0.001f,
            "Точка хита — передняя грань куба, z=-0.5");
    }

    [Test]
    public void UpdatePlaneHit_NoGeometryInScene_LeavesPlaneHintNull()
    {
        Physics.SyncTransforms();

        _controller!.UpdatePlaneHit(_camera!, ScreenCenter());

        Assert.IsNull(_controller.PlaneHint,
            "В пустой сцене луч уходит в пустоту — PlaneHint остаётся null");
    }

    [Test]
    public void UpdatePlaneHit_RayHitsBasePlate_DoesNotSetPlaneHint()
    {
        // BasePlate — техническая подложка (см. CollectVertices, там же фильтр).
        // Создаём руками, не через BasePlate.Create, чтобы не зависеть от URP-шейдера.
        var go = MakeCubeAt("BasePlate", Vector3.zero);
        go.AddComponent<KitchenElement>();
        go.AddComponent<BasePlate>();
        Physics.SyncTransforms();

        _controller!.UpdatePlaneHit(_camera!, ScreenCenter());

        Assert.IsNull(_controller.PlaneHint,
            "BasePlate — служебная плита, замер к ней привязывать нельзя");
    }

    [Test]
    public void UpdatePlaneHit_AfterHit_LeavingGeometryClearsHintNextFrame()
    {
        MakeCubeAt("Box", Vector3.zero);
        Physics.SyncTransforms();

        // Кадр 1: луч в куб.
        _controller!.UpdatePlaneHit(_camera!, ScreenCenter());
        Assert.IsTrue(_controller.PlaneHint.HasValue, "хит → PlaneHint");

        // Кадр 2: тот же кадр, но куб убрали — UpdatePlaneHit обязан обнулить,
        // иначе застрявшая подсказка будет указывать на уже несуществующую точку.
        foreach (var s in _spawned)
            if (s != null && s.name == "Box") Object.DestroyImmediate(s);
        Physics.SyncTransforms();
        _controller.UpdatePlaneHit(_camera!, ScreenCenter());

        Assert.IsNull(_controller.PlaneHint, "после исчезновения геометрии PlaneHint обнуляется");
    }

    // ── UpdatePreview: осевая проекция хита, когда вершины рядом нет ─────

    [Test]
    public void UpdatePreview_AnchorSetPlaneHitOnWall_PreviewEndIsAxisProjection()
    {
        // Якорь (0,1,0), хит в стену (1.3, 0.8, 0.2). Доминирующая ось X →
        // PreviewEnd = (1.3, 1, 0). Так пунктир остаётся осевым и подпись не
        // получает глиф ∠.
        _controller!.Anchor = new Vector3(0f, 1f, 0f);
        _controller.PlaneHint = new Vector3(1.3f, 0.8f, 0.2f);

        _controller.UpdatePreview(_camera!, Vector2.zero);

        Assert.IsTrue(_controller.PreviewEnd.HasValue);
        Assert.AreEqual(new Vector3(1.3f, 1f, 0f), _controller.PreviewEnd!.Value);
    }

    [Test]
    public void UpdatePreview_VertexHintTakesPriorityOverPlaneHint()
    {
        // Если курсор и у вершины, и луч в стену — побеждает вершина
        // (она точнее, геометрия привязки конкретнее).
        _controller!.Anchor = new Vector3(0f, 1f, 0f);
        _controller.PlaneHint = new Vector3(1.3f, 0.8f, 0.2f);
        Vector3 vertex = new Vector3(0.7f, 0.9f, 0.1f);
        _controller!.Hint = vertex;

        _controller.UpdatePreview(_camera!, Vector2.zero);

        Assert.AreEqual(vertex, _controller.PreviewEnd!.Value);
    }

    // ── HandleClick: первый клик → якорь на хите, второй → фиксация ─────

    [Test]
    public void HandleClick_FirstClickOnPlane_SetsAnchorAtHitPoint()
    {
        MeasureMode.SetActive(true);
        _controller!.PlaneHint = new Vector3(0.5f, 0.7f, 0.9f);

        _controller.HandleClick();

        Assert.IsTrue(_controller.Anchor.HasValue, "первый ЛКМ по стене ставит якорь");
        Assert.AreEqual(new Vector3(0.5f, 0.7f, 0.9f), _controller.Anchor!.Value,
            "первый клик: якорь в точке хита, не в осевой проекции (проекции ещё не от чего считать)");
        Assert.IsNull(_controller.PreviewEnd, "PreviewEnd сбрасывается вместе с якорем");
    }

    [Test]
    public void HandleClick_SecondClickOnPlane_CommitsAxisAlignedSegmentFromAnchor()
    {
        MeasureMode.SetActive(true);
        _controller!.Anchor = new Vector3(0f, 1f, 0f);
        _controller.PlaneHint = new Vector3(1.3f, 0.8f, 0.2f);
        // UpdatePreview в реальном Update идёт перед HandleClick — повторяем порядок.
        _controller.UpdatePreview(_camera!, Vector2.zero);

        int before = MeasureStore.Segments.Count;
        _controller.HandleClick();

        Assert.AreEqual(before + 1, MeasureStore.Segments.Count,
            "второй ЛКМ по хиту в стену фиксирует замер");
        var seg = MeasureStore.Segments[MeasureStore.Segments.Count - 1];
        Assert.AreEqual(new Vector3(0f, 1f, 0f), seg.A);
        Assert.AreEqual(new Vector3(1.3f, 1f, 0f), seg.B,
            "второй конец — осевая проекция хита, не сам хит");
        Assert.AreEqual(0, seg.Axis,
            "замер остаётся осевым (X) — подпись без глифа ∠");
        Assert.IsNull(_controller.Anchor, "после фиксации якорь сброшен");
        Assert.IsNull(_controller.PreviewEnd, "после фиксации превью сброшено");
    }

    [Test]
    public void HandleClick_VertexHintTakesPriorityOverPlaneHintOnSecondClick()
    {
        // Если у пользователя оба типа подсказки одновременно — выигрывает
        // вершина (как точная привязка к геометрии детали).
        MeasureMode.SetActive(true);
        _controller!.Anchor = new Vector3(0f, 1f, 0f);
        _controller.PlaneHint = new Vector3(1.3f, 0.8f, 0.2f);
        Vector3 vertex = new Vector3(0.7f, 0.9f, 0.1f);
        _controller!.Hint = vertex;
        _controller.UpdatePreview(_camera!, Vector2.zero);

        _controller.HandleClick();

        var seg = MeasureStore.Segments[MeasureStore.Segments.Count - 1];
        Assert.AreEqual(vertex, seg.B, "вершина выигрывает у хита в стену");
    }

    [Test]
    public void HandleClick_SecondClickMissesEverything_CancelsAnchor()
    {
        // Совместимость со старым поведением: клик в пустоту при выставленном
        // якоре сбрасывает якорь без фиксации.
        MeasureMode.SetActive(true);
        _controller!.Anchor = new Vector3(0f, 1f, 0f);

        int before = MeasureStore.Segments.Count;
        _controller.HandleClick();

        Assert.AreEqual(before, MeasureStore.Segments.Count, "в пустоту — нет замера");
        Assert.IsNull(_controller.Anchor, "якорь сброшен");
    }

    private KitchenElement MakeMeasurableElement(string name, Vector3 worldPos, int sizeMM = 1000)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = new Vector3Int(sizeMM, sizeMM, sizeMM);
        go.transform.position = worldPos;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    // ── Esc отменяет ровно одно ─────────────────────────────────────────

    [Test]
    public void CancelOneStep_WithAnUnfinishedMeasure_DropsTheAnchorAndNothingElse()
    {
        MeasureMode.SetActive(true);
        var seg = new MeasureSegment(Vector3.zero, Vector3.up);
        MeasureStore.Add(seg);
        MeasureStore.Select(seg);
        _controller!.Anchor = new Vector3(0f, 1f, 0f);

        _controller.CancelOneStep();

        Assert.IsNull(_controller.Anchor, "первым Esc отменяет незавершённый замер");
        Assert.AreSame(seg, MeasureStore.Selected,
            "и только его: выбранный отрезок обязан пережить эту отмену, иначе одно "
            + "нажатие съедало бы два шага сразу");
        Assert.IsTrue(MeasureMode.Active, "и режим тоже остаётся включённым");
    }

    [Test]
    public void CancelOneStep_WithNoAnchorButASelectedSegment_OnlyDropsTheSelection()
    {
        MeasureMode.SetActive(true);
        var seg = new MeasureSegment(Vector3.zero, Vector3.up);
        MeasureStore.Add(seg);
        MeasureStore.Select(seg);

        _controller!.CancelOneStep();

        Assert.IsNull(MeasureStore.Selected,
            "незавершённого замера нет — Esc закрывает окно свойств отрезка");
        Assert.IsTrue(MeasureMode.Active, "но из режима не выбрасывает");
        Assert.AreEqual(1, MeasureStore.Segments.Count, "и сам замер не удаляет");
    }

    [Test]
    public void CancelOneStep_WithNothingPending_LeavesTheMode()
    {
        MeasureMode.SetActive(true);
        Assume.That(_controller!.Anchor, Is.Null, "предусловие: незавершённого замера нет");
        Assume.That(MeasureStore.Selected, Is.Null, "предусловие: ничего не выбрано");

        _controller.CancelOneStep();

        Assert.IsFalse(MeasureMode.Active,
            "отменять больше нечего — только теперь Esc выходит из режима целиком");
    }

    // ── Что попадает в кандидаты для привязки ───────────────────────────

    [Test]
    public void CollectVertices_TakesOnlyVisibleContent()
    {
        MakeMeasurableElement("Деталь", Vector3.zero);
        _controller!.CollectVertices(_camera!);
        int alone = _controller.CandidateVerticesWorld.Count;
        Assert.Greater(alone, 0, "предусловие: у обычной детали вершины есть");

        var plateGo = new GameObject("BasePlate");
        plateGo.AddComponent<MeshFilter>();
        plateGo.AddComponent<MeshRenderer>();
        var plate = plateGo.AddComponent<KitchenElement>();
        plate.PartName = "BasePlate";
        plate.DimensionsMM = new Vector3Int(3000, 18, 3000);
        plateGo.AddComponent<BasePlate>();
        PartRegistry.Register(plate);
        _spawned.Add(plateGo);

        var hidden = MakeMeasurableElement("Скрытая", new Vector3(2f, 0f, 0f));
        hidden.GetComponentInChildren<MeshRenderer>().enabled = false;

        var off = MakeMeasurableElement("Выключенная", new Vector3(-2f, 0f, 0f));
        off.gameObject.SetActive(false);

        _controller.CollectVertices(_camera!);

        Assert.AreEqual(alone, _controller.CandidateVerticesWorld.Count,
            "опорная плита — техническая подложка, скрытая и выключенная деталь на "
            + "экране не видны: привязываться к тому, чего не видно, нельзя");
    }

    [Test]
    public void CollectVertices_ASecondVisibleElement_DoesAddItsVertices()
    {
        MakeMeasurableElement("Первая", Vector3.zero);
        _controller!.CollectVertices(_camera!);
        int one = _controller.CandidateVerticesWorld.Count;

        MakeMeasurableElement("Вторая", new Vector3(2f, 0f, 0f));
        _controller.CollectVertices(_camera!);

        Assert.AreEqual(2 * one, _controller.CandidateVerticesWorld.Count,
            "положительный контроль к предыдущему тесту: фильтр отсекает служебное, "
            + "а не всё подряд");
    }

    [Test]
    public void CollectVertices_ReusesItsListsBetweenFrames()
    {
        MakeMeasurableElement("Деталь", Vector3.zero);

        _controller!.CollectVertices(_camera!);
        var firstFrame = _controller.CandidateVerticesWorld;
        _controller.CollectVertices(_camera!);

        Assert.AreSame(firstFrame, _controller.CandidateVerticesWorld,
            "кандидаты пересобираются КАЖДЫЙ кадр; свежий список на каждый кадр — это "
            + "8 Vector3 на элемент в мусор, а сцены здесь бывают в сотни деталей");
    }

    // ── Подсказка вершины ───────────────────────────────────────────────

    [Test]
    public void UpdateHint_TheAnchorItself_IsNotOfferedAsTheSecondEnd()
    {
        MakeMeasurableElement("Деталь", Vector3.zero);
        _controller!.CollectVertices(_camera!);
        Assume.That(_controller.CandidateVerticesWorld.Count, Is.GreaterThan(0));

        Vector3 vertex = _controller.CandidateVerticesWorld[0];
        Vector3 screen = _camera!.WorldToScreenPoint(vertex);
        var mouse = new Vector2(screen.x, screen.y);

        _controller.Anchor = null;
        _controller.UpdateHint(mouse);
        Assert.IsTrue(_controller.Hint.HasValue,
            "положительный контроль: курсор стоит ровно на вершине, подсказка обязана быть");
        Vector3 offered = _controller.Hint!.Value;

        _controller.Anchor = offered;
        _controller.UpdateHint(mouse);

        Assert.IsNull(_controller.Hint,
            "замер из точки в саму себя имеет нулевую длину — предлагать собственный "
            + "якорь вторым концом бессмысленно");
    }

    // ── Выбор отрезка уступает постановке точки ─────────────────────────

    [Test]
    public void UpdateHover_WithNothingToPlace_HighlightsTheSegmentUnderTheCursor()
    {
        MeasureMode.SetActive(true);
        var seg = new MeasureSegment(new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f));
        MeasureStore.Add(seg);

        _controller!.Hint = null;
        _controller.PlaneHint = null;
        _controller.Anchor = null;
        _controller.UpdateHover(_camera!, ScreenCenter());

        Assert.AreSame(seg, _controller.Hovered,
            "положительный контроль: отрезок проходит через центр экрана");
    }

    [Test]
    public void UpdateHover_WhileAPointCanStillBePlaced_HighlightsNothing()
    {
        MeasureMode.SetActive(true);
        var seg = new MeasureSegment(new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f));
        MeasureStore.Add(seg);

        _controller!.Anchor = new Vector3(0f, 1f, 0f);
        _controller.UpdateHover(_camera!, ScreenCenter());

        Assert.IsNull(_controller.Hovered,
            "постановка точки важнее выбора чужого замера — иначе замер ВДОЛЬ детали "
            + "невозможно было бы ни начать, ни закончить: курсор всё время висел бы "
            + "над уже поставленным отрезком");
    }

    // ── Замер в воздухе ─────────────────────────────────────────────────

    [Test]
    public void UpdatePreview_NothingUnderTheCursor_StillPreviewsInMidAir()
    {
        MeasureMode.SetActive(true);
        _controller!.Anchor = new Vector3(0f, 1f, 0f);
        _controller.Hint = null;
        _controller.PlaneHint = null;

        _controller.UpdatePreview(_camera!, ScreenCenter() + new Vector2(120f, 40f));

        Assert.IsTrue(_controller.PreviewEnd.HasValue,
            "коллайдера под курсором нет, но замер обязан работать и в воздухе: курсор "
            + "проецируется на плоскость через якорь, перпендикулярную взгляду камеры");
        var preview = new MeasureSegment(_controller.Anchor!.Value, _controller.PreviewEnd!.Value);
        Assert.AreNotEqual(-1, preview.Axis,
            "свободный конец всё равно прижимается к одной оси — подпись остаётся «N мм» "
            + "без глифа ∠");
    }
}
