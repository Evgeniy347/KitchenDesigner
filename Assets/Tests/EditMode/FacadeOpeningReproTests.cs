using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сцена пользователя из docs/example.save.json: верхний шкаф A34K1
/// стоит вплотную к боковине высокого модуля A4, петли фасада — с её стороны,
/// и сама боковина (552 мм глубиной) торчит на 200 мм ПЕРЕД плоскостью
/// фасада. Фасад обязан открыться примерно на 90°: чашечная петля утапливает
/// его кромку внутрь, и мимо боковины он проходит.
///
/// Он не открывался. Два дефекта разом:
/// 1. Проверка раскрытия сравнивала ОСЕВЫЕ габариты, а у двери 670 мм на
///    повороте AABB раздувается в квадрат 670×670 — «столкновение» находилось
///    между противоположными углами коробки, и фасад замирал на 18°.
/// 2. Сосед, которого фасад касается в закрытом виде, исключался ЦЕЛИКОМ —
///    а боковина касается его кромкой. Почини только первое, и фасад ушёл бы
///    на все 110°, прорезав боковину на 38 мм.</summary>
public class FacadeOpeningReproTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private (FacadeElement facade, KitchenElement side) BuildCornerCase()
    {
        var facadeGo = ElementFactory.CreateFacade(new Vector3Int(670, 896, 18),
            "A34K1_upper_door_L", new Vector3(1.244f, 1.810f, -2.199f), 2, 2, 2, 2);
        _spawned.Add(facadeGo);
        var facade = facadeGo.GetComponent<FacadeElement>();
        facade.transform.rotation = ManagedRotation.Euler(0f, -90f, 0f);
        facade.Mode = DoorMode.HingeFrontRight;

        var sideGo = ElementFactory.CreatePart(new Vector3Int(552, 2160, 18),
            "A4_side_L", new Vector3(1.309f, 1.180f, -1.853f));
        _spawned.Add(sideGo);

        return (facade, sideGo.GetComponent<KitchenElement>());
    }

    private static float OpenedAngleDeg(FacadeElement facade) =>
        Quaternion.Angle(facade.ClosedRotation, facade.transform.rotation);

    [Test]
    public void Facade_HingedAtAProtrudingTallSide_OpensAtLeastNinetyDegrees()
    {
        var (facade, _) = BuildCornerCase();

        facade.SetOpen(true);
        facade.StepDoor(1f);

        Assert.GreaterOrEqual(OpenedAngleDeg(facade), 85f,
            "мимо боковины фасад проходит: на 90° его тыльная пласть отстоит от неё "
            + "на миллиметр. Замер на 18° — это ложное срабатывание осевого габарита");
    }

    [Test]
    public void Facade_HingedAtAProtrudingTallSide_StopsBeforeCuttingThroughIt()
    {
        var (facade, side) = BuildCornerCase();

        facade.SetOpen(true);
        facade.StepDoor(1f);

        Assert.Less(OpenedAngleDeg(facade), 100f,
            "контроль к предыдущему: последние 20° хода чашечной петли уводят фасад "
            + "ЗА плоскость боковины, поэтому 110° здесь недостижимы");

        var boxes = new List<OrientedBox>();
        facade.GetOpenBoxes(facade.DoorProgress, boxes);
        var sideBox = new OrientedBox(side.transform.position, side.transform.rotation,
            side.transform.localScale * 0.5f);

        foreach (var box in boxes)
            Assert.IsFalse(OpeningCollision.Blocks(box, sideBox, out float penetrationMm),
                $"открытый фасад не режет боковину (проникновение {penetrationMm:0.0} мм)");
    }

    [Test]
    public void Facade_WithoutTheTallSide_StillOpensFully()
    {
        var facadeGo = ElementFactory.CreateFacade(new Vector3Int(670, 896, 18),
            "A34K1_upper_door_L", new Vector3(1.244f, 1.810f, -2.199f), 2, 2, 2, 2);
        _spawned.Add(facadeGo);
        var facade = facadeGo.GetComponent<FacadeElement>();
        facade.transform.rotation = ManagedRotation.Euler(0f, -90f, 0f);
        facade.Mode = DoorMode.HingeFrontRight;

        facade.SetOpen(true);
        facade.StepDoor(1f);

        Assert.AreEqual(1f, facade.DoorProgress, 1e-4f,
            "второй контроль: без боковины тот же фасад доходит до упора петли, "
            + "иначе «останавливается на 90°» было бы неотличимо от «не открывается»");
    }
}
