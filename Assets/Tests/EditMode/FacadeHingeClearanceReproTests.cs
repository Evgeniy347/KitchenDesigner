using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сцена пользователя, шкаф B2: дверь 537×716×18 с зазорами по 2 мм,
/// петля справа, а над ней в столешницу врезана мойка 500×188×500. Мойка
/// свисает на 6 мм ПРАВЕЕ двери — ровно там петля, — перекрывает её верхние
/// 54 мм по высоте и выходит на 3 мм перед плоскостью фасада.
///
/// Дверь открывалась на 12° вместо 96°. Ось касания у такого соседа — Z,
/// толщина двери, поэтому тень режет мойку по X и по Y и оставляет боковой
/// кусок шириной 6 мм ЦЕЛЫМ по глубине — вместе со слоем внутри собственной
/// толщины двери. А чашечная петля утоплена внутрь полотна: передняя кромка
/// у петли описывает дугу радиусом 8,5 мм и заходит на 2 мм за свою закрытую
/// кромку, не покидая своей глубины. Она и упиралась в этот кусок.
///
/// До 0f4809fa сосед, которого дверь касается закрытой, выбрасывался целиком,
/// и мойка не мешала. Возвращать то правило нельзя: на нём фасад проходил
/// сквозь боковину высокого модуля (см. FacadeOpeningReproTests).</summary>
public class FacadeHingeClearanceReproTests
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

    private FacadeElement MakeDoor()
    {
        var go = ElementFactory.CreateFacade(new Vector3Int(537, 716, 18), "B2_door",
            Vector3.zero, 2, 2, 2, 2);
        _spawned.Add(go);
        var facade = go.GetComponent<FacadeElement>();
        facade.Mode = DoorMode.HingeFrontRight;
        return facade;
    }

    private void MakeSink() =>
        _spawned.Add(ElementFactory.CreatePart(new Vector3Int(500, 188, 500), "Moyka",
            new Vector3(0.0265f, 0.400f, -0.238f)));

    private static float OpenedAngleDeg(FacadeElement facade) =>
        Quaternion.Angle(facade.ClosedRotation, facade.transform.rotation);

    private static float Open(FacadeElement facade)
    {
        facade.SetOpen(true);
        facade.StepDoor(1f);
        return OpenedAngleDeg(facade);
    }

    [Test]
    public void Facade_WithASinkOverhangingItsHingeEdge_StillOpens()
    {
        var facade = MakeDoor();
        MakeSink();

        Assert.GreaterOrEqual(Open(facade), 85f,
            "мойка стоит ЗА плоскостью фасада и лишь свисает мимо его кромки — "
            + "в слое самой двери сосед не препятствие, там ходит петля");
    }

    [Test]
    public void Facade_WithoutTheSink_OpensToTheHingeStop()
    {
        var facade = MakeDoor();

        Assert.AreEqual(FacadeDoor.CupMaxAngleDeg, Open(facade), 0.5f,
            "контроль: без мойки та же дверь доходит до упора чашечной петли, "
            + "иначе «открывается» было бы неотличимо от «упирается чуть позже»");
    }

    [Test]
    public void Facade_WithTheSinkAndAPanelInFrontOfIt_StillStops()
    {
        var facade = MakeDoor();
        MakeSink();
        _spawned.Add(ElementFactory.CreatePart(new Vector3Int(537, 716, 18), "Panel",
            new Vector3(0f, 0f, 0.15f)));

        Assert.Less(Open(facade), 85f,
            "второй контроль: мойка гасится потому, что лежит в слое двери, а не "
            + "потому, что проверка выключена — щит в 150 мм перед фасадом "
            + "по-прежнему останавливает его");
    }
}
