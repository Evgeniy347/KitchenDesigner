using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class FacadeElementTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        BoardRegistry.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in BoardRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        BoardRegistry.Clear();
    }

    private FacadeElement MakeFacade(string name, Vector3Int dims, Vector3 pos, int gapMM = 2)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var f = go.AddComponent<FacadeElement>();
        f.BoardName = name;
        f.DimensionsMM = dims;
        f.GapMM = gapMM;
        BoardRegistry.Register(f);
        _spawned.Add(go);
        return f;
    }

    private KitchenElement MakeElement(string name, Vector3Int dims, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.BoardName = name;
        e.DimensionsMM = dims;
        BoardRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [Test]
    public void Facade_DefaultGap_IsTwoMM()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        Assert.AreEqual(2, f.GapMM);
    }

    [Test]
    public void Facade_Gap_CanBeSet()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero, 5);
        Assert.AreEqual(5, f.GapMM);
    }

    [Test]
    public void Facade_Gap_ClampedToZero()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero, -1);
        Assert.AreEqual(0, f.GapMM);
    }

    [Test]
    public void Facade_EffectiveScale_IncludesGap()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero, 2);
        var phys = f.transform.localScale;
        var effective = f.GetFaces();

        // Gap 2mm → 0.004 units added to each axis
        Assert.AreEqual(phys.x + 0.004f, effective[0].size.x / 300f * 0.018f, 1e-5f);
    }

    [Test]
    public void Facade_Faces_AreLargerThanPhysical_ByGap()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero, 3);
        var physScale = f.transform.localScale;
        var faces = f.GetFaces();

        // Face sizes come from EffectiveScale, not physical scale
        // For a facade with gap 3mm: effective = physical + 2*3*0.001 on each axis
        // The Z faces (front/back) have size = (effectiveX, effectiveY)
        Assert.AreEqual((physScale.x + 0.006f), faces[4].size.x, 1e-5f);
        Assert.AreEqual((physScale.y + 0.006f), faces[4].size.y, 1e-5f);
    }

    [Test]
    public void Facade_Vertices_ExtendedByGap()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero, 2);
        var verts = f.GetVertices();

        // Physical half-extents: 0.2, 0.15, 0.009
        // With gap 2mm: add 0.002 on each side → half-extents: 0.202, 0.152, 0.011
        foreach (var v in verts)
        {
            Assert.LessOrEqual(Mathf.Abs(v.x), 0.202f);
            Assert.LessOrEqual(Mathf.Abs(v.y), 0.152f);
            Assert.LessOrEqual(Mathf.Abs(v.z), 0.011f);
        }
    }

    [Test]
    public void Facade_OverlappingAnotherFacade_GapIncluded()
    {
        // Two facades with gap=2, physical 400x300x18 placed 3mm apart
        // Effective bounds extend 2mm on each side → 4mm total effective width per axis
        // Gap between physical edges = 3mm, effective edges overlap by 1mm → should violate
        var a = MakeFacade("A", new Vector3Int(400, 300, 18), Vector3.zero, 2);
        MakeFacade("B", new Vector3Int(400, 300, 18), new Vector3(0.003f, 0f, 0f), 2);

        var all = BoardRegistry.GetAll();
        var vr = ConstraintValidator.Validate(all);
        Assert.IsTrue(vr.violations.Contains(a), "Effective bounds overlap → violation");
    }

    [Test]
    public void Facade_WithGap_DoesNotOverlap_WhenPhysicalEdgesTouch()
    {
        // Two facades physical edges exactly touching, but gap adds 2mm each side → they overlap
        var a = MakeFacade("A", new Vector3Int(400, 300, 18), Vector3.zero, 0);
        MakeFacade("B", new Vector3Int(400, 300, 18), new Vector3(0.4f, 0f, 0f), 0);

        var all = BoardRegistry.GetAll();
        var vr = ConstraintValidator.Validate(all);
        // With gap=0, effective = physical, touching edges should NOT be a violation
        Assert.IsFalse(vr.violations.Contains(a));
    }

    [Test]
    public void Facade_ElementFactory_CreatesCorrectly()
    {
        var go = ElementFactory.CreateFacade(
            new Vector3Int(600, 400, 18), "TestFacade", Vector3.zero, 3);
        _spawned.Add(go);

        var facade = go.GetComponent<FacadeElement>();
        Assert.IsNotNull(facade);
        Assert.AreEqual("TestFacade", facade.BoardName);
        Assert.AreEqual(3, facade.GapMM);
        Assert.AreEqual(new Vector3Int(600, 400, 18), facade.DimensionsMM);
    }

    [Test]
    public void Facade_PhysicalScale_StaysAtDimensions()
    {
        var f = MakeFacade("F", new Vector3Int(500, 400, 18), Vector3.zero, 4);
        Assert.AreEqual(0.5f, f.transform.localScale.x, 1e-5f);
        Assert.AreEqual(0.4f, f.transform.localScale.y, 1e-5f);
        Assert.AreEqual(0.018f, f.transform.localScale.z, 1e-5f);
    }
}
