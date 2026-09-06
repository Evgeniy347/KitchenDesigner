using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Откидная дверца прибора. Духовка и посудомойка вели её каждая своей копией
/// (SetOpen / ToggleOpen / ForceClose / StepDoor / ApplyDoorPose / WorldBoxes
/// / DoorLocalRotation) — теперь копия одна, <see cref="DropDoor"/>. Здесь
/// проверяется то, что в копиях было комментариями: петля по НИЖНЕЙ кромке,
/// гашение о препятствие, и что одна и та же функция кормит сцену и расчёт
/// габаритов открывания.
/// </summary>
public class DropDoorTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    private const float PanelWidthMM = 600f;
    private const float PanelHeightMM = 700f;
    private const float PanelThicknessMM = 20f;

    private static readonly Vector3 HingeMM =
        new Vector3(0f, -PanelHeightMM * 0.5f, PanelThicknessMM * 0.5f);

    private static (Vector3 centerMM, Vector3 sizeMM)[] OnePanel() => new[]
    {
        (Vector3.zero, new Vector3(PanelWidthMM, PanelHeightMM, PanelThicknessMM)),
    };

    private (Transform root, ApplianceBoxes boxes, DropDoor door) NewDoor()
    {
        var go = new GameObject("DropDoorHost");
        _spawned.Add(go);
        var boxes = new ApplianceBoxes(go.transform, i => "Panel" + i);
        boxes.Ensure(1);
        var door = new DropDoor(boxes, 0, OnePanel, () => HingeMM);
        return (go.transform, boxes, door);
    }

    [Test]
    public void LocalRotation_ClampsProgress_ToTheZeroNinetyRange()
    {
        Assert.AreEqual(0f, Quaternion.Angle(DropDoor.LocalRotation(-1f), Quaternion.identity), 0.01f,
            "отрицательный прогресс — всё ещё закрытая дверца");
        Assert.AreEqual(DropDoor.OPEN_ANGLE_DEG,
            Quaternion.Angle(DropDoor.LocalRotation(5f), Quaternion.identity), 0.01f,
            "дверца раскрывается ровно в горизонталь и дальше не идёт");
    }

    [Test]
    public void ApplyPose_FullyOpen_KeepsTheBottomEdgeInPlace_AndLaysThePanelFlat()
    {
        var (root, _, door) = NewDoor();
        StepUntilSettled(door, open: true);

        var panel = root.GetChild(0);
        float toU = AppConstants.MM_TO_UNITS;

        Assert.AreEqual(DropDoor.OPEN_ANGLE_DEG,
            Quaternion.Angle(panel.localRotation, Quaternion.identity), 0.5f,
            "откинутая дверца лежит горизонтально");

        Vector3 hinge = HingeMM * toU;
        Vector3 bottomEdge = panel.localPosition
            + panel.localRotation * new Vector3(0f, -PanelHeightMM * 0.5f * toU,
                PanelThicknessMM * 0.5f * toU);
        Assert.Less((bottomEdge - hinge).magnitude, 1e-3f,
            "ось откидывания — НИЖНЯЯ горизонтальная кромка: она остаётся на месте "
            + "при любом прогрессе, иначе дверца уезжала бы от прибора");
    }

    [Test]
    public void ApplyPose_ClosedByDefault_LeavesThePanelWhereTheLayoutPutIt()
    {
        var (root, _, door) = NewDoor();
        door.ApplyPose();

        var panel = root.GetChild(0);
        Assert.AreEqual(0f, door.Progress);
        Assert.AreEqual(Vector3.zero, panel.localPosition);
        Assert.AreEqual(0f, Quaternion.Angle(panel.localRotation, Quaternion.identity), 0.01f);
    }

    [Test]
    public void Step_TowardsTheStateItIsAlreadyIn_DoesNothing()
    {
        var (_, _, door) = NewDoor();
        Assert.IsFalse(door.Step(1f, open: false, null),
            "закрытая дверца с целью «закрыто» не должна каждый кадр перекладывать коробки");
    }

    [Test]
    public void Step_HaltsAtTheCollisionLimit_InsteadOfPushingThrough()
    {
        var (_, _, door) = NewDoor();
        const float limit = 0.3f;

        for (int frame = 0; frame < 30; frame++)
            door.Step(0.1f, open: true, () => limit);

        Assert.LessOrEqual(door.Progress, limit + 1e-3f,
            "препятствие гасит открывание: дверца останавливается на безопасном прогрессе, "
            + "а не проезжает сквозь соседа");
        Assert.Greater(door.Progress, 0f, "но до предела она всё-таки доходит");
    }

    [Test]
    public void Step_WithoutACollisionCheck_ReachesFullyOpen()
    {
        var (_, _, door) = NewDoor();
        StepUntilSettled(door, open: true);
        Assert.AreEqual(1f, door.Progress, 1e-3f);
    }

    [Test]
    public void ForceClose_FromHalfway_SlamsToZero_AndReportsThePoseMustBeReapplied()
    {
        var (_, _, door) = NewDoor();
        door.Step(0.2f, open: true, null);
        Assert.Greater(door.Progress, 0f);

        Assert.IsTrue(door.ForceClose(), "поза изменилась — коробки надо переложить");
        Assert.AreEqual(0f, door.Progress);
        Assert.IsFalse(door.ForceClose(), "уже закрыта — лишней пересборки быть не должно");
    }

    /// <summary>Габариты открывания и сцена обязаны считаться ОДНОЙ функцией:
    /// разойдись они — коллизия ловила бы призрак.</summary>
    [Test]
    public void WorldBoxes_MatchTheBoxTheSceneActuallyPlaces()
    {
        var (root, _, door) = NewDoor();
        StepUntilSettled(door, open: true);

        var openBoxes = new List<OrientedBox>();
        door.WorldBoxes(root, 1f, openBoxes);
        var (min, max) = OpenBoxAabb.Of(openBoxes);
        var panel = root.GetChild(0);

        var corners = new List<Vector3>();
        var half = new Vector3(PanelWidthMM, PanelHeightMM, PanelThicknessMM)
                   * AppConstants.MM_TO_UNITS * 0.5f;
        for (int c = 0; c < 8; c++)
            corners.Add(root.TransformPoint(panel.localPosition + panel.localRotation * new Vector3(
                (c & 1) == 0 ? -half.x : half.x,
                (c & 2) == 0 ? -half.y : half.y,
                (c & 4) == 0 ? -half.z : half.z)));

        foreach (var corner in corners)
        {
            Assert.GreaterOrEqual(corner.x, min.x - 1e-4f);
            Assert.LessOrEqual(corner.x, max.x + 1e-4f);
            Assert.GreaterOrEqual(corner.y, min.y - 1e-4f);
            Assert.LessOrEqual(corner.y, max.y + 1e-4f);
            Assert.GreaterOrEqual(corner.z, min.z - 1e-4f);
            Assert.LessOrEqual(corner.z, max.z + 1e-4f);
        }
    }

    /// <summary>Пассажир (пристёгнутый фасад посудомойки) крутится вокруг ТОЙ ЖЕ
    /// петли, что и дверца, — иначе его собственная кинематика уводит его мимо.</summary>
    [Test]
    public void RiderPose_RotatesAboutTheSameHingeAsTheDoor()
    {
        var (root, _, door) = NewDoor();
        StepUntilSettled(door, open: true);

        var panel = root.GetChild(0);
        var closedWorld = root.TransformPoint(Vector3.zero);

        var (riderPos, riderRot) = DropDoor.RiderPose(
            root, DropDoor.LocalRotation(1f), door.HingeLocalUnits, closedWorld, root.rotation);

        Assert.Less((riderPos - root.TransformPoint(panel.localPosition)).magnitude, 1e-4f,
            "фасад-пассажир едет ровно там же, где полотно двери с той же закрытой позой");
        Assert.AreEqual(0f, Quaternion.Angle(riderRot, root.rotation * panel.localRotation), 0.01f);
    }

    private static void StepUntilSettled(DropDoor door, bool open)
    {
        for (int frame = 0; frame < 50; frame++)
            door.Step(0.1f, open, null);
        door.ApplyPose();
    }
}
