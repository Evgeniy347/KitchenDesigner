using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

// Чистая математика открывания дверцы (без Unity-состояния).
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
        // ease-in-out: у краёв медленнее линейного, в середине — быстрее.
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

    // ── Поза ─────────────────────────────────────────────────────────
    [Test]
    public void Pose_Closed_EqualsClosedTransform()
    {
        var cp = new Vector3(1f, 0.5f, -2f);
        var cr = Quaternion.Euler(0f, 30f, 0f);
        FacadeDoor.Pose(cp, cr, Half, HingeEdge.Left, 0f, out var pos, out var rot);
        Assert.Less(Vector3.Distance(pos, cp), 1e-4f);
        Assert.Less(Quaternion.Angle(rot, cr), 1e-3f);
    }

    [Test]
    public void Pose_FullyOpen_Rotates90()
    {
        var cr = Quaternion.identity;
        FacadeDoor.Pose(Vector3.zero, cr, Half, HingeEdge.Left, 1f, out _, out var rot);
        Assert.AreEqual(90f, Quaternion.Angle(cr, rot), 0.5f);
    }

    [TestCase(HingeEdge.Left)]
    [TestCase(HingeEdge.Right)]
    [TestCase(HingeEdge.Top)]
    [TestCase(HingeEdge.Bottom)]
    public void Pose_HingeEdge_StaysFixed(HingeEdge edge)
    {
        var cp = new Vector3(0.5f, 1f, 0.25f);
        var cr = Quaternion.Euler(0f, 90f, 0f);

        FacadeDoor.Hinge(edge, Half, out var pivotLocal, out _, out _);
        var pivotClosed = cp + cr * pivotLocal;

        FacadeDoor.Pose(cp, cr, Half, edge, 1f, out var pos, out var rot);
        var pivotOpen = pos + rot * pivotLocal;

        Assert.Less(Vector3.Distance(pivotClosed, pivotOpen), 1e-4f,
            "ребро-петля не должно смещаться при открытии");
    }

    [TestCase(HingeEdge.Left)]
    [TestCase(HingeEdge.Right)]
    [TestCase(HingeEdge.Top)]
    [TestCase(HingeEdge.Bottom)]
    public void Pose_Progress_TiltsOutOfPlane(HingeEdge edge)
    {
        var cr = Quaternion.identity;
        FacadeDoor.Pose(Vector3.zero, cr, Half, edge, 1f, out _, out var rot);
        Assert.Greater(Quaternion.Angle(cr, rot), 1f, "открытая дверца выходит из плоскости");
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
    public void Open_Animates_ToFullyOpen()
    {
        var f = MakeFacade();
        var closedRot = f.transform.rotation;

        f.SetOpen(true);
        Assert.IsTrue(f.IsOpen);

        f.StepDoor(0.1f); // маленький шаг — где-то посередине
        Assert.Greater(f.DoorProgress, 0f);
        Assert.Less(f.DoorProgress, 1f);

        f.StepDoor(1f);   // добить до конца (MoveTowards зажимает)
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

    [Test]
    public void Hinge_Default_IsLeft()
    {
        var f = MakeFacade();
        Assert.AreEqual(HingeEdge.Left, f.Hinge);
    }
}
