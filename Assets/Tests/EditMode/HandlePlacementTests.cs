using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Размещение ручек ресайза/перемещения на плоской детали: вынос к камере
/// и откат стрелки, начинающейся внутри соседа (HandlePlacement).</summary>
public class HandlePlacementTests
{
    // Геометрия стрелки из ResizeHandleManager: зазор + стержень + наконечник.
    private const float ArrowLen = 0.02f + 0.10f + 0.05f;
    private const float Gap = 0.02f;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(Vector3 pos, Vector3Int dims, float yaw = 0f, bool wall = false)
    {
        var go = new GameObject(wall ? "W" : "P");
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        var e = go.AddComponent<KitchenElement>();
        e.DimensionsMM = dims;
        if (wall) go.AddComponent<Wall>();
        _spawned.Add(go);
        return e;
    }

    private KitchenElement MakeWall(Vector3 pos, Vector3Int dims, float yaw = 0f) =>
        Make(pos, dims, yaw, wall: true);

    private static HandlePlacement.Box BoxOf(KitchenElement e) =>
        HandlePlacement.BoxOf(e.GetFaces());

    private static List<HandlePlacement.Box> Boxes(params KitchenElement[] elements)
    {
        var list = new List<HandlePlacement.Box>();
        foreach (var e in elements) list.Add(BoxOf(e));
        return list;
    }

    // Г-образный угол: стена A вдоль X от -2 до 2, стена B вдоль Z от 0 до 4.
    // Стены стыкуются по осевым линиям — торец A приходится в толщу B.
    private KitchenElement MakeCornerA() =>
        MakeWall(new Vector3(0f, 1.35f, 0f), new Vector3Int(4000, 2700, 100));

    private KitchenElement MakeCornerB() =>
        MakeWall(new Vector3(2f, 1.35f, 2f), new Vector3Int(4000, 2700, 100), 90f);

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void BoxOf_RotatedElement_CentreAxesAndHalfMatchTheElement()
    {
        var box = BoxOf(MakeWall(new Vector3(2f, 1.35f, 2f), new Vector3Int(4000, 2700, 100), 90f));

        Assert.AreEqual(2f, box.Center.x, 1e-4f);
        Assert.AreEqual(1.35f, box.Center.y, 1e-4f);
        Assert.AreEqual(2f, box.Center.z, 1e-4f);
        Assert.AreEqual(2f, box.Half.x, 1e-4f);
        Assert.AreEqual(1.35f, box.Half.y, 1e-4f);
        Assert.AreEqual(0.05f, box.Half.z, 1e-4f);
        Assert.AreEqual(-1f, box.AxisX.z, 1e-3f);  // поворот на 90°: ось X смотрит по -Z
        Assert.AreEqual(1f, box.AxisZ.x, 1e-3f);
    }

    [Test]
    public void ThinAxis_Plates_ReturnTheThinnestAxis()
    {
        // Стена: тонкая ось Z.
        Assert.AreEqual(2, HandlePlacement.ThinAxis(
            BoxOf(MakeWall(Vector3.zero, new Vector3Int(4000, 2700, 100)))));
        // Полка 600×18×300: тонкая ось Y.
        Assert.AreEqual(1, HandlePlacement.ThinAxis(
            BoxOf(Make(Vector3.zero, new Vector3Int(600, 18, 300)))));
        // Фасад 400×700×18: тонкая ось Z.
        Assert.AreEqual(2, HandlePlacement.ThinAxis(
            BoxOf(Make(Vector3.zero, new Vector3Int(400, 700, 18)))));
    }

    [Test]
    public void BoxOf_Axes_AreUnitVectors()
    {
        var box = BoxOf(MakeWall(new Vector3(2f, 1.35f, 2f), new Vector3Int(4000, 2700, 100), 90f));

        Assert.AreEqual(1f, box.AxisX.magnitude, 1e-4f,
            "оси ящика единичные: полуразмеры и DistanceTo считаются проекцией на них, "
            + "и неединичная ось молча растянула бы обе величины");
        Assert.AreEqual(1f, box.AxisY.magnitude, 1e-4f);
        Assert.AreEqual(1f, box.AxisZ.magnitude, 1e-4f);
    }

    [Test]
    public void BoxOf_LoweredWall_KeepsTheFullHeight()
    {
        var wall = MakeWall(new Vector3(0f, 1.35f, 0f), new Vector3Int(4000, 2700, 100));
        float fullHalfHeight = BoxOf(wall).Half.y;
        Assume.That(fullHalfHeight, Is.EqualTo(1.35f).Within(1e-4f));

        wall.GetComponent<Wall>()!.SetLowered(true, 0.9f);

        Assert.AreEqual(1.35f, BoxOf(wall).Half.y, 1e-4f,
            "ящик собирается из граней, а грани опущенной стены отдают ПОЛНУЮ высоту: "
            + "иначе ручки на срезанной стене уехали бы к полу");
        Assert.AreEqual(1.35f, BoxOf(wall).Center.y, 1e-4f);
    }

    [Test]
    public void ThinAxis_EitherSideOfTheAspectRatio_FlipsTheVerdict()
    {
        Assume.That(HandlePlacement.MinPlateAspectRatio, Is.EqualTo(3f),
            "пробы построены под это отношение: порог между 290 и 310 мм");

        // 100×2000×310: тонкая ось 100, ближайшая другая 310 > 100 × 3.
        Assert.AreEqual(0, HandlePlacement.ThinAxis(
            BoxOf(Make(Vector3.zero, new Vector3Int(100, 2000, 310)))),
            "во столько раз тонкая ось меньше остальных — деталь плита, "
            + "её ручки тонут в толще и их надо вынести");

        // 290 < 300: чуть-чуть не дотянула.
        Assert.AreEqual(-1, HandlePlacement.ThinAxis(
            BoxOf(Make(Vector3.zero, new Vector3Int(100, 2000, 290)))),
            "чуть ниже отношения — уже объём, выносить ручки не нужно");
    }

    [Test]
    public void ThinAxis_Tabletop_IsAPlate_ButAPillarIsNot()
    {
        Assert.AreEqual(1, HandlePlacement.ThinAxis(
            BoxOf(Make(Vector3.zero, new Vector3Int(1200, 38, 600)))),
            "столешница 38 мм при глубине 600 — плита: её ручки тонут в толще");
        Assert.AreEqual(-1, HandlePlacement.ThinAxis(
            BoxOf(Make(Vector3.zero, new Vector3Int(50, 100, 50)))),
            "опора 50×50×100 — не плита, у неё ручки и так снаружи");
    }

    [Test]
    public void IntersectsSegment_ArrowTipTouchingTheFace_IsNotBlocked()
    {
        // Плита 100 мм по X с гранями на 0.5 и 0.6.
        var slab = BoxOf(Make(new Vector3(0.55f, 0f, 0f), new Vector3Int(100, 2000, 2000)));

        Assert.IsFalse(slab.IntersectsSegment(Vector3.zero, Vector3.right, 0.5f),
            "стрелка, упирающаяся кончиком в грань соседа, стоит правильно: "
            + "нулевая длина пересечения — это касание, а не заход внутрь");
        Assert.IsTrue(slab.IntersectsSegment(Vector3.zero, Vector3.right, 0.51f),
            "зашла на 10 мм внутрь — уже мешает");
    }

    [Test]
    public void IntersectsSegment_ParallelToTheSlab_AndOffsetPastIt_Misses()
    {
        // Та же плита: по Y она от -1 до 1.
        var slab = BoxOf(Make(new Vector3(0.55f, 0f, 0f), new Vector3Int(100, 2000, 2000)));

        Assert.IsFalse(slab.IntersectsSegment(new Vector3(0f, 5f, 0f), Vector3.right, 1f),
            "стрелка идёт вдоль плиты и мимо неё: без отдельной проверки нулевого "
            + "наклона эта ось просто пропускается и промах читается как попадание");
        Assert.IsTrue(slab.IntersectsSegment(new Vector3(0f, 0.5f, 0f), Vector3.right, 1f),
            "та же стрелка в пределах плиты по Y — попадание");
    }

    [Test]
    public void ThinAxis_BulkyElement_IsNotAPlate()
    {
        // Корпус ящика и опора 50×100×50 — ручки и так снаружи, выносить нечего.
        Assert.AreEqual(-1, HandlePlacement.ThinAxis(
            BoxOf(Make(Vector3.zero, new Vector3Int(600, 720, 560)))));
        Assert.AreEqual(-1, HandlePlacement.ThinAxis(
            BoxOf(Make(Vector3.zero, new Vector3Int(50, 100, 50)))));
    }

    [Test]
    public void DistanceTo_MeasuresFromTheSurfaceNotTheCentre()
    {
        var box = BoxOf(MakeWall(new Vector3(0f, 1.35f, 0f), new Vector3Int(4000, 2700, 100)));

        Assert.AreEqual(0f, box.DistanceTo(new Vector3(1.5f, 1.35f, 0.02f)), 1e-4f); // внутри
        Assert.AreEqual(0.25f, box.DistanceTo(new Vector3(1.5f, 1.35f, 0.3f)), 1e-4f);
        Assert.AreEqual(1f, box.DistanceTo(new Vector3(3f, 1.35f, 0.05f)), 1e-4f);   // за торцом
    }

    [Test]
    public void CameraOffset_PushesHandlesOutOfTheWallTowardsCamera()
    {
        var box = BoxOf(MakeWall(new Vector3(0f, 1.35f, 0f), new Vector3Int(4000, 2700, 100)));

        var front = HandlePlacement.CameraOffset(box, 2, new Vector3(0f, 5f, 6f), Gap);
        var back = HandlePlacement.CameraOffset(box, 2, new Vector3(0f, 5f, -6f), Gap);

        Assert.AreEqual(0.07f, front.z, 1e-4f);   // полтолщины 0.05 + зазор 0.02
        Assert.AreEqual(-0.07f, back.z, 1e-4f);   // камера с другой стороны — на ту же сторону
        Assert.AreEqual(0f, front.x, 1e-4f);
        Assert.AreEqual(0f, front.y, 1e-4f);
    }

    [Test]
    public void CameraOffset_RotatedWall_UsesItsOwnNormal()
    {
        var box = BoxOf(MakeWall(new Vector3(2f, 1.35f, 2f), new Vector3Int(4000, 2700, 100), 90f));

        var offset = HandlePlacement.CameraOffset(box, 2, new Vector3(-4f, 5f, 2f), Gap);

        Assert.AreEqual(-0.07f, offset.x, 1e-4f); // нормаль повёрнутой стены — по X
        Assert.AreEqual(0f, offset.z, 1e-4f);
    }

    [Test]
    public void CameraOffset_Shelf_LiftsHandlesAboveTheBoard()
    {
        var box = BoxOf(Make(new Vector3(0f, 0.5f, 0f), new Vector3Int(600, 18, 300)));

        var above = HandlePlacement.CameraOffset(box, 1, new Vector3(0f, 3f, 2f), Gap);
        var below = HandlePlacement.CameraOffset(box, 1, new Vector3(0f, -3f, 2f), Gap);

        Assert.AreEqual(0.029f, above.y, 1e-4f);  // полтолщины 0.009 + зазор 0.02
        Assert.AreEqual(-0.029f, below.y, 1e-4f);
        Assert.AreEqual(0f, above.x, 1e-4f);
    }

    [Test]
    public void Blocked_FreeEnd_False()
    {
        MakeCornerA();
        var walls = Boxes(MakeCornerB());
        // Свободный торец A (x = -2) с уже применённым выносом к камере.
        var origin = new Vector3(-2f, 1.35f, 0.07f);

        Assert.IsFalse(HandlePlacement.Blocked(origin, Vector3.left, ArrowLen, walls));
        Assert.AreEqual(0f, HandlePlacement.PullBack(origin, Vector3.left, ArrowLen, 2f, walls), 1e-4f);
    }

    [Test]
    public void Blocked_Corner_ArrowStartsInsideTheNeighbourWall()
    {
        MakeCornerA();
        var walls = Boxes(MakeCornerB());
        var origin = new Vector3(2f, 1.35f, 0.07f); // торец A в углу, вынос к камере

        Assert.IsTrue(HandlePlacement.Blocked(origin, Vector3.right, ArrowLen, walls));
    }

    [Test]
    public void PullBack_Corner_PutsTheWholeArrowOnTheWall()
    {
        MakeCornerA();
        var walls = Boxes(MakeCornerB());
        var origin = new Vector3(2f, 1.35f, 0.07f);

        float d = HandlePlacement.PullBack(origin, Vector3.right, ArrowLen, 2f, walls);

        Assert.Greater(d, 0f);
        Assert.LessOrEqual(d, 2f);
        // Отведённая стрелка целиком свободна и не выходит за торец стены.
        var moved = origin - Vector3.right * d;
        Assert.IsFalse(HandlePlacement.Blocked(moved, Vector3.right, ArrowLen, walls));
        Assert.LessOrEqual(moved.x + ArrowLen, 2f + 1e-4f);
    }

    [Test]
    public void PullBack_NoFreeSpot_StillLeavesTheAxisContinuation()
    {
        MakeCornerA();
        var walls = Boxes(MakeCornerB());
        var origin = new Vector3(2f, 1.35f, 0.07f);

        // maxShift меньше длины стрелки — отойти некуда, но на продолжении оси
        // ручку не оставляем.
        float d = HandlePlacement.PullBack(origin, Vector3.right, ArrowLen, 0.05f, walls);

        Assert.AreEqual(ArrowLen, d, 1e-4f);
    }

    [Test]
    public void PullBack_ShelfBetweenSidePanels_PutsTheArrowOnTheShelf()
    {
        // Полка 600 мм между боковинами 18 мм: торцевая стрелка уходит в боковину,
        // а та выше полки — одного выноса к камере не хватает.
        var shelf = Make(new Vector3(0f, 0.5f, 0f), new Vector3Int(600, 18, 300));
        var left = Make(new Vector3(-0.309f, 0.5f, 0f), new Vector3Int(18, 720, 300));
        var right = Make(new Vector3(0.309f, 0.5f, 0f), new Vector3Int(18, 720, 300));
        var box = BoxOf(shelf);
        var neighbours = Boxes(left, right);

        var origin = new Vector3(0.3f, 0.5f, 0f)
                     + HandlePlacement.CameraOffset(box, 1, new Vector3(0f, 3f, 2f), Gap);

        Assert.IsTrue(HandlePlacement.Blocked(origin, Vector3.right, ArrowLen, neighbours));

        float d = HandlePlacement.PullBack(origin, Vector3.right, ArrowLen, box.Half.x, neighbours);

        Assert.Greater(d, 0f);
        var moved = origin - Vector3.right * d;
        Assert.IsFalse(HandlePlacement.Blocked(moved, Vector3.right, ArrowLen, neighbours));
        // Стрелка осталась над полкой, а не улетела за её середину.
        Assert.Greater(moved.x, 0f);
        Assert.AreEqual(0.529f, moved.y, 1e-4f);
    }
}
