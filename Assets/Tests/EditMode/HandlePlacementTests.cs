using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Размещение ручек ресайза/перемещения: габаритный ящик детали и вынос
/// ручек плиты на ту сторону, где стоит камера (HandlePlacement). Перекрытием
/// размещение не занимается — ручки рисуются поверх всего и ловятся по экрану.</summary>
public class HandlePlacementTests
{
    private const float Gap = 0.02f;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(Vector3 pos, Vector3Int dims, float yaw = 0f, bool wall = false)
    {
        var go = new GameObject(wall ? "W" : "P");
        go.transform.SetPositionAndRotation(pos, ManagedRotation.Euler(0f, yaw, 0f));
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
}
