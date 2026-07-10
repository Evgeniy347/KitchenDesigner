using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

// Чистая математика открывания фасада (без Unity-состояния).
public class FacadeDoorTests
{
    // 600×700×18 мм → половины в юнитах (метрах).
    private static readonly Vector3 Half = new Vector3(0.3f, 0.35f, 0.009f);

    private static IEnumerable<DoorMode> AllModes() =>
        (DoorMode[])System.Enum.GetValues(typeof(DoorMode));

    // ── Плавность (синус ease-in-out) ────────────────────────────────
    [Test]
    public void Ease_Endpoints()
    {
        Assert.AreEqual(0f, FacadeDoor.Ease(0f), 1e-5f);
        Assert.AreEqual(1f, FacadeDoor.Ease(1f), 1e-5f);
    }

    [Test]
    public void Ease_Midpoint_IsHalf()
    {
        Assert.AreEqual(0.5f, FacadeDoor.Ease(0.5f), 1e-5f);
    }

    [Test]
    public void Ease_SlowStart_And_SlowEnd()
    {
        Assert.Less(FacadeDoor.Ease(0.25f), 0.25f, "медленный старт");
        Assert.Greater(FacadeDoor.Ease(0.75f), 0.75f, "медленное торможение");
    }

    [Test]
    public void Ease_Monotonic()
    {
        float prev = -1f;
        for (float t = 0f; t <= 1.0001f; t += 0.05f)
        {
            float e = FacadeDoor.Ease(t);
            Assert.GreaterOrEqual(e, prev - 1e-6f, $"не убывает при t={t}");
            prev = e;
        }
    }

    [Test]
    public void Ease_ClampsOutOfRange()
    {
        Assert.AreEqual(0f, FacadeDoor.Ease(-5f), 1e-5f);
        Assert.AreEqual(1f, FacadeDoor.Ease(5f), 1e-5f);
    }

    // ── Переключатель режима (18 = 12 рёбер + 6 ящиков) ───────────────
    [Test]
    public void Count_Is18()
    {
        Assert.AreEqual(18, FacadeDoor.Count);
    }

    [Test]
    public void Next_Advances_AndWrapsFullCircle()
    {
        Assert.AreEqual(DoorMode.HingeFrontRight, FacadeDoor.Next(DoorMode.HingeFrontLeft));
        Assert.AreEqual(DoorMode.HingeFrontLeft, FacadeDoor.Next(DoorMode.DrawerDown), "после последнего — первый");

        var m = DoorMode.HingeFrontLeft;
        for (int i = 0; i < FacadeDoor.Count; i++) m = FacadeDoor.Next(m);
        Assert.AreEqual(DoorMode.HingeFrontLeft, m, "полный цикл возвращает к началу");
    }

    [Test]
    public void Symbol_EveryMode_IsSingleChar_AndDistinct()
    {
        var seen = new HashSet<string>();
        foreach (var m in AllModes())
        {
            var s = FacadeDoor.Symbol(m);
            Assert.AreEqual(1, s.Length, $"символ режима {m} должен быть одним знаком");
            Assert.IsTrue(seen.Add(s), $"символ режима {m} должен быть уникальным");
        }
        Assert.AreEqual(18, seen.Count);
    }

    // ── Поза: рёбра ──────────────────────────────────────────────────
    [Test]
    public void Pose_Closed_EqualsClosedTransform()
    {
        var cp = new Vector3(1f, 0.5f, -2f);
        var cr = Quaternion.Euler(0f, 30f, 0f);
        FacadeDoor.Pose(cp, cr, Half, DoorMode.HingeFrontLeft, 0f, out var pos, out var rot);
        Assert.Less(Vector3.Distance(pos, cp), 1e-4f);
        Assert.Less(Quaternion.Angle(rot, cr), 1e-3f);
    }

    [Test]
    public void Pose_ClosedAtProgressZero_ForEveryMode()
    {
        var cp = new Vector3(1f, 0.5f, -2f);
        var cr = Quaternion.Euler(10f, 30f, 0f);
        foreach (var m in AllModes())
        {
            FacadeDoor.Pose(cp, cr, Half, m, 0f, out var pos, out var rot);
            Assert.Less(Vector3.Distance(pos, cp), 1e-4f, $"{m}: закрыто ≠ исходное");
            Assert.Less(Quaternion.Angle(rot, cr), 1e-3f, $"{m}: закрыто ≠ исходное");
        }
    }

    [Test]
    public void Pose_FullyOpen_Rotates90()
    {
        var cr = Quaternion.identity;
        FacadeDoor.Pose(Vector3.zero, cr, Half, DoorMode.HingeFrontLeft, 1f, out _, out var rot);
        Assert.AreEqual(90f, Quaternion.Angle(cr, rot), 0.5f);
    }

    [TestCase(DoorMode.HingeFrontLeft)]
    [TestCase(DoorMode.HingeFrontRight)]
    [TestCase(DoorMode.HingeFrontTop)]
    [TestCase(DoorMode.HingeFrontBottom)]
    [TestCase(DoorMode.HingeBackLeft)]
    [TestCase(DoorMode.HingeBackTop)]
    [TestCase(DoorMode.HingeEdgeTopLeft)]
    [TestCase(DoorMode.HingeEdgeBottomRight)]
    public void Pose_HingeEdge_StaysFixed(DoorMode mode)
    {
        var cp = new Vector3(0.5f, 1f, 0.25f);
        var cr = Quaternion.Euler(0f, 90f, 0f);

        Assert.IsTrue(FacadeDoor.Hinge(mode, Half, out var pivotLocal, out _));
        var pivotClosed = cp + cr * pivotLocal;

        FacadeDoor.Pose(cp, cr, Half, mode, 1f, out var pos, out var rot);
        var pivotOpen = pos + rot * pivotLocal;

        Assert.Less(Vector3.Distance(pivotClosed, pivotOpen), 1e-4f,
            "ребро-петля не должно смещаться при открытии");
    }

    [Test]
    public void Pose_FrontEdges_OpenOutward_CenterMovesTowardMinusZ()
    {
        // 4 передних ребра распахиваются наружу: центр фасада уезжает к −Z
        // (при 90° нормаль ложится в плоскость, поэтому проверяем именно центр).
        var cr = Quaternion.identity;
        foreach (var m in new[] { DoorMode.HingeFrontLeft, DoorMode.HingeFrontRight,
                                  DoorMode.HingeFrontTop, DoorMode.HingeFrontBottom })
        {
            FacadeDoor.Pose(Vector3.zero, cr, Half, m, 1f, out var pos, out _);
            Assert.Less(pos.z, -0.05f, $"{m}: центр не ушёл наружу (к −Z)");
        }
    }

    // ── Поза: ящик ───────────────────────────────────────────────────
    [Test]
    public void Pose_DrawerOut_SlidesTowardViewer_NoRotation()
    {
        var cp = new Vector3(1f, 0.5f, -2f);
        var cr = Quaternion.Euler(0f, 90f, 0f);

        FacadeDoor.Pose(cp, cr, Half, DoorMode.DrawerOut, 1f, out var pos, out var rot);

        Assert.Less(Quaternion.Angle(cr, rot), 1e-3f, "ящик не поворачивается");
        var expected = cp + cr * (Vector3.back * FacadeDoor.DrawerSlideMeters); // −Z локально
        Assert.Less(Vector3.Distance(pos, expected), 1e-4f);
    }

    [Test]
    public void Pose_DrawerRight_SlidesAlongPlusX()
    {
        var cp = Vector3.zero;
        var cr = Quaternion.identity;
        FacadeDoor.Pose(cp, cr, Half, DoorMode.DrawerRight, 1f, out var pos, out _);
        var expected = Vector3.right * FacadeDoor.DrawerSlideMeters;
        Assert.Less(Vector3.Distance(pos, expected), 1e-4f);
    }

    [Test]
    public void Hinge_ReturnsFalse_ForDrawerModes()
    {
        Assert.IsFalse(FacadeDoor.Hinge(DoorMode.DrawerOut, Half, out _, out _));
        Assert.IsFalse(FacadeDoor.Hinge(DoorMode.DrawerUp, Half, out _, out _));
    }
}

// Поведение анимации на FacadeElement через StepDoor (Update в EditMode не зовётся).
public class FacadeDoorAnimationTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private FacadeElement MakeFacade()
    {
        var go = new GameObject("F_door");
        var f = go.AddComponent<FacadeElement>();
        f.DimensionsMM = new Vector3Int(600, 700, 18); // задаёт localScale
        go.transform.position = new Vector3(1f, 0.5f, -2f);
        go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        _spawned.Add(go);
        return f;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        BoardRegistry.Clear();
    }

    [Test]
    public void StartsClosed()
    {
        var f = MakeFacade();
        Assert.IsFalse(f.IsOpen);
        Assert.IsTrue(f.IsDoorClosed);
        Assert.AreEqual(0f, f.DoorProgress, 1e-5f);
    }

    [Test]
    public void Mode_Default_IsHingeFrontLeft()
    {
        var f = MakeFacade();
        Assert.AreEqual(DoorMode.HingeFrontLeft, f.Mode);
    }

    [Test]
    public void CycleMode_Advances_AndWrapsAfter18()
    {
        var f = MakeFacade();
        f.CycleMode();
        Assert.AreEqual(DoorMode.HingeFrontRight, f.Mode);

        for (int i = 0; i < FacadeDoor.Count; i++) f.CycleMode();
        Assert.AreEqual(DoorMode.HingeFrontRight, f.Mode, "полный цикл (18) возвращает в ту же точку");
    }

    [Test]
    public void Open_Animates_ToFullyOpen()
    {
        var f = MakeFacade();
        var closedRot = f.transform.rotation;

        f.SetOpen(true);
        Assert.IsTrue(f.IsOpen);

        f.StepDoor(0.1f);
        Assert.Greater(f.DoorProgress, 0f);
        Assert.Less(f.DoorProgress, 1f);

        f.StepDoor(1f);
        Assert.AreEqual(1f, f.DoorProgress, 1e-4f);
        Assert.AreEqual(90f, Quaternion.Angle(closedRot, f.transform.rotation), 0.5f);
    }

    [Test]
    public void Close_ReturnsToClosedTransform()
    {
        var f = MakeFacade();
        var closedPos = f.transform.position;
        var closedRot = f.transform.rotation;

        f.SetOpen(true);
        f.StepDoor(1f);
        Assert.Greater(Vector3.Distance(closedPos, f.transform.position), 1e-3f, "открыта → сместилась");

        f.SetOpen(false);
        f.StepDoor(1f);
        Assert.Less(Vector3.Distance(closedPos, f.transform.position), 1e-3f);
        Assert.Less(Quaternion.Angle(closedRot, f.transform.rotation), 1e-2f);
        Assert.IsTrue(f.IsDoorClosed);
    }

    [Test]
    public void Drawer_SlidesForward_ThenReturns()
    {
        var f = MakeFacade();
        var closedPos = f.transform.position;
        var closedRot = f.transform.rotation;
        f.Mode = DoorMode.DrawerOut;

        f.SetOpen(true);
        f.StepDoor(1f);

        Assert.Less(Quaternion.Angle(closedRot, f.transform.rotation), 1e-2f, "ящик не поворачивается");
        var expected = closedPos + closedRot * (Vector3.back * FacadeDoor.DrawerSlideMeters);
        Assert.Less(Vector3.Distance(expected, f.transform.position), 1e-3f, "ящик выдвинулся вперёд");

        f.SetOpen(false);
        f.StepDoor(1f);
        Assert.Less(Vector3.Distance(closedPos, f.transform.position), 1e-3f, "ящик задвинулся обратно");
    }

    [Test]
    public void ForceClose_RestoresImmediately()
    {
        var f = MakeFacade();
        var closedPos = f.transform.position;
        var closedRot = f.transform.rotation;

        f.SetOpen(true);
        f.StepDoor(1f);
        f.ForceClose();

        Assert.Less(Vector3.Distance(closedPos, f.transform.position), 1e-3f);
        Assert.Less(Quaternion.Angle(closedRot, f.transform.rotation), 1e-2f);
        Assert.IsFalse(f.IsOpen);
        Assert.IsTrue(f.IsDoorClosed);
    }

    [Test]
    public void Toggle_Flips_State()
    {
        var f = MakeFacade();
        f.ToggleDoor();
        Assert.IsTrue(f.IsOpen);
        f.ToggleDoor();
        Assert.IsFalse(f.IsOpen);
    }
}
