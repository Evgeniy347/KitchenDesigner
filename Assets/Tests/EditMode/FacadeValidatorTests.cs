using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class FacadeValidatorTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        GroupManager.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
    }

    private FacadeElement MakeFacade(string name, Vector3Int dims, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var f = go.AddComponent<FacadeElement>();
        f.PartName = name;
        f.DimensionsMM = dims;
        PartRegistry.Register(f);
        _spawned.Add(go);
        return f;
    }

    private KitchenElement MakeElement(string name, Vector3Int dims, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    private List<KitchenElement> All() => PartRegistry.GetAll();

    [Test]
    public void GetFaceNormal_DefaultRotation_PointsToPositiveZ()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        var normal = FacadeValidator.GetFaceNormal(f);
        Assert.AreEqual(0f, normal.x, 1e-5f);
        Assert.AreEqual(0f, normal.y, 1e-5f);
        Assert.AreEqual(1f, normal.z, 1e-5f);
    }

    [Test]
    public void GetFaceNormal_Rotated90Y_PointsToPositiveX()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        f.transform.rotation = ManagedRotation.Euler(0f, 90f, 0f);
        var normal = FacadeValidator.GetFaceNormal(f);
        Assert.AreEqual(1f, normal.x, 1e-5f);
        Assert.AreEqual(0f, normal.y, 1e-5f);
        Assert.AreEqual(0f, normal.z, 1e-5f);
    }

    [Test]
    public void IsFacingInward_NoModule_ReturnsFalse()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        Assert.IsFalse(FacadeValidator.IsFacingInward(f));
    }

    [Test]
    public void IsFacingInward_FaceOutward_ReturnsFalse()
    {
        var box = MakeElement("Box", new Vector3Int(600, 400, 500), Vector3.zero);
        // Фасад стоит спереди (z = -0.3), повёрнут на 180° — лицевая грань (+Z локально)
        // смотрит в -Z, то есть наружу от короба.
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), new Vector3(0f, 0f, -0.3f));
        f.transform.rotation = ManagedRotation.Euler(0f, 180f, 0f);
        GroupManager.Link(new List<KitchenElement> { box, f });
        Assert.IsFalse(FacadeValidator.IsFacingInward(f));
    }

    [Test]
    public void IsFacingInward_FaceInward_ReturnsTrue()
    {
        var box = MakeElement("Box", new Vector3Int(600, 400, 500), Vector3.zero);
        // Фасад стоит спереди (z = -0.3) с identity-rotation — лицевая грань (+Z локально)
        // смотрит в +Z, то есть внутрь короба.
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), new Vector3(0f, 0f, -0.3f));
        GroupManager.Link(new List<KitchenElement> { box, f });
        Assert.IsTrue(FacadeValidator.IsFacingInward(f));
    }

    [Test]
    public void FindFaceObstructions_NoObstruction_ReturnsEmpty()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        var all = All();
        var obs = FacadeValidator.FindFaceObstructions(f, all);
        Assert.IsEmpty(obs);
    }

    [Test]
    public void FindFaceObstructions_ElementDirectlyInFront_Detected()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        // 20 мм впереди по нормали +Z.
        var obstacle = MakeElement("Obstacle", new Vector3Int(200, 200, 18), new Vector3(0f, 0f, 0.038f));
        var obs = FacadeValidator.FindFaceObstructions(f, All());
        Assert.AreEqual(1, obs.Count);
        Assert.AreEqual("Obstacle", obs[0].neighbor);
        Assert.Less(obs[0].distanceFromFaceMm, 30f);
    }

    [Test]
    public void FindFaceObstructions_SameModuleElement_Ignored()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        var side = MakeElement("Side", new Vector3Int(18, 400, 500), new Vector3(-0.3f, 0f, 0f));
        GroupManager.Link(new List<KitchenElement> { f, side });
        var obs = FacadeValidator.FindFaceObstructions(f, All());
        Assert.IsEmpty(obs);
    }

    [Test]
    public void FindOpeningViolations_HingedDoorWithObstacle_Detected()
    {
        var f = MakeFacade("F", new Vector3Int(400, 700, 18), new Vector3(0f, 0.35f, 0f));
        f.Mode = DoorMode.HingeFrontLeft;
        // Дверь с левой петлёй распахивается влево-наружу (центр уходит в -X, +Z).
        // Ставим большое препятствие в зоне качания.
        var obstacle = MakeElement("Obstacle", new Vector3Int(400, 700, 18), new Vector3(-0.25f, 0.35f, 0.15f));
        var viol = FacadeValidator.FindOpeningViolations(f, All());
        Assert.AreEqual(1, viol.Count);
        Assert.AreEqual("Obstacle", viol[0].neighbor);
        Assert.Greater(viol[0].collisionAtProgress, 0f);
        Assert.LessOrEqual(viol[0].collisionAtProgress, 1f);
    }

    [Test]
    public void FindOpeningViolations_DrawerWithObstacle_Detected()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        f.Mode = DoorMode.DrawerOut;
        // Препятствие прямо перед ящиком на пути выдвижения (+Z).
        var obstacle = MakeElement("Obstacle", new Vector3Int(400, 300, 18), new Vector3(0f, 0f, 0.3f));
        var viol = FacadeValidator.FindOpeningViolations(f, All());
        Assert.AreEqual(1, viol.Count);
        Assert.AreEqual("Obstacle", viol[0].neighbor);
    }

    [Test]
    public void FindOpeningViolations_ClearPath_ReturnsEmpty()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        f.Mode = DoorMode.HingeFrontLeft;
        var viol = FacadeValidator.FindOpeningViolations(f, All());
        Assert.IsEmpty(viol);
    }
}
