using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Земля пустой сцены — картинка, а не деталь: квадрат без KitchenElement и без
/// коллайдера. Его нельзя выбрать, на него нельзя прилипнуть, он не загораживает ни один
/// луч (выбор, замер, лампа, камера) и не лежит в реестре деталей. Рисуется, пока в сцене
/// нет ни одного пользовательского пола.</summary>
public class GroundQuadTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        EditModeManager.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        EditModeManager.Reset();
    }

    private GroundQuad MakeGround()
    {
        var ground = GroundQuad.Create();
        _spawned.Add(ground.gameObject);
        return ground;
    }

    private FloorElement MakeFloor()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = new Vector3(0f, -0.05f, 0f);
        _spawned.Add(go);
        var floor = go.AddComponent<FloorElement>();
        floor.PartName = "Пол";
        floor.DimensionsMM = new Vector3Int(2000, 100, 2000);
        return floor;
    }

    private static Ray FromAbove => new Ray(new Vector3(0.3f, 2f, 0.2f), Vector3.down);

    private static KitchenElement? Pick(Ray ray, bool shift)
    {
        Physics.SyncTransforms();
        return SelectionManager.RaycastTransparentAware(ray, shift);
    }

    [Test]
    public void Create_BuildsAFlatSquareOfTheDefaultSize_OnTheGroundLevel()
    {
        var ground = MakeGround();

        var bounds = ground.GetComponent<MeshFilter>().sharedMesh.bounds;
        float side = AppConstants.GROUND_QUAD_SIZE_MM * AppConstants.MM_TO_UNITS;
        Assert.AreEqual(side, bounds.size.x, 1e-4f, "ширина квадрата — размер земли по умолчанию");
        Assert.AreEqual(side, bounds.size.z, 1e-4f, "и длина тоже");
        Assert.AreEqual(0f, bounds.size.y, 1e-6f, "квадрат плоский");
        Assert.AreEqual(0f, ground.transform.position.y, 1e-6f, "лежит на нулевой отметке, где стоят детали");
        Assert.AreSame(ground, GroundQuad.Instance, "приложение находит землю через Instance");
    }

    [Test]
    public void EmptyScene_ShowsGroundQuad_NotSelectable()
    {
        var ground = MakeGround();

        Assert.IsTrue(ground.IsShown, "в пустой сцене земля нарисована");
        Assert.IsNull(ground.GetComponent<Collider>(), "у земли нет коллайдера: луч проходит сквозь неё");
        Assert.IsNull(ground.GetComponent<KitchenElement>(), "земля — не деталь");
        Assert.AreEqual(0, PartRegistry.GetAll().Count, "и не лежит в реестре деталей: её не выберут и не прилипнут");
        Assert.IsNull(Pick(FromAbove, shift: false), "клик по земле — клик в пустоту, то есть снятие выделения");
        Assert.IsNull(Pick(FromAbove, shift: true), "то же с Shift");
        Physics.SyncTransforms();
        Assert.IsFalse(Physics.Raycast(FromAbove), "сырой луч не видит земли: замер, лампа и камера не обязаны её обходить");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Floor_TopClick_WithTheGroundQuadPresent_SelectsTheFloorInRoomMode(bool shift)
    {
        EditModeManager.SetMode(EditMode.Room);
        var floor = MakeFloor();
        MakeGround();

        var picked = Pick(FromAbove, shift);

        Assert.AreEqual(floor, picked, "земля на одной плоскости с верхом пола не перехватывает клик сверху");
        Assert.IsTrue(EditModeManager.IsInteractable(picked!), "в режиме помещения пол доступен");
    }

    [Test]
    public void UserFloor_HidesTheGroundQuad_AndTheLastFloorGoingBringsItBack()
    {
        var ground = MakeGround();
        var floor = MakeFloor();
        FloorElement.RefreshGroundVisibility();

        Assert.IsTrue(ground.CoveredByUserFloor, "посылка: пол накрывает землю");
        Assert.IsFalse(ground.IsShown, "с полом земля не рисуется: две плоскости на y=0 мерцали бы");

        Object.DestroyImmediate(floor.gameObject);
        FloorElement.RefreshGroundVisibility();

        Assert.IsFalse(ground.CoveredByUserFloor, "полов нет");
        Assert.IsTrue(ground.IsShown, "земля снова нарисована");
    }

    [Test]
    public void Ground_CreatedUnderAnExistingFloor_IsBornHidden()
    {
        MakeFloor();

        var ground = MakeGround();

        Assert.IsFalse(ground.IsShown,
            "порядок создания пола и земли (загрузка проекта, Bootstrap) не решает, видна ли земля");
    }

    [Test]
    public void ViewVisibility_HidesTheGround_AndNeverReopensItUnderAUserFloor()
    {
        var ground = MakeGround();

        ground.ApplyViewVisibility(false);
        Assert.IsFalse(ground.IsShown, "уровень скрыт видом — земля скрыта");
        ground.ApplyViewVisibility(true);
        Assert.IsTrue(ground.IsShown, "вид вернулся — земля тоже");

        MakeFloor();
        FloorElement.RefreshGroundVisibility();
        foreach (var visible in new[] { false, true, false, true })
        {
            ground.ApplyViewVisibility(visible);
            Assert.IsFalse(ground.IsShown, $"смена вида (visible={visible}) не включает землю под полом");
        }
    }

    [TestCase(false, true, true)]
    [TestCase(true, true, false)]
    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    public void ShownWith_NeedsNoFloorAndAVisibleLevel(bool covered, bool visibleInView, bool expected)
    {
        Assert.AreEqual(expected, GroundQuad.ShownWith(covered, visibleInView),
            "земля видна только без пользовательского пола и на видимом уровне");
    }

    [Test]
    public void Destroying_TheGround_ClearsTheInstance()
    {
        var ground = MakeGround();
        Object.DestroyImmediate(ground.gameObject);

        Assert.IsTrue(GroundQuad.Instance == null, "уничтоженную землю приложение не находит");
    }
}
