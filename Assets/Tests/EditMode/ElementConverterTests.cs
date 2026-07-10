using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class ElementConverterTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
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
    }

    private T Make<T>(string name, Vector3Int dims, Vector3 pos) where T : KitchenElement
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var e = go.AddComponent<T>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    private FacadeElement MakeFacade(string name, Vector3Int dims, Vector3 pos,
        int gapL = 2, int gapR = 2, int gapT = 2, int gapB = 2,
        DoorMode mode = DoorMode.HingeFrontLeft, bool open = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var f = go.AddComponent<FacadeElement>();
        f.PartName = name;
        f.DimensionsMM = dims;
        f.GapLeft = gapL;
        f.GapRight = gapR;
        f.GapTop = gapT;
        f.GapBottom = gapB;
        f.Mode = mode;
        if (open) f.SetOpen(true);
        PartRegistry.Register(f);
        _spawned.Add(go);
        return f;
    }

    private AssembledFacadeElement MakeAssembled(string name, Vector3Int dims, Vector3 pos,
        int gapL = 2, int gapR = 2, int gapT = 2, int gapB = 2,
        AssembledFill fill = AssembledFill.Blind, int grooves = 1)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var a = go.AddComponent<AssembledFacadeElement>();
        a.PartName = name;
        a.DimensionsMM = dims;
        a.GapLeft = gapL;
        a.GapRight = gapR;
        a.GapTop = gapT;
        a.GapBottom = gapB;
        a.Fill = fill;
        a.GrooveCount = grooves;
        PartRegistry.Register(a);
        _spawned.Add(go);
        return a;
    }

    private static void AssertCommon(KitchenElement src, KitchenElement dst)
    {
        Assert.AreEqual(src.PartName, dst.PartName);
        Assert.AreEqual(src.DimensionsMM, dst.DimensionsMM);
        Assert.AreEqual(src.Movable, dst.Movable);
        Assert.AreEqual(src.GroupId, dst.GroupId);
        Assert.AreEqual(src.MaterialId, dst.MaterialId);
        Assert.AreEqual(src.Transparent, dst.Transparent);
    }

    // ── Part → Facade ──────────────────────────────────────────────────

    [Test]
    public void Part_To_Facade_PreservesCommon()
    {
        var src = Make<KitchenElement>("Src", new Vector3Int(600, 400, 18), Vector3.zero);
        src.Movable = false;
        src.GroupId = 5;
        src.MaterialId = "oak";
        src.Transparent = false;

        var result = ElementConverter.Convert(src, ElementConverter.TargetType.Facade);
        Assert.IsNotNull(result);
        Assert.IsInstanceOf<FacadeElement>(result);
        AssertCommon(src, result);
    }

    [Test]
    public void Part_To_Facade_HasDefaultGaps()
    {
        var src = Make<KitchenElement>("Src", new Vector3Int(600, 400, 18), Vector3.zero);
        var result = (FacadeElement)ElementConverter.Convert(src, ElementConverter.TargetType.Facade);
        Assert.AreEqual(2, result.GapLeft);
        Assert.AreEqual(2, result.GapRight);
        Assert.AreEqual(2, result.GapTop);
        Assert.AreEqual(2, result.GapBottom);
        Assert.AreEqual(8, result.GapMM);
        Assert.AreEqual(DoorMode.HingeFrontLeft, result.Mode);
        Assert.IsFalse(result.IsOpen);
    }

    // ── Part → Assembled ───────────────────────────────────────────────

    [Test]
    public void Part_To_Assembled_PreservesCommon()
    {
        var src = Make<KitchenElement>("Src", new Vector3Int(800, 500, 18), Vector3.zero);
        src.Movable = true;
        src.GroupId = 3;
        src.MaterialId = "white";
        src.Transparent = false;

        var result = ElementConverter.Convert(src, ElementConverter.TargetType.AssembledFacade);
        Assert.IsNotNull(result);
        Assert.IsInstanceOf<AssembledFacadeElement>(result);
        AssertCommon(src, result);
    }

    [Test]
    public void Part_To_Assembled_HasDefaults()
    {
        var src = Make<KitchenElement>("Src", new Vector3Int(800, 500, 18), Vector3.zero);
        var result = (AssembledFacadeElement)ElementConverter.Convert(src, ElementConverter.TargetType.AssembledFacade);
        Assert.AreEqual(2, result.GapLeft);
        Assert.AreEqual(2, result.GapRight);
        Assert.AreEqual(2, result.GapTop);
        Assert.AreEqual(2, result.GapBottom);
        Assert.AreEqual(AssembledFill.Blind, result.Fill);
        Assert.AreEqual(AppConstants.ASSEMBLED_DEFAULT_GROOVES, result.GrooveCount);
    }

    // ── Facade → Part ──────────────────────────────────────────────────

    [Test]
    public void Facade_To_Part_PreservesCommon_DropsFacadeProperties()
    {
        var src = MakeFacade("Src", new Vector3Int(400, 300, 18), Vector3.zero,
            1, 3, 0, 5, DoorMode.HingeFrontRight, true);
        src.Movable = true;
        src.GroupId = 7;
        src.MaterialId = "cherry";
        src.Transparent = false;

        var result = ElementConverter.Convert(src, ElementConverter.TargetType.Part);
        Assert.IsNotNull(result);
        Assert.IsNotInstanceOf<FacadeElement>(result);
        Assert.IsInstanceOf<KitchenElement>(result);
        AssertCommon(src, result);
    }

    // ── Facade → Assembled ─────────────────────────────────────────────

    [Test]
    public void Facade_To_Assembled_PreservesAll()
    {
        var src = MakeFacade("Src", new Vector3Int(500, 350, 18), Vector3.zero,
            1, 2, 3, 4, DoorMode.DrawerOut, true);
        src.Movable = false;
        src.GroupId = 9;
        src.MaterialId = "wenge";
        src.Transparent = false;

        var result = (AssembledFacadeElement)ElementConverter.Convert(
            src, ElementConverter.TargetType.AssembledFacade);
        Assert.IsNotNull(result);
        AssertCommon(src, result);
        Assert.AreEqual(1, result.GapLeft);
        Assert.AreEqual(2, result.GapRight);
        Assert.AreEqual(3, result.GapTop);
        Assert.AreEqual(4, result.GapBottom);
        Assert.AreEqual(DoorMode.DrawerOut, result.Mode);
        Assert.IsTrue(result.IsOpen);
        Assert.AreEqual(AssembledFill.Blind, result.Fill);
        Assert.AreEqual(AppConstants.ASSEMBLED_DEFAULT_GROOVES, result.GrooveCount);
    }

    // ── Assembled → Part ───────────────────────────────────────────────

    [Test]
    public void Assembled_To_Part_PreservesCommon_DropsAssembledProperties()
    {
        var src = MakeAssembled("Src", new Vector3Int(900, 600, 18), Vector3.zero,
            2, 2, 2, 2, AssembledFill.Glass, 3);
        src.Movable = true;
        src.GroupId = 11;
        src.MaterialId = "glass_grey";
        src.Transparent = true;

        var result = ElementConverter.Convert(src, ElementConverter.TargetType.Part);
        Assert.IsNotNull(result);
        Assert.IsNotInstanceOf<FacadeElement>(result);
        Assert.IsInstanceOf<KitchenElement>(result);
        AssertCommon(src, result);
    }

    // ── Assembled → Facade ─────────────────────────────────────────────

    [Test]
    public void Assembled_To_Facade_PreservesCommon_DropsAssembledProperties()
    {
        var src = MakeAssembled("Src", new Vector3Int(700, 400, 18), Vector3.zero,
            0, 0, 0, 0, AssembledFill.Open, 2);
        src.Movable = false;
        src.GroupId = 13;
        src.MaterialId = "black";
        src.Transparent = false;

        var result = (FacadeElement)ElementConverter.Convert(
            src, ElementConverter.TargetType.Facade);
        Assert.IsNotNull(result);
        Assert.IsInstanceOf<FacadeElement>(result);
        Assert.IsNotInstanceOf<AssembledFacadeElement>(result);
        AssertCommon(src, result);
        Assert.AreEqual(0, result.GapLeft);
        Assert.AreEqual(0, result.GapRight);
        Assert.AreEqual(0, result.GapTop);
        Assert.AreEqual(0, result.GapBottom);
    }

    // ── Idempotency ────────────────────────────────────────────────────

    [Test]
    public void Convert_ToSameType_ReturnsSameInstance()
    {
        var src = Make<KitchenElement>("Src", new Vector3Int(600, 400, 18), Vector3.zero);
        var result = ElementConverter.Convert(src, ElementConverter.TargetType.Part);
        Assert.AreSame(src, result);
    }

    [Test]
    public void Convert_NullSource_Throws()
    {
        Assert.That(() => ElementConverter.Convert(null, ElementConverter.TargetType.Part),
            Throws.ArgumentNullException);
    }

    // ── GameObject remains alive after conversion ─────────────────────

    [Test]
    public void Convert_GameObjectNotDestroyed()
    {
        var src = Make<KitchenElement>("Src", new Vector3Int(600, 400, 18), Vector3.zero);
        var go = src.gameObject;
        ElementConverter.Convert(src, ElementConverter.TargetType.Facade);
        Assert.IsNotNull(go);
        Assert.AreEqual("Src", go.name);
    }

    // ── Reflection: every public property is covered ──────────────────

    private static readonly HashSet<string> CommonProperties = new HashSet<string>
    {
        "PartName", "DimensionsMM", "Movable", "GroupId", "MaterialId", "Transparent", "Data"
    };

    private static readonly HashSet<string> FacadeProperties = new HashSet<string>
    {
        "GapLeft", "GapRight", "GapTop", "GapBottom", "GapMM", "Mode",
        "IsOpen", "DoorProgress", "IsDoorClosed", "ClosedPosition", "ClosedRotation"
    };

    private static readonly HashSet<string> AssembledProperties = new HashSet<string>
    {
        "Fill", "GrooveCount"
    };

    private static readonly HashSet<string> AllCovered = new HashSet<string>
    {
        // KitchenElement
        "PartName", "DimensionsMM", "Movable", "GroupId", "MaterialId", "Transparent", "Data",
        // FacadeElement
        "GapLeft", "GapRight", "GapTop", "GapBottom", "GapMM", "Mode",
        "IsOpen", "DoorProgress", "IsDoorClosed", "ClosedPosition", "ClosedRotation",
        // AssembledFacadeElement
        "Fill", "GrooveCount"
    };

    [Test]
    public void Reflection_AllPublicProperties_AreCovered()
    {
        var declared = new HashSet<string>();

        foreach (var type in new[] { typeof(KitchenElement), typeof(FacadeElement), typeof(AssembledFacadeElement) })
        {
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var p in props)
                declared.Add(p.Name);
        }

        var missing = declared.Except(AllCovered).ToList();
        Assert.IsEmpty(missing,
            "Properties not covered by converter tests: " + string.Join(", ", missing));

        var unknown = AllCovered.Except(declared).ToList();
        Assert.IsEmpty(unknown,
            "Covered list has names not found as declared public properties: " + string.Join(", ", unknown));
    }
}
