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
        var cr = ManagedRotation.Euler(0f, 30f, 0f);
        FacadeDoor.Pose(cp, cr, Half, DoorMode.HingeFrontLeft, 0f, out var pos, out var rot);
        Assert.Less(Vector3.Distance(pos, cp), 1e-4f);
        Assert.Less(Quaternion.Angle(rot, cr), 1e-3f);
    }

    [Test]
    public void Pose_ClosedAtProgressZero_ForEveryMode()
    {
        var cp = new Vector3(1f, 0.5f, -2f);
        var cr = ManagedRotation.Euler(10f, 30f, 0f);
        foreach (var m in AllModes())
        {
            FacadeDoor.Pose(cp, cr, Half, m, 0f, out var pos, out var rot);
            Assert.Less(Vector3.Distance(pos, cp), 1e-4f, $"{m}: закрыто ≠ исходное");
            Assert.Less(Quaternion.Angle(rot, cr), 1e-3f, $"{m}: закрыто ≠ исходное");
        }
    }

    [Test]
    public void Pose_FullyOpen_CupHinge_Rotates110()
    {
        var cr = Quaternion.identity;
        FacadeDoor.Pose(Vector3.zero, cr, Half, DoorMode.HingeFrontLeft, 1f, out _, out var rot);
        Assert.AreEqual(110f, Quaternion.Angle(cr, rot), 0.5f);
    }

    [Test]
    public void Pose_FullyOpen_EdgePivot_Rotates90()
    {
        var cr = Quaternion.identity;
        FacadeDoor.Pose(Vector3.zero, cr, Half, DoorMode.HingeFrontLeft, 1f, out _, out var rot,
            HingeKinematics.EdgePivot);
        Assert.AreEqual(90f, Quaternion.Angle(cr, rot), 0.5f);
    }

    [Test]
    public void MaxAngle_DependsOnKinematics()
    {
        Assert.AreEqual(110f, FacadeDoor.MaxAngle(HingeKinematics.CupHinge), 1e-4f);
        Assert.AreEqual(90f, FacadeDoor.MaxAngle(HingeKinematics.EdgePivot), 1e-4f);
    }

    [TestCase(DoorMode.HingeFrontLeft)]
    [TestCase(DoorMode.HingeFrontRight)]
    [TestCase(DoorMode.HingeFrontTop)]
    [TestCase(DoorMode.HingeFrontBottom)]
    [TestCase(DoorMode.HingeBackLeft)]
    [TestCase(DoorMode.HingeBackTop)]
    [TestCase(DoorMode.HingeEdgeTopLeft)]
    [TestCase(DoorMode.HingeEdgeBottomRight)]
    public void Pose_HingeAxis_StaysFixed(DoorMode mode)
    {
        var cp = new Vector3(0.5f, 1f, 0.25f);
        var cr = ManagedRotation.Euler(0f, 90f, 0f);

        Assert.IsTrue(FacadeDoor.Hinge(mode, Half, out var pivotLocal, out _));
        var pivotClosed = cp + cr * pivotLocal;

        FacadeDoor.Pose(cp, cr, Half, mode, 1f, out var pos, out var rot);
        var pivotOpen = pos + rot * pivotLocal;

        Assert.Less(Vector3.Distance(pivotClosed, pivotOpen), 1e-4f,
            "ось петли не должна смещаться при открытии");
    }

    [TestCase(DoorMode.HingeFrontLeft)]
    [TestCase(DoorMode.HingeFrontRight)]
    [TestCase(DoorMode.HingeEdgeTopLeft)]
    public void Pose_EdgePivot_KeepsBoxEdgeFixed(DoorMode mode)
    {
        // Дверь и окно помещения по-прежнему вращаются вокруг собственного ребра.
        var cp = new Vector3(0.5f, 1f, 0.25f);
        var cr = ManagedRotation.Euler(0f, 90f, 0f);

        Assert.IsTrue(FacadeDoor.Hinge(mode, Half, out var pivotLocal, out _,
            HingeKinematics.EdgePivot));
        var expectedEdge = Vector3.Scale(EdgeSigns(mode), Half);
        Assert.Less(Vector3.Distance(pivotLocal, expectedEdge), 1e-6f,
            "при EdgePivot ось совпадает с ребром бокса");

        var pivotClosed = cp + cr * pivotLocal;
        FacadeDoor.Pose(cp, cr, Half, mode, 1f, out var pos, out var rot, HingeKinematics.EdgePivot);
        Assert.Less(Vector3.Distance(pivotClosed, pos + rot * pivotLocal), 1e-4f);
    }

    private static Vector3 EdgeSigns(DoorMode mode) => mode switch
    {
        DoorMode.HingeFrontLeft => new Vector3(-1f, 0f, -1f),
        DoorMode.HingeFrontRight => new Vector3(1f, 0f, -1f),
        DoorMode.HingeEdgeTopLeft => new Vector3(-1f, 1f, 0f),
        _ => Vector3.zero
    };

    // ── Мебельная (чашечная) петля: виртуальная ось ──────────────────
    // Ось лежит в чашке Ø35, утопленной в заднюю пласть, поэтому петлевое
    // ребро при открывании уходит внутрь корпуса, а не стоит на месте.

    [Test]
    public void Pose_CupHinge_HingeEdgeMovesInward()
    {
        var cp = Vector3.zero;
        var cr = Quaternion.identity;

        // Середина петлевого ребра на задней пласти (режим «Дверь: слева»).
        var edgeLocal = new Vector3(-Half.x, 0f, -Half.z);

        FacadeDoor.Pose(cp, cr, Half, DoorMode.HingeFrontLeft, 1f, out var pos, out var rot);
        var edgeOpen = pos + rot * edgeLocal;

        var offset = FacadeDoor.HingePivotOffset(Half);
        float inward = edgeOpen.x - edgeLocal.x; // +X = внутрь фасада, к его центру
        Assert.Greater(inward, offset.x + offset.z,
            "петлевое ребро должно уйти внутрь минимум на смещение оси");
        Assert.Less(inward, 3f * (offset.x + offset.z), "но не улететь");
    }

    [Test]
    public void Pose_CupHinge_ReducesSidewaysOverhang()
    {
        // Наружный угол фасада выступает за линию петли меньше, чем на всю толщину,
        // — ради этого четырёхшарнирная петля и придумана.
        var cr = Quaternion.identity;
        var cornerLocal = new Vector3(-Half.x, 0f, Half.z);

        FacadeDoor.Pose(Vector3.zero, cr, Half, DoorMode.HingeFrontLeft, 1f, out var cup, out var cupRot);
        FacadeDoor.Pose(Vector3.zero, cr, Half, DoorMode.HingeFrontLeft, 1f, out var edge, out var edgeRot,
            HingeKinematics.EdgePivot);

        float cupOverhang = -Half.x - (cup + cupRot * cornerLocal).x;
        float edgeOverhang = -Half.x - (edge + edgeRot * cornerLocal).x;

        Assert.Greater(edgeOverhang, cupOverhang,
            "мебельная петля выносит фасад вбок меньше, чем поворот по ребру");
        Assert.Less(cupOverhang, Half.z,
            "у мебельной петли вынос меньше половины толщины фасада");
        Assert.AreEqual(2f * Half.z, edgeOverhang, 1e-4f,
            "поворот по ребру выносит фасад ровно на его толщину");
    }

    [Test]
    public void Pose_CupHinge_NotAppliedToCornerModes()
    {
        // Угловые режимы по толщине — не мебельная петля: ребро стоит на месте.
        Assert.IsTrue(FacadeDoor.Hinge(DoorMode.HingeEdgeBottomLeft, Half, out var pivot, out _));
        Assert.Less(Vector3.Distance(pivot, new Vector3(-Half.x, -Half.y, 0f)), 1e-6f);
    }

    // Главный инвариант: за весь ход открывания петлевое ребро почти не вылезает за
    // линию петли — иначе открытый фасад упрётся в соседний.
    [TestCase(16, 2f)]
    [TestCase(18, 3f)]
    [TestCase(19, 3.5f)]
    public void Pose_CupHinge_SidewaysOverhang_FitsInFacadeGap(int thicknessMM, float maxOverhangMM)
    {
        var half = new Vector3(0.3f, 0.35f, thicknessMM * 0.0005f);
        var cornerLocal = new Vector3(-half.x, 0f, half.z);

        float worst = float.MinValue;
        for (float t = 0f; t <= 1.0001f; t += 0.02f)
        {
            FacadeDoor.Pose(Vector3.zero, Quaternion.identity, half, DoorMode.HingeFrontLeft, t,
                out var pos, out var rot);
            worst = Mathf.Max(worst, -half.x - (pos + rot * cornerLocal).x);
        }

        Assert.Less(worst * 1000f, maxOverhangMM,
            $"фасад {thicknessMM} мм вылезает за линию петли на {worst * 1000f:F1} мм");
    }

    [Test]
    public void Pose_CupHinge_FullyOpen_TucksInsideHingeLine()
    {
        // В конце хода фасад уходит ВНУТРЬ линии петли, а не выступает наружу.
        var cornerLocal = new Vector3(-Half.x, 0f, Half.z);
        FacadeDoor.Pose(Vector3.zero, Quaternion.identity, Half, DoorMode.HingeFrontLeft, 1f,
            out var pos, out var rot);

        Assert.Greater((pos + rot * cornerLocal).x, -Half.x,
            "открытый фасад не должен выступать за линию петли");
    }

    // ── Смещение оси от толщины фасада ───────────────────────────────
    // side = clamp(T/4, 3, 7) мм; depth = min(12.5, T − 3.5) мм — дно чашки.

    [TestCase(10, 3f, 6.5f)]
    [TestCase(16, 4f, 12.5f)]
    [TestCase(19, 4.75f, 12.5f)]
    [TestCase(21, 5.25f, 12.5f)]
    [TestCase(22, 5.5f, 12.5f)]
    [TestCase(30, 7f, 12.5f)]
    public void HingePivotOffset_FollowsThickness(int thicknessMM, float sideMM, float depthMM)
    {
        var half = new Vector3(0.3f, 0.35f, thicknessMM * 0.0005f);
        var offset = FacadeDoor.HingePivotOffset(half) * 1000f; // юниты → мм

        Assert.AreEqual(sideMM, offset.x, 1e-3f, "боковое смещение (присадка чашки)");
        Assert.AreEqual(sideMM, offset.y, 1e-3f, "то же по вертикали для верхних/нижних петель");
        Assert.AreEqual(depthMM, offset.z, 1e-3f, "глубина оси от задней пласти");
    }

    [Test]
    public void HingePivotOffset_ClampedForTinyFacade()
    {
        // Фасад 4×4×2 мм: ось не должна выйти за противоположную грань.
        var half = new Vector3(0.002f, 0.002f, 0.001f);
        var offset = FacadeDoor.HingePivotOffset(half);

        Assert.LessOrEqual(offset.x, 2f * half.x + 1e-6f);
        Assert.LessOrEqual(offset.y, 2f * half.y + 1e-6f);
        Assert.LessOrEqual(offset.z, 2f * half.z + 1e-6f);
    }

    [Test]
    public void Pose_FrontEdges_OpenOutward_CenterMovesTowardPlusZ()
    {
        // 4 передних ребра распахиваются наружу: центр фасада уезжает к +Z
        // (при 90° нормаль ложится в плоскость, поэтому проверяем именно центр).
        var cr = Quaternion.identity;
        foreach (var m in new[] { DoorMode.HingeFrontLeft, DoorMode.HingeFrontRight,
                                  DoorMode.HingeFrontTop, DoorMode.HingeFrontBottom })
        {
            FacadeDoor.Pose(Vector3.zero, cr, Half, m, 1f, out var pos, out _);
            Assert.Greater(pos.z, 0.05f, $"{m}: центр не ушёл наружу (к +Z)");
        }
    }

    // ── Поза: ящик ───────────────────────────────────────────────────
    [Test]
    public void Pose_DrawerOut_SlidesTowardViewer_NoRotation()
    {
        var cp = new Vector3(1f, 0.5f, -2f);
        var cr = ManagedRotation.Euler(0f, 90f, 0f);

        FacadeDoor.Pose(cp, cr, Half, DoorMode.DrawerOut, 1f, out var pos, out var rot);

        Assert.Less(Quaternion.Angle(cr, rot), 1e-3f, "ящик не поворачивается");
        var expected = cp + cr * (Vector3.forward * FacadeDoor.DrawerSlideMeters); // +Z локально = наружу
        Assert.Less(Vector3.Distance(pos, expected), 1e-4f);
    }

    [Test]
    public void Pose_DrawerRight_SlidesAlongMinusX()
    {
        var cp = Vector3.zero;
        var cr = Quaternion.identity;
        FacadeDoor.Pose(cp, cr, Half, DoorMode.DrawerRight, 1f, out var pos, out _);
        var expected = Vector3.left * FacadeDoor.DrawerSlideMeters;
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
        go.transform.rotation = ManagedRotation.Euler(0f, 90f, 0f);
        _spawned.Add(go);
        return f;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
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
        Assert.AreEqual(110f, Quaternion.Angle(closedRot, f.transform.rotation), 0.5f);
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
        var expected = closedPos + closedRot * (Vector3.forward * FacadeDoor.DrawerSlideMeters);
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
        f.ToggleOpen();
        Assert.IsTrue(f.IsOpen);
        f.ToggleOpen();
        Assert.IsFalse(f.IsOpen);
    }

    // Регрессия: сохранение ОТКРЫТОЙ дверцы должно писать ЗАКРЫТУЮ позу.
    // Иначе после перезагрузки дверца отводится от петли повторно и «уезжает».
    [Test]
    public void Save_OpenDoor_PersistsClosedPose_NotShiftedTransform()
    {
        var f = MakeFacade();
        var closedPos = f.transform.position;
        var closedRot = f.transform.rotation;

        f.SetOpen(true);
        f.StepDoor(1f); // довели анимацию до конца — трансформ отведён от петли
        Assert.Greater(Vector3.Distance(closedPos, f.transform.position), 1e-3f,
            "предусловие: открытая дверца смещена");

        var data = ElementCapture.FromElement(f);

        Assert.Less(Vector3.Distance(closedPos, data.Position), 1e-4f,
            "в сохранение попала закрытая позиция, а не смещённая");
        Assert.Less(Quaternion.Angle(closedRot, data.Rotation), 1e-2f,
            "в сохранение попал закрытый поворот");
        Assert.IsTrue(data.doorOpen, "флаг открытости сохранён");
        Assert.AreEqual((int)f.Mode, data.doorMode, "режим двери сохранён");
    }
}
