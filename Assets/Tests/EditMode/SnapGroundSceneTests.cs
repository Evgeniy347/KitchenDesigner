using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сценовая часть «земля — грань снэпа» (ядро: <c>GroundSnapTests</c>). Подложки нет;
/// <c>ConstraintValidator.Ground</c> ставит приложение, и пустая сцена обязана прилипать к
/// этой плоскости так же, как прилипала к верху подложки. Красным становятся от возврата
/// <c>SnapSceneGeometry.For</c> вместо <c>ForSnapping</c> в <c>SnapSystem.TrySnap</c> или от
/// потери <c>TryOffer</c> в <c>Diagnose</c>: сцена без пола пользователя перестаёт
/// прилипать к земле, либо земля пропадает из диагноза, либо появляется дважды.
/// Землю дают только эти два входа — ни элемента, ни записи в <c>PartRegistry</c>.</summary>
public class SnapGroundSceneTests : SnapTestBase
{
    private ImpliedGround _groundBefore;

    protected override void OnSetup()
    {
        _groundBefore = ConstraintValidator.Ground;
        ConstraintValidator.Ground = ImpliedGround.At(0f);
    }

    [TearDown]
    public void RestoreGround() => ConstraintValidator.Ground = _groundBefore;

    private static Vector3 StdCentreWithBottomAt(float bottomY) =>
        new Vector3(0f, bottomY + 0.2f, 0f);

    private KitchenElement MakeFloorTopAtGround() =>
        Make("Floor", new Vector3Int(3000, 18, 3000), new Vector3(0f, -0.009f, 0f));

    private static int GroundMentions(SnapDiagnosis d) =>
        d.neighbors.FindAll(n => n.name == GroundSnapGeometry.Name).Count;

    [Test]
    public void EmptyScene_Board15mmAboveGround_SnapsToZero()
    {
        var board = MakeStd("Board", StdCentreWithBottomAt(0.015f));

        var r = Snap(board, new List<KitchenElement> { board }, board.transform.position);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.2f, r.position.y, Tol);
        Assert.AreEqual(GroundSnapGeometry.Name, r.targetName);
    }

    [Test]
    public void EmptyScene_Board300mmAboveGround_DoesNotSnap()
    {
        var board = MakeStd("Board", StdCentreWithBottomAt(0.3f));

        var r = Snap(board, new List<KitchenElement> { board }, board.transform.position);

        Assert.IsFalse(r.snapped);
    }

    [Test]
    public void SnapDisabled_GroundDoesNotSnapEither()
    {
        KitchenSettings.Instance.SnapEnabled = false;
        try
        {
            var board = MakeStd("Board", StdCentreWithBottomAt(0.015f));

            var r = Snap(board, new List<KitchenElement> { board }, board.transform.position);

            Assert.IsFalse(r.snapped);
        }
        finally { KitchenSettings.Instance.SnapEnabled = true; }
    }

    [Test]
    public void NoImpliedGround_EmptyScene_DoesNotSnap()
    {
        ConstraintValidator.Ground = ImpliedGround.None;
        var board = MakeStd("Board", StdCentreWithBottomAt(0.015f));

        var r = Snap(board, new List<KitchenElement> { board }, board.transform.position);

        Assert.IsFalse(r.snapped, "земли нет — приложение её не объявило, прилипать не к чему");
    }

    [Test]
    public void UserFloorPresent_OneSnap_SameY_TargetIsTheFloor()
    {
        var floor = MakeFloorTopAtGround();
        var board = MakeStd("Board", StdCentreWithBottomAt(0.015f));

        var r = Snap(board, new List<KitchenElement> { floor, board }, board.transform.position);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.2f, r.position.y, Tol);
        Assert.AreEqual("Floor", r.targetName);
    }

    [Test]
    public void UserFloorPresent_BoardBesideIt_SnapsToTheGround()
    {
        var floor = MakeFloorTopAtGround();
        var board = MakeStd("Board", StdCentreWithBottomAt(0.015f) + new Vector3(3f, 0f, 0f));

        var r = Snap(board, new List<KitchenElement> { floor, board }, board.transform.position);

        Assert.IsTrue(r.snapped, "вне контура пола земля остаётся опорой");
        Assert.AreEqual(0.2f, r.position.y, Tol);
        Assert.AreEqual(GroundSnapGeometry.Name, r.targetName);
    }

    [Test]
    public void Diagnose_BoardBesideTheFloor_ListsTheGroundOnce()
    {
        var floor = MakeFloorTopAtGround();
        var board = MakeStd("Board", StdCentreWithBottomAt(0.015f) + new Vector3(3f, 0f, 0f));

        var d = SnapSystem.Diagnose(board, new List<KitchenElement> { floor, board },
            board.transform.position);

        Assert.AreEqual(GroundSnapGeometry.Name, d.snapTarget, "диагноз называет ту же цель, что и снэп");
        Assert.AreEqual(1, GroundMentions(d), "земля названа ровно один раз");
    }

    [Test]
    public void ForSnapping_UnderTheFloorOrBesideIt_FollowsThePosition()
    {
        var elements = new List<KitchenElement> { MakeFloorTopAtGround() };

        Assert.AreEqual(1, SnapSceneGeometry.ForSnapping(elements, null, Vector3.zero).Count,
            "над полом - только пол");
        Assert.AreEqual(2, SnapSceneGeometry.ForSnapping(elements, null, new Vector3(3f, 0f, 0f)).Count,
            "вне контура пола к нему добавлена земля");
    }

    [Test]
    public void ForSnapping_EmptyScene_HoldsOnlyTheGround_AndRegistersNothing()
    {
        int registered = PartRegistry.GetAll().Count;

        var scene = SnapSceneGeometry.ForSnapping(new List<KitchenElement>(), null, Vector3.zero);

        Assert.AreEqual(1, scene.Count);
        Assert.AreEqual(GroundSnapGeometry.Name, scene[0].Name);
        Assert.AreEqual(registered, PartRegistry.GetAll().Count,
            "земля — не элемент: ни в реестре деталей, ни в иерархии её нет");
    }

    [Test]
    public void ForSnapping_WithUserFloor_AddsNothingToTheFloor()
    {
        var floor = MakeFloorTopAtGround();
        var elements = new List<KitchenElement> { floor };

        var scene = SnapSceneGeometry.ForSnapping(elements, null, Vector3.zero);

        Assert.AreEqual(SnapSceneGeometry.For(elements, null).Count, scene.Count);
        Assert.AreEqual("Floor", scene[0].Name);
    }

    [Test]
    public void Diagnose_EmptyScene_ListsTheGroundOnce()
    {
        var board = MakeStd("Board", StdCentreWithBottomAt(0.015f));

        var d = SnapSystem.Diagnose(board, new List<KitchenElement> { board },
            board.transform.position);

        Assert.IsTrue(d.wouldSnap);
        Assert.AreEqual(GroundSnapGeometry.Name, d.snapTarget);
        Assert.AreEqual(1, GroundMentions(d), "земля названа соседом ровно один раз");
        var n = d.neighbors.Find(x => x.name == GroundSnapGeometry.Name);
        Assert.IsTrue(n.wouldSnap, n.verdict);
        Assert.AreEqual(15f, n.gapMM, 0.5f);
        Assert.IsFalse(n.intersects);
    }

    [Test]
    public void Diagnose_WithUserFloor_DoesNotListTheGround()
    {
        var floor = MakeFloorTopAtGround();
        var board = MakeStd("Board", StdCentreWithBottomAt(0.015f));

        var d = SnapSystem.Diagnose(board, new List<KitchenElement> { floor, board },
            board.transform.position);

        Assert.IsTrue(d.wouldSnap);
        Assert.AreEqual("Floor", d.snapTarget);
        Assert.AreEqual(0, GroundMentions(d), "пол пользователя обслуживает сам, земля — дубль");
    }

    [Test]
    public void Diagnose_AndTrySnap_AgreeOnTheGround()
    {
        foreach (float bottomMm in new[] { 5f, 15f, 45f, 80f, 300f })
        {
            var board = MakeStd("Board" + bottomMm, StdCentreWithBottomAt(bottomMm * MM));
            var others = new List<KitchenElement> { board };

            var snap = Snap(board, others, board.transform.position);
            var d = SnapSystem.Diagnose(board, others, board.transform.position);

            Assert.AreEqual(snap.snapped, d.wouldSnap, $"низ на {bottomMm} мм: снэп и диагноз разошлись");
        }
    }
}
