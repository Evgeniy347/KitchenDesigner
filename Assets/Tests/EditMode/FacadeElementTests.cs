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

    private FacadeElement MakeFacade(string name, Vector3Int dims, Vector3 pos,
        int gapL = 2, int gapR = 2, int gapT = 2, int gapB = 2)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var f = go.AddComponent<FacadeElement>();
        f.BoardName = name;
        f.DimensionsMM = dims;
        f.GapLeft = gapL;
        f.GapRight = gapR;
        f.GapTop = gapT;
        f.GapBottom = gapB;
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
    public void Facade_DefaultGap_IsTwoOnAllSides()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        Assert.AreEqual(2, f.GapLeft);
        Assert.AreEqual(2, f.GapRight);
        Assert.AreEqual(2, f.GapTop);
        Assert.AreEqual(2, f.GapBottom);
    }

    [Test]
    public void Facade_Gap_CanSetIndividualSides()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero, 1, 3, 5, 7);
        Assert.AreEqual(1, f.GapLeft);
        Assert.AreEqual(3, f.GapRight);
        Assert.AreEqual(5, f.GapTop);
        Assert.AreEqual(7, f.GapBottom);
    }

    [Test]
    public void Facade_Gap_ClampedToZero()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero, -1, -1, -1, -1);
        Assert.AreEqual(0, f.GapLeft);
        Assert.AreEqual(0, f.GapRight);
        Assert.AreEqual(0, f.GapTop);
        Assert.AreEqual(0, f.GapBottom);
    }

    [Test]
    public void Facade_EffectiveScale_AddsGapOnXAndY_NotZ()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero, 2, 2, 2, 2);
        var faces = f.GetFaces();

        // Z faces (index 4,5) have size = (effectiveX, effectiveY)
        // physical: 0.4, 0.3; gap: 4mm total on each axis → +0.004
        Assert.AreEqual(0.404f, faces[4].size.x, 1e-5f);
        Assert.AreEqual(0.304f, faces[4].size.y, 1e-5f);

        // X faces (index 0,1) have size = (effectiveY, effectiveZ) — Z unchanged
        Assert.AreEqual(0.304f, faces[0].size.x, 1e-5f); // effectiveY
        Assert.AreEqual(0.018f, faces[0].size.y, 1e-5f); // physicalZ (no gap)
    }

    [Test]
    public void Facade_Vertices_ExtendedOnXAndY_NotZ()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero, 2, 2, 2, 2);
        var verts = f.GetVertices();

        // Physical half-extents: 0.2, 0.15, 0.009
        // Gap 2mm each side → +0.002 per side → +0.004 total → half: 0.202, 0.152, Z unchanged
        foreach (var v in verts)
        {
            Assert.IsTrue(Mathf.Abs(v.x) <= 0.202f + 1e-5f, $"v.x={v.x} exceeds 0.202");
            Assert.IsTrue(Mathf.Abs(v.y) <= 0.152f + 1e-5f, $"v.y={v.y} exceeds 0.152");
            Assert.IsTrue(Mathf.Abs(v.z) <= 0.009f + 1e-5f, $"v.z={v.z} exceeds 0.009");
        }
    }

    [Test]
    public void Facade_AsymmetricGap_EffectiveBounds()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero, 1, 3, 0, 5);
        var verts = f.GetVertices();

        // Gap left=1, right=3 → +4mm total X → half-ext X = 0.202
        // Gap top=0, bottom=5 → +5mm total Y → half-ext Y = 0.1525
        // Z unchanged
        float maxX = 0.202f;
        float maxY = 0.1525f;
        float maxZ = 0.009f;
        foreach (var v in verts)
        {
            Assert.IsTrue(Mathf.Abs(v.x) <= maxX + 1e-4f, $"v.x={v.x} exceeds {maxX}");
            Assert.IsTrue(Mathf.Abs(v.y) <= maxY + 1e-4f, $"v.y={v.y} exceeds {maxY}");
            Assert.IsTrue(Mathf.Abs(v.z) <= maxZ + 1e-4f, $"v.z={v.z} exceeds {maxZ}");
        }
    }

    [Test]
    public void Facade_OverlappingAnother_GapIncluded()
    {
        // Two facades with gap=2 each, physical 400x300x18 placed 3mm apart
        // Effective: 404x304x18. Physical gap=3mm, effective edges overlap by 1mm
        var a = MakeFacade("A", new Vector3Int(400, 300, 18), Vector3.zero, 2, 2, 2, 2);
        MakeFacade("B", new Vector3Int(400, 300, 18), new Vector3(0.003f, 0f, 0f), 2, 2, 2, 2);

        var all = BoardRegistry.GetAll();
        var vr = ConstraintValidator.Validate(all);
        Assert.IsTrue(vr.violations.Contains(a), "Effective bounds overlap → violation");
    }

    [Test]
    public void Facade_NoGap_TouchingEdges_NoViolation()
    {
        var a = MakeFacade("A", new Vector3Int(400, 300, 18), Vector3.zero, 0, 0, 0, 0);
        MakeFacade("B", new Vector3Int(400, 300, 18), new Vector3(0.4f, 0f, 0f), 0, 0, 0, 0);

        var all = BoardRegistry.GetAll();
        var vr = ConstraintValidator.Validate(all);
        Assert.IsFalse(vr.violations.Contains(a));
    }

    [Test]
    public void Facade_ElementFactory_CreatesWithGaps()
    {
        var go = ElementFactory.CreateFacade(
            new Vector3Int(600, 400, 18), "TestFacade", Vector3.zero, 1, 2, 3, 4);
        _spawned.Add(go);

        var facade = go.GetComponent<FacadeElement>();
        Assert.IsNotNull(facade);
        Assert.AreEqual("TestFacade", facade.BoardName);
        Assert.AreEqual(1, facade.GapLeft);
        Assert.AreEqual(2, facade.GapRight);
        Assert.AreEqual(3, facade.GapTop);
        Assert.AreEqual(4, facade.GapBottom);
        Assert.AreEqual(new Vector3Int(600, 400, 18), facade.DimensionsMM);
    }

    [Test]
    public void Facade_PhysicalScale_StaysAtDimensions()
    {
        var f = MakeFacade("F", new Vector3Int(500, 400, 18), Vector3.zero, 4, 4, 4, 4);
        Assert.AreEqual(0.5f, f.transform.localScale.x, 1e-5f);
        Assert.AreEqual(0.4f, f.transform.localScale.y, 1e-5f);
        Assert.AreEqual(0.018f, f.transform.localScale.z, 1e-5f);
    }

    [Test]
    public void Facade_Duplicate_PreservesGaps()
    {
        var src = MakeFacade("Src", new Vector3Int(500, 400, 18), Vector3.zero, 1, 2, 3, 4);
        var dupGo = ElementFactory.Duplicate(src);
        _spawned.Add(dupGo);

        var dup = dupGo.GetComponent<FacadeElement>();
        Assert.IsNotNull(dup);
        Assert.AreEqual(1, dup.GapLeft);
        Assert.AreEqual(2, dup.GapRight);
        Assert.AreEqual(3, dup.GapTop);
        Assert.AreEqual(4, dup.GapBottom);
    }
}
