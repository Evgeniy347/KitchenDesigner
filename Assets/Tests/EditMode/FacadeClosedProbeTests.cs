using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>DrawerLinks.WithFacadeClosed — проверка навески всегда идёт по
/// ЗАКРЫТОЙ позе фасада.
///
/// Здесь живёт причина, которая раньше была комментарием в DrawerLinks.cs:
/// подменяем позу ТОЛЬКО у пассажира и ТОЛЬКО пока хозяин сдвинут; в остальных
/// случаях подмена холостая, а у закрытой машины ещё и вредная — «фасад
/// оторвали» перестало бы находиться.</summary>
public class FacadeClosedProbeTests
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
    }

    private FacadeElement MakeFacade(Vector3 pos)
    {
        var go = ElementFactory.CreateFacade(new Vector3Int(600, 700, 18), "Фасад", pos, 2, 2, 2, 2);
        _spawned.Add(go);
        return go.GetComponent<FacadeElement>();
    }

    [Test]
    public void DisplacedPassenger_IsProbedAtItsClosedPose_AndPutBackAfterwards()
    {
        var facade = MakeFacade(new Vector3(0f, 0.4f, 0f));
        var closedPos = facade.transform.position;
        facade.IsPassenger = true;
        facade.CaptureClosedPose();
        var carriedTo = new Vector3(0f, 0.05f, 0.6f);
        facade.transform.position = carriedTo;

        Vector3 seen = Vector3.zero;
        DrawerLinks.WithFacadeClosed(facade, displaced: true, () =>
        {
            seen = facade.transform.position;
            return 0;
        });

        Assert.AreEqual(closedPos, seen,
            "фасад-пассажир едет на дверце хозяина; мерить по нему «навешен ли фасад» "
            + "бессмысленно — навеска не меняется от того, открыли машину или нет");
        Assert.AreEqual(carriedTo, facade.transform.position,
            "после проверки поза обязана вернуться: иначе анимация дверцы дёрнется");
    }

    [Test]
    public void ClosedHost_IsProbedByTheLiveTransform_SoATornOffFacadeIsStillFound()
    {
        var facade = MakeFacade(new Vector3(0f, 0.4f, 0f));
        facade.IsPassenger = true;
        var draggedTo = new Vector3(2f, 0.4f, 0f);
        facade.transform.position = draggedTo;

        Vector3 seen = Vector3.zero;
        DrawerLinks.WithFacadeClosed(facade, displaced: false, () =>
        {
            seen = facade.transform.position;
            return 0;
        });

        Assert.AreEqual(draggedTo, seen,
            "дверца закрыта — подставлять хранимую закрытую позу нельзя: она устарела бы "
            + "ровно тогда, когда фасад перетащили мышью, и «фасад оторвали» перестало бы "
            + "находиться вовсе");
    }

    [Test]
    public void NonPassenger_IsNeverSubstituted_EvenWhileTheHostIsDisplaced()
    {
        var facade = MakeFacade(new Vector3(0f, 0.4f, 0f));
        Assert.IsFalse(facade.IsPassenger, "самостоятельная дверца пассажиром не является");

        facade.SetOpen(true);
        facade.StepDoor(1f);
        var standsAt = facade.transform.position;
        Assert.AreNotEqual(facade.ClosedPosition, standsAt,
            "распахнутая дверца стоит НЕ в закрытой позе — только тогда подмену вообще видно");

        Vector3 seen = Vector3.zero;
        DrawerLinks.WithFacadeClosed(facade, displaced: true, () =>
        {
            seen = facade.transform.position;
            return 0;
        });

        Assert.AreEqual(standsAt, seen,
            "не-пассажир своей позой не двигает: у него ValidationPosition и так берёт "
            + "закрытую позу, и подмена была бы холостой");
    }
}
