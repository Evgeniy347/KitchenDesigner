using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class BasePlateTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    private BasePlate MakePlate()
    {
        var plate = BasePlate.Create();
        _spawned.Add(plate.gameObject);
        return plate;
    }

    private FloorElement MakeFloor()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var floor = go.AddComponent<FloorElement>();
        floor.PartName = "Пол";
        floor.DimensionsMM = new Vector3Int(2000, 100, 2000);
        return floor;
    }

    [Test]
    public void BasePlate_CreatesWithCorrectSize()
    {
        var plate = BasePlate.Create();
        Assert.NotNull(plate);
        Assert.NotNull(plate.Element);

        var element = plate.Element;
        Assert.AreEqual(new Vector3Int(3000, 18, 3000), element.DimensionsMM);

        var scale = plate.transform.localScale;
        Assert.AreEqual(3f, scale.x, 0.001f);
        Assert.AreEqual(0.018f, scale.y, 0.001f);
        Assert.AreEqual(3f, scale.z, 0.001f);

        Object.DestroyImmediate(plate.gameObject);
    }

    [Test]
    public void BasePlate_AloneInTheScene_IsDrawnAndSolid()
    {
        var plate = MakePlate();

        Assert.IsTrue(plate.GetComponent<MeshRenderer>().enabled, "без полов плита нарисована");
        Assert.IsTrue(plate.GetComponent<Collider>().enabled, "и по ней можно попасть лучом: это земля пустой сцены");
    }

    [Test]
    public void BasePlate_Hidden_HasNoCollider()
    {
        var plate = MakePlate();
        MakeFloor();

        FloorElement.RefreshBasePlateVisibility();

        Assert.IsFalse(plate.GetComponent<MeshRenderer>().enabled, "посылка: с полом плита не нарисована");
        Assert.IsFalse(plate.GetComponent<Collider>().enabled,
            "невидимая плита с коллайдером загораживала пол от любого луча (выбор, замер, лампа, камера): "
            + "коллайдер живёт только пока она нарисована");
    }

    [Test]
    public void BasePlate_CreatedUnderAnExistingFloor_IsBornHiddenWithoutCollider()
    {
        MakeFloor();

        var plate = MakePlate();

        Assert.IsFalse(plate.GetComponent<MeshRenderer>().enabled, "плита, созданная под полом, не рисуется");
        Assert.IsFalse(plate.GetComponent<Collider>().enabled,
            "и без коллайдера: порядок создания пола и плиты (загрузка проекта, Bootstrap) не должен решать, "
            + "загораживает ли она пол");
    }

    [Test]
    public void BasePlate_WhenTheLastFloorGoes_ComesBackDrawnAndSolid()
    {
        var plate = MakePlate();
        var floor = MakeFloor();
        FloorElement.RefreshBasePlateVisibility();
        Assert.IsFalse(plate.GetComponent<Collider>().enabled, "посылка: с полом плита выключена");

        Object.DestroyImmediate(floor.gameObject);
        FloorElement.RefreshBasePlateVisibility();

        Assert.IsTrue(plate.GetComponent<MeshRenderer>().enabled, "полов нет — плита снова нарисована");
        Assert.IsTrue(plate.GetComponent<Collider>().enabled, "и снова твёрдая: рендерер и коллайдер ходят парой");
    }
}
