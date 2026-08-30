using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Дочерние коробки приборов. Клик по прибору ловит ОДИН коллайдер на корне,
/// поэтому у детей коллайдеров быть не должно — и не только ради лишних
/// рейкастов: в WebGL-сборке линкер вырезает неиспользуемые классы коллайдеров,
/// и примитив всё равно приходит без них (см. память проекта про стриппинг).
/// Раньше это стояло комментарием в четырёх EnsureChildren подряд.
/// </summary>
public class ApplianceChildGeometryTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    private T Spawn<T>() where T : KitchenElement
    {
        var go = new GameObject(typeof(T).Name);
        _spawned.Add(go);
        var el = go.AddComponent<T>();
        el.PartName = typeof(T).Name;
        el.ApplyDimensions();
        return el;
    }

    private static void AssertOnlyTheRootIsClickable(KitchenElement el)
    {
        var root = el.GetComponent<Collider>();
        Assert.IsNotNull(root, el.DisplayTypeName + ": клик обязан попадать в прибор");

        int childColliders = 0;
        foreach (Transform child in el.transform)
            childColliders += child.GetComponents<Collider>().Length;

        Assert.AreEqual(0, childColliders,
            el.DisplayTypeName + ": коллайдеры детей лишние — клик ловит корень, а в WebGL-сборке "
            + "примитив приходит без коллайдера вовсе");
    }

    [Test]
    public void Cooktop_OnlyTheRootCarriesACollider()
    {
        var cooktop = Spawn<CooktopElement>();
        Assert.Greater(cooktop.transform.childCount, 0, "варочная строится дочерними коробками");
        AssertOnlyTheRootIsClickable(cooktop);
    }

    [Test]
    public void Sink_OnlyTheRootCarriesACollider()
    {
        var sink = Spawn<SinkElement>();
        Assert.AreEqual(SinkMesh.CHILD_COUNT, sink.transform.childCount,
            "борт, чаша и смеситель — четырнадцать коробок и цилиндров");
        AssertOnlyTheRootIsClickable(sink);
    }

    [Test]
    public void Dishwasher_Base_WearsTheDarkDoorColour_NotTheLightTankOne()
    {
        var dishwasher = Spawn<DishwasherElement>();
        var basePlateRenderer = dishwasher.transform.Find("Base")!.GetComponent<MeshRenderer>();
        var doorRenderer = dishwasher.transform.Find("Door")!.GetComponent<MeshRenderer>();
        var tankRenderer = dishwasher.transform.Find("BodyBack")!.GetComponent<MeshRenderer>();

        Assert.AreSame(doorRenderer.sharedMaterial, basePlateRenderer.sharedMaterial,
            "основание у настоящей машины тёмное (чёрный поддон): светлая нержавейка бака "
            + "под фасадом смотрелась бы полкой");
        Assert.AreNotSame(tankRenderer.sharedMaterial, basePlateRenderer.sharedMaterial);
    }

    [Test]
    public void Dishwasher_OnlyTheRootCarriesACollider()
    {
        var dishwasher = Spawn<DishwasherElement>();
        AssertOnlyTheRootIsClickable(dishwasher);
    }

    [Test]
    public void Oven_OnlyTheRootCarriesACollider()
    {
        var oven = Spawn<OvenElement>();
        AssertOnlyTheRootIsClickable(oven);
    }

    [Test]
    public void FreeCooktop_HasNoBurners_TheyBelongToAModelNotToAnyGlassRectangle()
    {
        var free = Spawn<CooktopElement>();
        Assert.AreEqual(CooktopMesh.PLATE_AND_BODY_COUNT, free.transform.childCount,
            "у свободной варочной только плита и короб выреза: рисунок принадлежит модели");

        free.Model = CooktopElement.MODEL_BOSCH_PUE611BB5E;
        Assert.AreEqual(CooktopMesh.PLATE_AND_BODY_COUNT + CooktopMesh.DECOR_COUNT,
            free.transform.childCount,
            "у готовой модели появляются четыре конфорки и панель управления");
    }
}
