using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Земля как грань для снэпа: подложки (3×3 м) больше нет, и пустая сцена
/// потеряла опору — деталь у пола перестала прилипать. Землю возвращает
/// <c>GroundSnapGeometry</c>: горизонтальная грань, смотрящая вверх, на высоте
/// <c>ImpliedGround.Y</c>, без элемента, выбора, иерархии и реестра деталей.
///
/// Красным эти тесты становятся от удаления <c>TryOffer</c>/<c>AppendTo</c>: пустая сцена
/// снова не прилипает. Второй сторож — «пол пользователя уже обслуживает»: если землю
/// добавлять поверх пола, кандидатов на ту же плоскость станет два, а <c>Diagnose</c>
/// назовёт землю соседом на нулевом зазоре. Сценовая часть (<c>TrySnap</c>, <c>Diagnose</c>,
/// <c>ForSnapping</c>) — <c>SnapGroundSceneTests</c>.</summary>
public class GroundSnapTests : SnapCoreTestBase
{
    private const int NoMovedPart = -1;

    private static readonly Vector3 AtOrigin = Vector3.zero;

    private static List<ElementGeometry> EmptySceneWithGround(float y = 0f)
    {
        var scene = new List<ElementGeometry>();
        GroundSnapGeometry.AppendTo(scene, ImpliedGround.At(y), NoMovedPart, AtOrigin);
        return scene;
    }

    private static List<ElementGeometry> UserFloorScene(bool offerGround)
    {
        var scene = new List<ElementGeometry> { Floor() };
        if (offerGround) GroundSnapGeometry.AppendTo(scene, ImpliedGround.At(0f), NoMovedPart, AtOrigin);
        return scene;
    }

    private static Box Board() => Make("Board", new Vector3Int(800, 400, 18));

    private static Vector3 BoardCentreWithBottomAt(float bottomY) =>
        new Vector3(0f, bottomY + 200f * MM, 0f);

    [Test]
    public void EmptyScene_BoardWithBottom15mmAboveGround_SnapsToZero()
    {
        var r = Snap(Board(), EmptySceneWithGround(), BoardCentreWithBottomAt(15f * MM));

        Assert.IsTrue(r.snapped, "пустая сцена: деталь в 15 мм от земли обязана прилипнуть");
        Assert.AreEqual(200f * MM, r.position.y, Tol, "низ детали встаёт на y = 0");
        Assert.AreEqual(0f, r.position.x, Tol, "сдвиг только по вертикали");
        Assert.AreEqual(0f, r.position.z, Tol, "сдвиг только по вертикали");
        Assert.AreEqual(GroundSnapGeometry.Name, r.targetName, "целью снэпа назван сосед-земля");
    }

    [Test]
    public void EmptyScene_BoardWithBottom300mmAboveGround_DoesNotSnap()
    {
        var r = Snap(Board(), EmptySceneWithGround(), BoardCentreWithBottomAt(300f * MM));

        Assert.IsFalse(r.snapped, "порог 50 мм: в 300 мм от земли прилипать нечему");
    }

    [Test]
    public void EmptyScene_BoardFarFromTheOrigin_StillSnaps()
    {
        var r = Snap(Board(), EmptySceneWithGround(),
            BoardCentreWithBottomAt(10f * MM) + new Vector3(12f, 0f, -9f));

        Assert.IsTrue(r.snapped, "земля не кончается на краю бывшей подложки 3×3 м");
        Assert.AreEqual(200f * MM, r.position.y, Tol, "прилипание по вертикали, как у начала координат");
    }

    [Test]
    public void GroundAtAnotherLevel_IsTheSnapPlane()
    {
        var r = Snap(Board(), EmptySceneWithGround(2.7f), BoardCentreWithBottomAt(2.7f + 20f * MM));

        Assert.IsTrue(r.snapped, "земля на другой отметке тоже грань снэпа");
        Assert.AreEqual(2.7f + 200f * MM, r.position.y, Tol, "высота земли берётся из ImpliedGround");
    }

    [Test]
    public void NoImpliedGround_AddsNothing()
    {
        var scene = new List<ElementGeometry>();

        Assert.IsFalse(GroundSnapGeometry.AppendTo(scene, ImpliedGround.None, NoMovedPart, AtOrigin), "земли не объявлено: добавлять нечего");
        Assert.IsEmpty(scene, "сцена осталась пустой");
        Assert.IsFalse(Snap(Board(), scene, BoardCentreWithBottomAt(15f * MM)).snapped, "без земли пустая сцена не прилипает");
    }

    [Test]
    public void UserFloorAtTheSamePlane_ServesAlone_NoGroundIsAdded()
    {
        var scene = UserFloorScene(offerGround: false);

        bool added = GroundSnapGeometry.AppendTo(scene, ImpliedGround.At(0f), NoMovedPart, AtOrigin);

        Assert.IsFalse(added, "верх пола лежит в плоскости земли — вторая грань была бы дублем");
        Assert.AreEqual(1, scene.Count, "в списке остался только пол");
    }

    [Test]
    public void UserFloorPresent_OneSnapResult_SameY_AsTheFloorAlone()
    {
        var testPos = BoardCentreWithBottomAt(15f * MM);

        var alone = Snap(Board(), UserFloorScene(offerGround: false), testPos);
        var offered = Snap(Board(), UserFloorScene(offerGround: true), testPos);

        Assert.IsTrue(offered.snapped, "с полом пользователя прилипание есть");
        Assert.AreEqual(alone.position.y, offered.position.y, Tol, "та же высота, что у пола без земли");
        Assert.AreEqual("Floor", offered.targetName, "цель — пол пользователя, не земля");
    }

    [Test]
    public void UserFloorPresent_CandidateCountEqualsTheFloorAlone()
    {
        var testPos = BoardCentreWithBottomAt(15f * MM);
        var movedNow = Board().At(testPos);

        int Candidates(List<ElementGeometry> scene)
        {
            var into = new SnapCandidates();
            SnapCandidateCollector.Collect(movedNow, scene, testPos,
                Threshold + Tolerance.SnapEpsilon, false, true, into);
            return into.Candidates.Count;
        }

        int alone = Candidates(UserFloorScene(offerGround: false));

        Assert.Greater(alone, 0, "фикстура: пол сам даёт кандидата");
        Assert.AreEqual(alone, Candidates(UserFloorScene(offerGround: true)),
            "земля не должна добавлять второго кандидата к плоскости пола");
    }

    [Test]
    public void FloorWhoseTopIsAnotherPlane_DoesNotServeTheGround()
    {
        var raisedFloor = At(Make("Raised", new Vector3Int(3000, 18, 3000)),
            new Vector3(0f, 1.0f - 9f * MM, 0f));
        var scene = new List<ElementGeometry> { raisedFloor };

        Assert.IsTrue(GroundSnapGeometry.AppendTo(scene, ImpliedGround.At(0f), NoMovedPart, AtOrigin),
            "верх этого пола на 1 м выше земли: на земле он стоять не помогает");
        Assert.AreEqual(2, scene.Count, "пол и земля - две разные плоскости");
    }

    [Test]
    public void ThePartBeingMoved_NeverCountsAsServingTheGround()
    {
        var sunkFlat = Floor("Sunk");
        var scene = new List<ElementGeometry> { sunkFlat };

        Assert.IsTrue(GroundSnapGeometry.AppendTo(scene, ImpliedGround.At(0f), sunkFlat.Id, AtOrigin),
            "деталь, которую тащат, не может быть опорой самой себе");
    }

    private static List<ElementGeometry> FloorSceneFor(Vector3 under)
    {
        var scene = new List<ElementGeometry> { Floor() };
        GroundSnapGeometry.AppendTo(scene, ImpliedGround.At(0f), NoMovedPart, under);
        return scene;
    }

    [Test]
    public void BoardBesideTheFloor_OutsideItsOutline_SnapsToTheGround()
    {
        var beside = new Vector3(3f, 0f, 0f);

        var r = Snap(Board(), FloorSceneFor(beside), BoardCentreWithBottomAt(15f * MM) + beside);

        Assert.IsTrue(r.snapped, "вне контура пола прилипание к земле должно остаться");
        Assert.AreEqual(200f * MM, r.position.y, Tol, "низ детали встаёт на y = 0");
        Assert.AreEqual(GroundSnapGeometry.Name, r.targetName, "здесь пола под деталью нет - цель земля");
    }

    [Test]
    public void BoardOverTheFloor_OneTarget_TheFloor_SameY()
    {
        var over = new Vector3(0.5f, 0f, -0.4f);

        var scene = FloorSceneFor(over);
        var r = Snap(Board(), scene, BoardCentreWithBottomAt(15f * MM) + over);

        Assert.AreEqual(1, scene.Count, "над полом земля не предлагается");
        Assert.AreEqual("Floor", r.targetName, "цель одна - пол пользователя");
        Assert.AreEqual(200f * MM, r.position.y, Tol, "та же высота, что у земли");
    }

    [TestCase(1.4f, 1)]
    [TestCase(1.6f, 2)]
    public void GroundOffer_FollowsTheFloorEdge_AtTheMovedPartCentre(float x, int expectedCount)
    {
        var scene = FloorSceneFor(new Vector3(x, 0f, 0f));

        Assert.AreEqual(expectedCount, scene.Count,
            "пол 3000 мм кончается на x = 1,5 м: левее земля не нужна, правее - нужна");
    }

    [Test]
    public void ResizeBottomEdge_BesideTheFloor_SnapsOntoTheGround()
    {
        var beside = new Vector3(4f, 0f, 0f);
        var self = At(Board(), BoardCentreWithBottomAt(20f * MM) + beside);
        var bottom = self.Faces[3];

        bool found = ResizeSnap.SnapDelta(bottom.center, bottom.normal, bottom.rightAxis,
            bottom.upAxis, bottom.size, FloorSceneFor(beside), self, Threshold, out float gap);

        Assert.IsTrue(found, "ручка вне контура пола прилипает к земле");
        Assert.AreEqual(20f * MM, gap, Tol, "кромка дотягивается до y = 0");
    }

    [Test]
    public void ResizeBottomEdge_20mmAboveGround_SnapsOntoIt()
    {
        var self = At(Board(), BoardCentreWithBottomAt(20f * MM));
        var bottom = self.Faces[3];

        bool found = ResizeSnap.SnapDelta(bottom.center, bottom.normal, bottom.rightAxis,
            bottom.upAxis, bottom.size, EmptySceneWithGround(), self, Threshold, out float gap);

        Assert.IsTrue(found, "ручка нижней кромки у земли прилипает к ней");
        Assert.AreEqual(20f * MM, gap, Tol, "кромка дотягивается вниз ровно до y = 0");
    }

    [Test]
    public void ResizeBottomEdge_300mmAboveGround_DoesNotSnap()
    {
        var self = At(Board(), BoardCentreWithBottomAt(300f * MM));
        var bottom = self.Faces[3];

        bool found = ResizeSnap.SnapDelta(bottom.center, bottom.normal, bottom.rightAxis,
            bottom.upAxis, bottom.size, EmptySceneWithGround(), self, Threshold, out _);

        Assert.IsFalse(found, "в 300 мм от земли ручка не прилипает");
    }

    [Test]
    public void ResizeBottomEdge_WithUserFloor_SameGapAsFloorAlone()
    {
        var self = At(Board(), BoardCentreWithBottomAt(20f * MM));
        var bottom = self.Faces[3];
        var withFloor = UserFloorScene(offerGround: true);

        bool found = ResizeSnap.SnapDelta(bottom.center, bottom.normal, bottom.rightAxis,
            bottom.upAxis, bottom.size, withFloor, self, Threshold, out float gap);

        Assert.IsTrue(found, "пол обслуживает ручку сам");
        Assert.AreEqual(20f * MM, gap, Tol, "зазор до плоскости y = 0 тот же, что у пола");
        Assert.AreEqual(1, withFloor.Count, "пол обслуживает сам, земля не добавлена");
    }

    [Test]
    public void GroundFaceOrder_KeepsTheBoxContract_TopFacesUp()
    {
        var ground = GroundSnapGeometry.Of(ImpliedGround.At(0.5f));

        Assert.AreEqual(Face.BoxFaceCount, ground.Faces.Length, "земля - коробка: шесть граней");
        Assert.AreEqual(Vector3.up, ground.Faces[2].normal, "индекс 2 = +Y: порядок граней — контракт");
        Assert.AreEqual(0.5f, ground.Faces[2].center.y, Tol, "верхняя грань стоит на высоте земли");
        Assert.AreEqual(Vector3.down, ground.Faces[3].normal, "нижняя грань смотрит вниз");
    }
}
