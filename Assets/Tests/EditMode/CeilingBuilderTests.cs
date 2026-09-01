using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class CeilingBuilderTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        CeilingBuilder.Clear();
        PartRegistry.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        CeilingBuilder.Clear();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private GameObject SpawnWall(string name, Vector3Int dimsMM, Vector3 position)
    {
        var go = ElementFactory.CreateWall(dimsMM, name, position);
        _spawned.Add(go);
        return go;
    }

    [Test]
    public void Rebuild_WithoutWalls_LeavesNoCeiling()
    {
        CeilingBuilder.Rebuild();

        Assert.IsFalse(CeilingBuilder.Exists,
            "потолок строится только по стенам: без них фоторежим не должен накрывать сцену плитой");
    }

    [Test]
    public void Rebuild_DropsTheCeilingItBuiltBefore()
    {
        SpawnWall("Wall_Rebuild", new Vector3Int(3000, 2500, 100), new Vector3(0f, 1.25f, 0f));

        CeilingBuilder.Rebuild();
        Assert.IsTrue(CeilingBuilder.Exists);
        var first = CeilingBuilder.Slab;

        CeilingBuilder.Rebuild();
        Assert.IsTrue(CeilingBuilder.Exists);
        Assert.IsTrue(first == null,
            "повторная сборка обязана убрать прежнюю плиту, иначе потолки копятся слоями");
    }

    [Test]
    public void Ceiling_HasNoCollider_SoClicksAndRaycastsPassThrough()
    {
        SpawnWall("Wall_NoCollider", new Vector3Int(3000, 2500, 100), new Vector3(0f, 1.25f, 0f));

        CeilingBuilder.Rebuild();

        var slab = CeilingBuilder.Slab;
        Assert.IsNotNull(slab);
        Assert.IsNull(slab!.GetComponent<Collider>(),
            "потолок — декорация: с коллайдером он перехватывал бы выбор деталей сверху");
    }

    [Test]
    public void Ceiling_IsNotRegisteredAsAPart()
    {
        SpawnWall("Wall_NotAPart", new Vector3Int(3000, 2500, 100), new Vector3(0f, 1.25f, 0f));
        int partsBefore = PartRegistry.GetAll().Count;

        CeilingBuilder.Rebuild();

        Assert.AreEqual(partsBefore, PartRegistry.GetAll().Count,
            "потолок временный и не деталь: попав в реестр, он ушёл бы в спецификацию и в сохранение");
    }

    [Test]
    public void Ceiling_CastsShadow_SoSunlightEntersOnlyThroughOpenings()
    {
        SpawnWall("Wall_Shadow", new Vector3Int(3000, 2500, 100), new Vector3(0f, 1.25f, 0f));

        CeilingBuilder.Rebuild();

        var renderer = CeilingBuilder.Slab!.GetComponent<MeshRenderer>();
        Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.On, renderer.shadowCastingMode,
            "плита перекрывает солнце сверху — без тени комната освещалась бы сквозь потолок");
        Assert.IsTrue(renderer.receiveShadows);
    }

    [Test]
    public void Ceiling_OverLoweredWall_SitsAtTheFullWallTop()
    {
        var go = SpawnWall("Wall_Lowered", new Vector3Int(3000, 2500, 100), new Vector3(0f, 1.25f, 0f));
        var wall = go.GetComponent<Wall>();

        CeilingBuilder.Rebuild();
        float fullTop = CeilingBuilder.Slab!.transform.position.y;

        wall.SetLowered(true, 0.4f);
        Assert.IsTrue(wall.IsLowered, "стена действительно опущена — иначе тест ничего не проверяет");

        CeilingBuilder.Rebuild();

        Assert.AreEqual(fullTop, CeilingBuilder.Slab!.transform.position.y, 1e-3f,
            "опущенные стены восстанавливаются перед замером: иначе потолок падал бы вместе с ними");
        Assert.IsFalse(wall.IsLowered, "замер вернул стену на полную высоту");
    }
}
