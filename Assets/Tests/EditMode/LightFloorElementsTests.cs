using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Пол (FloorElement) и источник света (LightSourceElement):
/// сериализация, валидация и глобальный выключатель света.</summary>
public class LightFloorElementsTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        LightSourceElement.SetGlobalOn(true);
    }

    private FloorElement CreateFloor()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var floor = go.AddComponent<FloorElement>();
        floor.PartName = "Пол";
        floor.DimensionsMM = new Vector3Int(
            FloorElement.DEFAULT_SIZE_MM,
            FloorElement.DEFAULT_THICKNESS_MM,
            FloorElement.DEFAULT_SIZE_MM);
        // В EditMode OnEnable не вызывается — синхронизируем явно, как фабрика.
        FloorElement.RefreshBasePlateVisibility();
        return floor;
    }

    private LightSourceElement CreateLamp()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _spawned.Add(go);
        var lamp = go.AddComponent<LightSourceElement>();
        lamp.PartName = "Источник света";
        lamp.DimensionsMM = new Vector3Int(
            LightSourceElement.DEFAULT_SIZE_MM,
            LightSourceElement.DEFAULT_SIZE_MM,
            LightSourceElement.DEFAULT_SIZE_MM);
        lamp.EnsureLight();
        lamp.SyncLightState();
        return lamp;
    }

    [Test]
    public void FromElement_Floor_SetsIsFloor()
    {
        var floor = CreateFloor();
        var d = ElementData.FromElement(floor);

        Assert.IsTrue(d.isFloor);
        Assert.IsFalse(d.isLightSource);
    }

    [Test]
    public void FromElement_LightSource_SetsIsLightSource()
    {
        var lamp = CreateLamp();
        var d = ElementData.FromElement(lamp);

        Assert.IsTrue(d.isLightSource);
        Assert.IsFalse(d.isFloor);
    }

    [Test]
    public void LightSource_HasDownwardSpotLight()
    {
        var lamp = CreateLamp();
        Assert.IsNotNull(lamp.PointLight, "у лампы есть Light");
        // Плафон светит направленно вниз (главный поток), а не во все стороны.
        Assert.AreEqual(LightType.Spot, lamp.PointLight!.type);
    }

    [Test]
    public void SetGlobalOn_TogglesAllLights()
    {
        var a = CreateLamp();
        var b = CreateLamp();

        LightSourceElement.SetGlobalOn(false);
        Assert.IsFalse(a.PointLight!.enabled);
        Assert.IsFalse(b.PointLight!.enabled);

        LightSourceElement.SetGlobalOn(true);
        Assert.IsTrue(a.PointLight!.enabled);
        Assert.IsTrue(b.PointLight!.enabled);
    }

    [Test]
    public void Validator_LoneFloor_IsAnchor_NoViolation()
    {
        var floor = CreateFloor();
        var result = ConstraintValidator.Validate(new List<KitchenElement> { floor });

        Assert.IsFalse(result.violations.Contains(floor),
            "пол — якорь, отсутствие контактов не нарушение");
    }

    [Test]
    public void Floor_HidesBasePlate_WhileExists()
    {
        var plate = BasePlate.Create();
        _spawned.Add(plate.gameObject);
        var renderer = plate.GetComponent<MeshRenderer>();
        Assert.IsTrue(renderer.enabled, "без своих полов плита видима");

        var floor = CreateFloor();
        Assert.IsFalse(renderer.enabled, "с собственным полом дефолтная плита скрыта");

        Object.DestroyImmediate(floor.gameObject);
        FloorElement.RefreshBasePlateVisibility();
        Assert.IsTrue(renderer.enabled, "полов нет — плита снова видима");
    }

    [Test]
    public void Validator_FloatingLamp_NoViolation()
    {
        var lamp = CreateLamp();
        lamp.transform.position = new Vector3(0f, 2.2f, 0f);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { lamp });

        Assert.IsFalse(result.violations.Contains(lamp),
            "источник света висит в воздухе штатно");
    }
}
