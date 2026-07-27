using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Размещение ручек ресайза/перемещения на стене: вынос к камере и
/// откат торцевой стрелки в «угле» (WallHandlePlacement).</summary>
public class WallHandlePlacementTests
{
    // Геометрия стрелки из ResizeHandleManager: зазор + стержень + наконечник.
    private const float ArrowLen = 0.02f + 0.10f + 0.05f;
    private const float Gap = 0.02f;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement MakeWall(Vector3 pos, Vector3Int dims, float yaw = 0f)
    {
        var go = new GameObject("W");
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        var e = go.AddComponent<KitchenElement>();
        e.DimensionsMM = dims;
        go.AddComponent<Wall>();
        _spawned.Add(go);
        return e;
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
    public void LengthAxis_ThinWall_IsTheLongHorizontalAxis()
    {
        Assert.AreEqual(0, WallHandlePlacement.LengthAxis(new Vector3(4f, 2.7f, 0.1f)));
        Assert.AreEqual(2, WallHandlePlacement.LengthAxis(new Vector3(0.1f, 2.7f, 4f)));
        Assert.AreEqual(4f, WallHandlePlacement.Length(new Vector3(4f, 2.7f, 0.1f)), 1e-4f);
    }

    [Test]
    public void CameraOffset_PushesHandlesOutOfTheWallTowardsCamera()
    {
        var scale = new Vector3(4f, 2.7f, 0.1f);
        var pos = new Vector3(0f, 1.35f, 0f);

        var front = WallHandlePlacement.CameraOffset(
            pos, Quaternion.identity, scale, new Vector3(0f, 5f, 6f), Gap);
        var back = WallHandlePlacement.CameraOffset(
            pos, Quaternion.identity, scale, new Vector3(0f, 5f, -6f), Gap);

        Assert.AreEqual(0.07f, front.z, 1e-4f);   // полтолщины 0.05 + зазор 0.02
        Assert.AreEqual(-0.07f, back.z, 1e-4f);   // камера с другой стороны — на ту же сторону
        Assert.AreEqual(0f, front.x, 1e-4f);
        Assert.AreEqual(0f, front.y, 1e-4f);
    }

    [Test]
    public void CameraOffset_RotatedWall_UsesItsOwnNormal()
    {
        var scale = new Vector3(4f, 2.7f, 0.1f);
        var offset = WallHandlePlacement.CameraOffset(
            new Vector3(2f, 1.35f, 2f), Quaternion.Euler(0f, 90f, 0f), scale,
            new Vector3(-4f, 5f, 2f), Gap);

        Assert.AreEqual(-0.07f, offset.x, 1e-4f); // нормаль повёрнутой стены — по X
        Assert.AreEqual(0f, offset.z, 1e-4f);
    }

    [Test]
    public void Blocked_FreeEnd_False()
    {
        MakeCornerA();
        var walls = new List<KitchenElement> { MakeCornerB() };
        // Свободный торец A (x = -2) с уже применённым выносом к камере.
        var origin = new Vector3(-2f, 1.35f, 0.07f);

        Assert.IsFalse(WallHandlePlacement.Blocked(origin, Vector3.left, ArrowLen, walls));
        Assert.AreEqual(0f, WallHandlePlacement.PullBack(origin, Vector3.left, ArrowLen, 2f, walls), 1e-4f);
    }

    [Test]
    public void Blocked_Corner_ArrowStartsInsideTheNeighbourWall()
    {
        MakeCornerA();
        var walls = new List<KitchenElement> { MakeCornerB() };
        var origin = new Vector3(2f, 1.35f, 0.07f); // торец A в углу, вынос к камере

        Assert.IsTrue(WallHandlePlacement.Blocked(origin, Vector3.right, ArrowLen, walls));
    }

    [Test]
    public void PullBack_Corner_PutsTheWholeArrowOnTheWall()
    {
        MakeCornerA();
        var walls = new List<KitchenElement> { MakeCornerB() };
        var origin = new Vector3(2f, 1.35f, 0.07f);

        float d = WallHandlePlacement.PullBack(origin, Vector3.right, ArrowLen, 2f, walls);

        Assert.Greater(d, 0f);
        Assert.LessOrEqual(d, 2f);
        // Отведённая стрелка целиком свободна и не выходит за торец стены.
        var moved = origin - Vector3.right * d;
        Assert.IsFalse(WallHandlePlacement.Blocked(moved, Vector3.right, ArrowLen, walls));
        Assert.LessOrEqual(moved.x + ArrowLen, 2f + 1e-4f);
    }

    [Test]
    public void PullBack_NoFreeSpot_StillLeavesTheAxisContinuation()
    {
        MakeCornerA();
        var walls = new List<KitchenElement> { MakeCornerB() };
        var origin = new Vector3(2f, 1.35f, 0.07f);

        // maxShift меньше длины стрелки — отойти некуда, но на продолжении оси
        // ручку не оставляем.
        float d = WallHandlePlacement.PullBack(origin, Vector3.right, ArrowLen, 0.05f, walls);

        Assert.AreEqual(ArrowLen, d, 1e-4f);
    }
}
