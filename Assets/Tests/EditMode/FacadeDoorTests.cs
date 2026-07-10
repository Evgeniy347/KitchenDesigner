using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

// Чистая математика открывания фасада (без Unity-состояния).
public class FacadeDoorTests
{
    // 600×700×18 мм → половины в юнитах (метрах).
    private static readonly Vector3 Half = new Vector3(0.3f, 0.35f, 0.009f);

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

    // ── Переключатель режима ─────────────────────────────────────────
    [Test]
    public void Next_Cycles_ThroughAllFive()
    {
        Assert.AreEqual(DoorMode.Right, FacadeDoor.Next(DoorMode.Left));
        Assert.AreEqual(DoorMode.Top, FacadeDoor.Next(DoorMode.Right));
        Assert.AreEqual(DoorMode.Bottom, FacadeDoor.Next(DoorMode.Top));
        Assert.AreEqual(DoorMode.Drawer, FacadeDoor.Next(DoorMode.Bottom));
        Assert.AreEqual(DoorMode.Left, FacadeDoor.Next(DoorMode.Drawer), "после ящика — снова слева");
    }

    [Test]
    public void Symbol_IsSingleChar_AndDistinct()
    {
        var modes = new[] { DoorMode.Left, DoorMode.Right, DoorMode.Top, DoorMode.Bottom, DoorMode.Drawer };
        var seen = new HashSet<string>();
        foreach (var m in modes)
        {
            var s = FacadeDoor.Symbol(m);
            Assert.AreEqual(1, s.Length, $"символ режима {m} должен быть одним знаком");
            Assert.IsTrue(seen.Add(s), $"символ режима {m} должен быть уникальным");
        }
    }

    // ── Поза: рёбра ──────────────────────────────────────────────────
    [Test]
    public void Pose_Closed_EqualsClosedTransform()
    {
        var cp = new Vector3(1f, 0.5f, -2f);
        var cr = Quaternion.Euler(0f, 30f, 0f);
        FacadeDoor.Pose(cp, cr, Half, DoorMode.Left, 0f, out var pos, out var rot);
        Assert.Less(Vector3.Distance(pos, cp), 1e-4f);
        Assert.Less(Quaternion.Angle(rot, cr), 1e-3f);
    }

    [Test]
    public void Pose_FullyOpen_Rotates90()
    {
        var cr = Quaternion.identity;
        FacadeDoor.Pose(Vector3.zero, cr, Half, DoorMode.Left, 1f, out _, out var rot);
        Assert.AreEqual(90f, Quaternion.Angle(cr, rot), 0.5f);
    }

    [TestCase(DoorMode.Left)]
    [TestCase(DoorMode.Right)]
    [TestCase(DoorMode.Top)]
    [TestCase(DoorMode.Bottom)]
    public void Pose_HingeEdge_StaysFixed(DoorMode mode)
    {
        var cp = new Vector3(0.5f, 1f, 0.25f);
        var cr = Quaternion.Euler(0f, 90f, 0f);

        Assert.IsTrue(FacadeDoor.Hinge(mode, Half, out var pivotLocal, out _, out _));
        var pivotClosed = cp + cr * pivotLocal;

        FacadeDoor.Pose(cp, cr, Half, mode, 1f, out var pos, out var rot);
        var pivotOpen = pos + rot * pivotLocal;

        Assert.Less(Vector3.Distance(pivotClosed, pivotOpen), 1e-4f,
            "ребро-петля не должно смещаться при открытии");
    }

    [TestCase(DoorMode.Left)]
    [TestCase(DoorMode.Right)]
    [TestCase(DoorMode.Top)]
    [TestCase(DoorMode.Bottom)]
    public void Pose_Edge_TiltsOutOfPlane(DoorMode mode)
    {
        var cr = Quaternion.identity;
        FacadeDoor.Pose(Vector3.zero, cr, Half, mode, 1f, out _, out var rot);
        Assert.Greater(Quaternion.Angle(cr, rot), 1f, "открытая дверца выходит из плоскости");
    }

    // ── Поза: ящик ───────────────────────────────────────────────────
    [Test]
    public void Pose_Drawer_SlidesForward_NoRotation()
    {
        var cp = new Vector3(1f, 0.5f, -2f);
        var cr = Quaternion.Euler(0f, 90f, 0f);

        FacadeDoor.Pose(cp, cr, Half, DoorMode.Drawer, 1f, out var pos, out var rot);

        Assert.Less(Quaternion.Angle(cr, rot), 1e-3f, "ящик не поворачивается");
        var expected = cp + cr * (Vector3.back * FacadeDoor.DrawerSlideMeters);
        Assert.Less(Vector3.Distance(pos, expected), 1e-4f, "ящик выдвигается вперёд по нормали");
    }

    [Test]
    public void Pose_Drawer_Closed_EqualsClosed()
    {
        var cp = new Vector3(1f, 0.5f, -2f);
        var cr = Quaternion.Euler(0f, 90f, 0f);
        FacadeDoor.Pose(cp, cr, Half, DoorMode.Drawer, 0f, out var pos, out var rot);
        Assert.Less(Vector3.Distance(pos, cp), 1e-4f);
        Assert.Less(Quaternion.Angle(rot, cr), 1e-3f);
    }

    [Test]
    public void Hinge_ReturnsFalse_ForDrawer()
    {
        Assert.IsFalse(FacadeDoor.Hinge(DoorMode.Drawer, Half, out _, out _, out _));
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
    public void Mode_Default_IsLeft()
    {
        var f = MakeFacade();
        Assert.AreEqual(DoorMode.Left, f.Mode);
    }

    [Test]
    public void CycleMode_Advances_AndWraps()
    {
        var f = MakeFacade();
        f.CycleMode(); Assert.AreEqual(DoorMode.Right, f.Mode);
        f.CycleMode(); Assert.AreEqual(DoorMode.Top, f.Mode);
        f.CycleMode(); Assert.AreEqual(DoorMode.Bottom, f.Mode);
        f.CycleMode(); Assert.AreEqual(DoorMode.Drawer, f.Mode);
        f.CycleMode(); Assert.AreEqual(DoorMode.Left, f.Mode, "цикл возвращается к началу");
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
        f.Mode = DoorMode.Drawer;

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
