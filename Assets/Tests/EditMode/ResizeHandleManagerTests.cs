using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Handles;

public class ResizeHandleManagerTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(Vector3 pos, Vector3Int dims, bool transparent = false)
    {
        var go = new GameObject("E");
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.DimensionsMM = dims;
        if (transparent) e.Transparent = true;
        _spawned.Add(go);
        return e;
    }

    private ResizeHandleManager.HandleMode _modeBefore;

    [SetUp]
    public void Setup() => _modeBefore = ResizeHandleManager.Mode;

    [TearDown]
    public void Teardown()
    {
        // Mode статический и попадает в снапшоты сериализации: не вернув его как было,
        // набор ломает соседние, и одиночный прогон расходится с полным.
        ResizeHandleManager.SetMode(_modeBefore);
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void SupportsHandleResize_RefusesTheBuiltInAppliances()
    {
        var sinkGo = new GameObject("Мойка");
        _spawned.Add(sinkGo);
        var sink = sinkGo.AddComponent<SinkElement>();

        var hobGo = new GameObject("Варочная");
        _spawned.Add(hobGo);
        var hob = hobGo.AddComponent<CooktopElement>();

        Assert.IsFalse(ResizeHandleManager.SupportsHandleResize(sink),
            "мойка покупная, её габарит фиксирован моделью — размеры правятся "
            + "только в окне свойств");
        Assert.IsFalse(ResizeHandleManager.SupportsHandleResize(hob),
            "у варочной габаритная коробка — только плита 5 мм, а «высота» в "
            + "свойствах ОБЩАЯ (плита плюс короб выреза): грань за ручку сдвинулась "
            + "бы не туда, куда метил снэп");
        Assert.IsTrue(ResizeHandleManager.SupportsHandleResize(
            Make(Vector3.zero, new Vector3Int(600, 400, 18))),
            "обычная деталь за грань тянется — иначе тест ничего не различает");
    }

    [Test]
    public void HandlesAvailableFor_AreOffInEveryModeThatOwnsTheMouse()
    {
        var board = Make(Vector3.zero, new Vector3Int(600, 400, 18));
        Assume.That(ResizeHandleManager.HandlesAvailableFor(board), Is.True);

        KitchenDesigner.Core.Measure.MeasureMode.SetActive(true);
        try
        {
            Assert.IsFalse(ResizeHandleManager.HandlesAvailableFor(board),
                "в режиме инструмента ручек нет: они перехватывали бы клики по "
                + "вершинам (рулетка) и по поверхности детали (пипетка)");
        }
        finally { KitchenDesigner.Core.Measure.MeasureMode.Reset(); }

        KitchenDesigner.Core.Tools.EyedropperMode.SetActive(true);
        try
        {
            Assert.IsFalse(ResizeHandleManager.HandlesAvailableFor(board),
                "пипетка забирает мышь так же, как рулетка");
        }
        finally { KitchenDesigner.Core.Tools.EyedropperMode.Reset(); }

        Assert.IsTrue(ResizeHandleManager.HandlesAvailableFor(board),
            "после выхода из инструмента ручки возвращаются");
    }

    [Test]
    public void HandlesAvailableFor_AnImmovableElement_IsFalse()
    {
        var board = Make(Vector3.zero, new Vector3Int(600, 400, 18));
        board.Movable = false;

        Assert.IsFalse(ResizeHandleManager.HandlesAvailableFor(board),
            "запрет перемещения запрещает и ресайз: ручки доступны только "
            + "подвижному объекту, и чекбокс в свойствах переключает это на лету");

        board.Movable = true;
        Assert.IsTrue(ResizeHandleManager.HandlesAvailableFor(board));
    }

    [Test]
    public void HandlesAvailableFor_Null_IsFalse()
    {
        Assert.IsFalse(ResizeHandleManager.HandlesAvailableFor(null));
    }

    [Test]
    public void Drawer_KeepsOnlyItsWidth_SoTheOtherAxesHaveNoHandles()
    {
        var go = new GameObject("Ящик");
        _spawned.Add(go);
        var drawer = go.AddComponent<DrawerElement>();
        drawer.Type = DrawerType.A;
        drawer.NominalLength = 450;
        drawer.InternalWidth = 500;
        var before = drawer.DimensionsMM;

        drawer.DimensionsMM = new Vector3Int(600, before.y + 200, before.z + 200);

        Assert.AreEqual(600, drawer.DimensionsMM.x, "ширина ящика тянется");
        Assert.AreEqual(before.y, drawer.DimensionsMM.y,
            "высота ящика GTV фиксирована типом: ручки оси Y ничего не меняли бы");
        Assert.AreEqual(before.z, drawer.DimensionsMM.z,
            "глубина фиксирована номинальной длиной");
    }

    [Test]
    public void Pillar_CrossSectionStaysSquare_AndHasNoHandles()
    {
        var go = ElementFactory.CreatePillar(70, "Опора", Vector3.zero);
        _spawned.Add(go);
        var pillar = go.GetComponent<PillarElement>();
        int heightBefore = pillar.DimensionsMM.y;

        pillar.DimensionsMM = new Vector3Int(300, heightBefore + 10, 120);

        Assert.AreEqual(PillarElement.DiameterMM_Max, pillar.DimensionsMM.x,
            "диаметр опоры зажат максимумом: ручек X/Z у неё нет, сечение "
            + "правится полем «Диаметр» в свойствах");
        Assert.AreEqual(pillar.DimensionsMM.x, pillar.DimensionsMM.z,
            "сечение круглое: глубина всегда равна ширине, что бы ни просили");
        Assert.AreEqual(heightBefore + 10, pillar.DimensionsMM.y, "а высота тянется");
    }

    [Test]
    public void LoweredWall_MixesALoweredCentreWithFullHeightFaces_UntilRestoreFull()
    {
        var go = new GameObject("Стена");
        _spawned.Add(go);
        var element = go.AddComponent<KitchenElement>();
        element.DimensionsMM = new Vector3Int(4000, 2700, 100);
        go.transform.position = new Vector3(0f, 1.35f, 0f);
        var wall = go.AddComponent<Wall>();

        wall.SetLowered(true, 0.9f);

        var faces = element.GetFaces();
        float centreFromFaces = (faces[2].center.y + faces[3].center.y) * 0.5f;
        Assert.AreEqual(1.35f, centreFromFaces, 1e-4f, "грани отдают ПОЛНУЮ высоту");
        Assert.AreEqual(0.45f, go.transform.position.y, 1e-4f, "а трансформ уже опущен");
        Assert.AreNotEqual(centreFromFaces, go.transform.position.y,
            "стартовые размер и центр, снятые в опущенном состоянии, разъезжаются "
            + "с геометрией грани — отсюда прыжок объекта на первом кадре тяги; "
            + "поэтому стену поднимают ДО того, как берут грань");

        wall.RestoreFull();

        Assert.AreEqual(1.35f, go.transform.position.y, 1e-4f,
            "RestoreFull ДО снятия геометрии грани убирает рассогласование");
        Assert.AreEqual(2.7f, go.transform.localScale.y, 1e-4f);
    }

    private static void Resize(KitchenElement target, int faceIndex, float rawDelta,
        IList<KitchenElement> others, float threshold,
        out Vector3Int newDims, out Vector3 newCenter, out bool snapped)
    {
        var f = target.GetFaces()[faceIndex];
        int axisIndex = faceIndex / 2;
        var dims = target.DimensionsMM;
        int dimMM = ResizeMath.DimAlong(dims, axisIndex);
        float sizeStart = dimMM * AppConstants.MM_TO_UNITS;

        ResizeMath.Compute(dims, axisIndex, f.normal.normalized, f.center, f.rightAxis, f.upAxis, f.size,
            target.transform.position, sizeStart, rawDelta, others.ToGeometry(), target.ToGeometry(),
            threshold > 0f, threshold, out newDims, out newCenter, out snapped);
    }

    [Test]
    public void ToggleMode_CyclesBetweenResizeAndMove()
    {
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);
        Assert.AreEqual(ResizeHandleManager.HandleMode.Resize, ResizeHandleManager.Mode);

        ResizeHandleManager.ToggleMode();
        Assert.AreEqual(ResizeHandleManager.HandleMode.Move, ResizeHandleManager.Mode);

        ResizeHandleManager.ToggleMode();
        Assert.AreEqual(ResizeHandleManager.HandleMode.Resize, ResizeHandleManager.Mode);
    }

    [Test]
    public void SetMode_SetsExactMode()
    {
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Move);
        Assert.AreEqual(ResizeHandleManager.HandleMode.Move, ResizeHandleManager.Mode);

        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);
        Assert.AreEqual(ResizeHandleManager.HandleMode.Resize, ResizeHandleManager.Mode);

        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Move);
        Assert.AreEqual(ResizeHandleManager.HandleMode.Move, ResizeHandleManager.Mode);
    }

    [Test]
    public void IsResizing_DefaultFalse()
    {
        Assert.IsFalse(ResizeHandleManager.IsResizing);
    }

    [Test]
    public void IsResizingElement_NotResizing_ReturnsFalse()
    {
        var e = Make(Vector3.zero, new Vector3Int(800, 400, 18));
        Assert.IsFalse(ResizeHandleManager.IsResizingElement(e));
    }

    [Test]
    public void IsResizingElement_Null_ReturnsFalse()
    {
        Assert.IsFalse(ResizeHandleManager.IsResizingElement(null!));
    }

    private static ResizeHandle MakeHandle(int faceIndex, KitchenElement? parent = null)
    {
        var go = new GameObject($"H_{faceIndex}");
        go.transform.SetParent(parent != null ? parent.transform : null, false);
        var h = go.AddComponent<ResizeHandle>();
        h.faceIndex = faceIndex;
        return h;
    }

    [Test]
    public void FreeResize_NegativeDelta_ShrinksElement()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 0, -0.1f, new List<KitchenElement>(), 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsFalse(snapped);
        Assert.AreEqual(700, dims.x);
        Assert.AreEqual(-0.05f, center.x, 0.001f);
    }

    [Test]
    public void FreeResize_LargeNegativeDelta_ClampedAt1mm()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 0, -10f, new List<KitchenElement>(), 0f,
            out var dims, out _, out _);

        Assert.AreEqual(1, dims.x);
    }

    [Test]
    public void FreeResize_MinClamp_OneMm()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 0, -0.799f, new List<KitchenElement>(), 0f,
            out var dims, out _, out _);

        Assert.GreaterOrEqual(dims.x, 1);
    }

    [Test]
    public void FreeResize_AlongY_Axis()
    {
        var a = Make(new Vector3(0, 0.5f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 2, 0.1f, new List<KitchenElement>(), 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsFalse(snapped);
        Assert.AreEqual(500, dims.y);
        Assert.AreEqual(0.55f, center.y, 0.001f);
    }

    [Test]
    public void FreeResize_AlongZ_Axis()
    {
        var a = Make(new Vector3(0, 0.2f, 0.2f), new Vector3Int(800, 400, 18));

        Resize(a, 4, 0.05f, new List<KitchenElement>(), 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsFalse(snapped);
        Assert.AreEqual(68, dims.z);
        Assert.AreEqual(0.225f, center.z, 0.001f);
    }

    [Test]
    public void FreeResize_NegativeDirection_OppositeFace()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 1, 0.1f, new List<KitchenElement>(), 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsFalse(snapped);
        Assert.AreEqual(900, dims.x);
        Assert.AreEqual(-0.05f, center.x, 0.001f);
    }

    [Test]
    public void SnapResize_OnY_Axis_FlushToFloor()
    {
        var floor = Make(new Vector3(0, 0f, 0), new Vector3Int(3000, 18, 3000));
        floor.gameObject.AddComponent<BasePlate>();
        var wall = Make(new Vector3(0, 1.5f, 0), new Vector3Int(2000, 2500, 100));

        Resize(wall, 3, 0.22f, new List<KitchenElement> { floor }, 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsTrue(snapped);
        Assert.AreEqual(2741, dims.y);
        Assert.AreEqual(0.009f, center.y - dims.y * AppConstants.MM_TO_UNITS * 0.5f, 0.001f);
    }

    [Test]
    public void SnapResize_OnZ_Axis_BetweenBoards()
    {
        var a = Make(new Vector3(0, 0.2f, 0.5f), new Vector3Int(800, 400, 18));
        var b = Make(new Vector3(0, 0.2f, 0.85f), new Vector3Int(800, 400, 18));

        Resize(a, 4, 0.32f, new List<KitchenElement> { b }, 0.05f,
            out var dims, out var center, out bool snapped);

        Assert.IsTrue(snapped);

        a.DimensionsMM = dims;
        a.transform.position = center;

        Assert.AreEqual(350, dims.z);
        Assert.IsFalse(SnapSystem.ElementsIntersect(a, b));
    }

    [Test]
    public void Resize_ZeroDelta_ReturnsOriginalDims()
    {
        var a = Make(new Vector3(0, 0.2f, 0), new Vector3Int(800, 400, 18));

        Resize(a, 0, 0f, new List<KitchenElement>(), 0f,
            out var dims, out var center, out bool snapped);

        Assert.IsFalse(snapped);
        Assert.AreEqual(800, dims.x);
        Assert.AreEqual(0f, center.x, 0.001f);
    }

    [Test]
    public void DimAlong_ReturnsCorrectAxis()
    {
        var dims = new Vector3Int(800, 400, 18);
        Assert.AreEqual(800, ResizeMath.DimAlong(dims, 0));
        Assert.AreEqual(400, ResizeMath.DimAlong(dims, 1));
        Assert.AreEqual(18, ResizeMath.DimAlong(dims, 2));
    }

    [Test]
    public void CenterForAppliedDims_SameSize_NoShift()
    {
        var center = ResizeMath.CenterForAppliedDims(
            new Vector3(1f, 0.5f, 0f), Vector3.right, 0.8f,
            new Vector3Int(800, 400, 18), 0);

        Assert.AreEqual(1f, center.x, 0.001f);
        Assert.AreEqual(0.5f, center.y, 0.001f);
    }

    [Test]
    public void CenterForAppliedDims_Grew_HalfDeltaShift()
    {
        var center = ResizeMath.CenterForAppliedDims(
            new Vector3(0f, 0.2f, 0f), Vector3.right, 0.8f,
            new Vector3Int(900, 400, 18), 0);

        Assert.AreEqual(0.05f, center.x, 0.001f);
    }

    [Test]
    public void CenterForAppliedDims_Shrank_HalfDeltaShift()
    {
        var center = ResizeMath.CenterForAppliedDims(
            new Vector3(0f, 0.2f, 0f), Vector3.right, 0.8f,
            new Vector3Int(700, 400, 18), 0);

        Assert.AreEqual(-0.05f, center.x, 0.001f);
    }

    [Test]
    public void CenterForAppliedDims_YAxis()
    {
        var center = ResizeMath.CenterForAppliedDims(
            new Vector3(0f, 0.5f, 0f), Vector3.up, 0.4f,
            new Vector3Int(800, 500, 18), 1);

        Assert.AreEqual(0.55f, center.y, 0.001f);
    }

    [Test]
    public void CenterForAppliedDims_ZAxis()
    {
        var center = ResizeMath.CenterForAppliedDims(
            new Vector3(0f, 0.2f, 0.2f), Vector3.forward, 0.018f,
            new Vector3Int(800, 400, 68), 2);

        Assert.AreEqual(0.225f, center.z, 0.001f);
    }

    [Test]
    public void Ctor_ResizeHandle_FaceIndexProperty()
    {
        var go = new GameObject("H");
        var handle = go.AddComponent<ResizeHandle>();
        handle.faceIndex = 3;

        Assert.AreEqual(3, handle.faceIndex);
        Object.DestroyImmediate(go);
    }


    private Camera MakeCamera()
    {
        var go = new GameObject("Камера");
        _spawned.Add(go);
        go.transform.SetPositionAndRotation(new Vector3(0f, 0f, -5f), Quaternion.identity);
        return go.AddComponent<Camera>();
    }

    private ResizeHandle MakeHandleAt(int faceIndex, Vector3 grabPoint)
    {
        var h = MakeHandle(faceIndex);
        _spawned.Add(h.gameObject);
        h.grabPoint = grabPoint;
        return h;
    }

    [Test]
    public void PickHandle_TakesTheNearestOnScreen()
    {
        var cam = MakeCamera();
        var left = MakeHandleAt(0, new Vector3(-1f, 0f, 0f));
        var right = MakeHandleAt(1, new Vector3(1f, 0f, 0f));
        var handles = new List<ResizeHandle> { left, right };

        Vector2 overRight = cam.WorldToScreenPoint(right.grabPoint);

        Assert.AreSame(right, ResizeHandleManager.PickHandle(overRight, cam, handles),
            "ручку выбирает экранное расстояние до её наконечника, а не луч по коллайдеру: "
            + "стрелку внутри корпуса геометрия перекрывает, и физика её не отдавала вовсе");
    }

    [Test]
    public void PickHandle_FarFromEveryHandle_IsNull()
    {
        var cam = MakeCamera();
        var handles = new List<ResizeHandle> { MakeHandleAt(0, Vector3.zero) };

        Vector2 onHandle = cam.WorldToScreenPoint(Vector3.zero);
        var far = onHandle + new Vector2(HandleScreenPick.DefaultRadiusPixels + 5f, 0f);

        Assert.IsNotNull(ResizeHandleManager.PickHandle(onHandle, cam, handles));
        Assert.IsNull(ResizeHandleManager.PickHandle(far, cam, handles),
            "за радиусом захвата ручка не берётся — иначе клик по пустому месту "
            + "начинал бы ресайз");
    }

    [Test]
    public void PickHandle_HandleBehindTheCamera_IsIgnored()
    {
        var cam = MakeCamera();
        var behind = MakeHandleAt(0, new Vector3(0f, 0f, -20f));
        var handles = new List<ResizeHandle> { behind };

        var projected = cam.WorldToScreenPoint(behind.grabPoint);
        Assume.That(projected.z, Is.LessThanOrEqualTo(0f));

        Assert.IsNull(ResizeHandleManager.PickHandle(
            new Vector2(projected.x, projected.y), cam, handles),
            "WorldToScreenPoint зеркалит точки за спиной камеры в правдоподобные "
            + "пиксели: без отсева по z ручка со спины ловилась бы курсором");
    }

    [Test]
    public void PickHandle_NoCamera_IsNull()
    {
        var handles = new List<ResizeHandle> { MakeHandleAt(0, Vector3.zero) };

        Assert.IsNull(ResizeHandleManager.PickHandle(Vector2.zero, null, handles));
    }

}
