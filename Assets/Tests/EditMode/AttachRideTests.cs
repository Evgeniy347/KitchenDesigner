using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Езда прикреплённых деталей за АНИМАЦИЕЙ родителя (AttachRider) и
/// за его РЕДАКТИРУЮЩИМ переносом (AttachMove).
///
/// Здесь живут причины, которые раньше были комментариями в AttachRider.cs и
/// AttachMove.cs: передаётся только поза, а не масштаб; поза покоя ребёнка
/// запоминается на первом кадре езды, иначе ошибка копится; кольцо в связях
/// обязано останавливать обход, а не вешать кадр; в мультивыделении родитель и
/// ребёнок могут быть выбраны оба, и второй сдвиг увёз бы ребёнка вдвое.</summary>
public class AttachRideTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
    }

    private KitchenElement MakeBoard(string name, Vector3 pos)
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 18, 500), name, pos);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private FacadeElement MakeFacade(string name, Vector3 pos)
    {
        var go = ElementFactory.CreateFacade(new Vector3Int(600, 716, 18), name, pos, 2, 2, 2, 2);
        _spawned.Add(go);
        return go.GetComponent<FacadeElement>();
    }

    [Test]
    public void RidingBackAndForth_ManyTimes_LeavesNoDrift()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        var bottom = MakeBoard("Дно", new Vector3(0f, -0.35f, 0.25f));
        bottom.AttachedToName = facade.PartName;
        var restPos = bottom.transform.position;

        for (int cycle = 0; cycle < 5; cycle++)
        {
            facade.SetOpen(true);
            for (int i = 0; i < 12; i++) { facade.StepDoor(0.05f); AttachRider.Step(); }
            facade.SetOpen(false);
            for (int i = 0; i < 12; i++) { facade.StepDoor(0.05f); AttachRider.Step(); }
        }

        Assert.AreEqual(restPos.x, bottom.transform.position.x, 1e-5f,
            "ребёнок ездит от ЗАПОМНЕННОЙ позы покоя, а не от позиции прошлого кадра — "
            + "иначе за десяток открываний ошибка накопилась бы в видимый сдвиг");
        Assert.AreEqual(restPos.y, bottom.transform.position.y, 1e-5f);
        Assert.AreEqual(restPos.z, bottom.transform.position.z, 1e-5f);
    }

    [Test]
    public void RidingParent_DoesNotStretchItsChildren()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        var bottom = MakeBoard("Дно", new Vector3(0f, -0.35f, 0.25f));
        bottom.AttachedToName = facade.PartName;
        var childScale = bottom.transform.localScale;
        var childDims = bottom.DimensionsMM;

        facade.SetOpen(true);
        facade.StepDoor(1f);
        AttachRider.Step();

        Assert.AreEqual(childScale, bottom.transform.localScale,
            "передаётся ТОЛЬКО поза: будь это Transform-иерархия Unity, масштаб фасада "
            + "растянул бы дно вместе с ним");
        Assert.AreEqual(childDims, bottom.DimensionsMM, "и габарит ребёнка остаётся своим");
        Assert.IsNull(bottom.transform.parent, "деталь остаётся самостоятельным объектом сцены");
    }

    [Test]
    public void ThreeLevelChain_RidesFromTheTopDown()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        var bottom = MakeBoard("Дно", new Vector3(0f, -0.35f, 0.25f));
        var back = MakeBoard("Задняя", new Vector3(0f, -0.2f, 0.25f));
        bottom.AttachedToName = facade.PartName;
        back.AttachedToName = bottom.PartName;
        var bottomRest = bottom.transform.position;
        var backRest = back.transform.position;

        facade.SetOpen(true);
        facade.StepDoor(1f);
        AttachRider.Step();

        Assert.AreNotEqual(bottomRest, bottom.transform.position, "дно уехало за фасадом");
        Assert.AreNotEqual(backRest, back.transform.position,
            "и задняя стенка — за дном: обход идёт сверху вниз, поэтому поза родителя "
            + "к моменту расчёта ребёнка уже посчитана");
        Assert.AreEqual(backRest - bottomRest, back.transform.position - bottom.transform.position,
            "жёсткая связка: взаимное смещение внутри сборки не меняется");
    }

    [Test]
    public void CycleBroughtInFromASaveFile_DoesNotHangTheStep()
    {
        var a = MakeBoard("A", new Vector3(0f, 0f, 0f));
        var b = MakeBoard("B", new Vector3(0.7f, 0f, 0f));
        a.AttachedToName = b.PartName;
        b.AttachedToName = a.PartName;
        var posA = a.transform.position;
        var posB = b.transform.position;

        Assert.DoesNotThrow(() => AttachRider.Step(),
            "кольцо в связях можно принести файлом проекта: шаг обязан завершиться, "
            + "а не вешать кадр");
        Assert.AreEqual(posA, a.transform.position, "и никого не сдвинуть:");
        Assert.AreEqual(posB, b.transform.position,
            "у элементов кольца родитель находится всегда, поэтому корнем обхода никто "
            + "из них не становится и в цикл шаг не заходит вовсе");
    }

    [Test]
    public void ExpandWithDescendants_WhenBothAreSelected_AddsTheChildOnlyOnce()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        var bottom = MakeBoard("Дно", new Vector3(0f, -0.35f, 0.25f));
        bottom.AttachedToName = facade.PartName;

        var set = new List<KitchenElement> { facade, bottom };
        AttachMove.ExpandWithDescendants(set);

        Assert.AreEqual(2, set.Count,
            "в мультивыделении родитель и ребёнок вполне могут быть выбраны оба; второй "
            + "раз в наборе ребёнок получил бы дельту дважды и уехал вдвое дальше");
        CollectionAssert.AllItemsAreUnique(set);
    }

    [Test]
    public void FollowersCommand_ForARidingChild_MovesItsRestPose_NotItsAnimatedTransform()
    {
        var facade = MakeFacade("Фасад", Vector3.zero);
        var bottom = MakeBoard("Дно", new Vector3(0f, -0.35f, 0.25f));
        bottom.AttachedToName = facade.PartName;
        var restPos = bottom.transform.position;

        facade.SetOpen(true);
        facade.StepDoor(1f);
        AttachRider.Step();
        Assert.IsTrue(bottom.IsAttachRidden, "дно сейчас едет — его трансформ принадлежит райдеру");

        var shift = new Vector3(0.4f, 0f, 0f);
        var cmd = AttachMove.FollowersCommand(facade, Vector3.zero, facade.ClosedRotation,
            shift, facade.ClosedRotation);
        Assert.IsNotNull(cmd);
        AttachLinks.ForceRest(bottom);
        cmd!.Execute();

        Assert.AreEqual(restPos + shift, bottom.transform.position,
            "сдвигать надо ПОЗУ ПОКОЯ: возьми команда анимированный трансформ, после "
            + "закрытия дверцы деталь вернулась бы не туда, куда её перетащили");
    }
}
