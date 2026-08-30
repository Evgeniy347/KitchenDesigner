using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Заполнение створки двери и стеклянная вставка сборного фасада.
///
/// Здесь живут две последние причины, которые были комментариями в
/// Core/Elements: смена типа створки меняет НЕ только материал, но и толщину
/// панели (глухая занимает всю глубину обвязки, стекло — три миллиметра);
/// а стеклянная вставка сборного фасада остаётся без коллайдера, чтобы клик
/// проходил к самому фасаду.</summary>
public class DoorSashTypeTests
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

    private static Transform? Pane(DoorElement door)
    {
        foreach (Transform child in door.transform)
        {
            var found = FindByName(child, "Glass");
            if (found != null) return found;
        }
        return null;
    }

    private static Transform? FindByName(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var found = FindByName(child, name);
            if (found != null) return found;
        }
        return null;
    }

    [Test]
    public void SashType_ChangesTheMaterial_AndThePanelThickness()
    {
        var go = ElementFactory.CreateDoor(new Vector3Int(900, 2050, 100), "Дверь", Vector3.zero);
        _spawned.Add(go);
        var door = go.GetComponent<DoorElement>();

        var pane = Pane(door);
        Assert.IsNotNull(pane, "у створки должна быть панель заполнения");

        door.SashType = DoorSashType.Glass;
        float glassThickness = pane!.localScale.z;
        var glassMaterial = pane.GetComponent<MeshRenderer>().sharedMaterial;

        door.SashType = DoorSashType.Blind;
        float blindThickness = pane.localScale.z;
        var blindMaterial = pane.GetComponent<MeshRenderer>().sharedMaterial;

        Assert.AreNotEqual(glassMaterial, blindMaterial, "материал панели меняется вместе с типом");
        Assert.Greater(blindThickness, glassThickness,
            "и толщина тоже: глухая панель занимает всю глубину обвязки, а стекло — "
            + $"свои {AppConstants.WINDOW_GLASS_THICKNESS_MM} мм. Поменять один только "
            + "материал значит оставить «глухую» створку толщиной со стекло");
        Assert.AreEqual(AppConstants.WINDOW_GLASS_THICKNESS_MM * AppConstants.MM_TO_UNITS,
            glassThickness, 1e-5f);
    }

    [Test]
    public void AssembledFacadeGlass_CarriesNoCollider_SoClicksReachTheFacade()
    {
        var go = ElementFactory.CreateAssembledFacade(new Vector3Int(600, 716, 18),
            "Витрина", Vector3.zero, AssembledFill.Glass);
        _spawned.Add(go);
        var facade = go.GetComponent<AssembledFacadeElement>();
        facade.Fill = AssembledFill.Glass;

        var glass = FindByName(facade.transform, "__Glass");
        Assert.IsNotNull(glass, "у витрины должна быть стеклянная вставка");
        Assert.IsNull(glass!.GetComponent<Collider>(),
            "иначе стекло перехватывало бы клики: выделить сам фасад мышью стало бы "
            + "невозможно, хотя стекло — его часть, а не отдельный объект");
    }
}
