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

    private DrawerElement MakeDrawer(string name, Vector3Int dims, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var d = go.AddComponent<DrawerElement>();
        d.PartName = name;
        d.DimensionsMM = dims;
        PartRegistry.Register(d);
        _spawned.Add(go);
        return d;
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

    // ── Radial shelf conversions ───────────────────────────────────────

    [Test]
    public void Part_To_RadialShelf_PreservesCommon_KeepsDimensions_DefaultCornerRadius()
    {
        var src = Make<KitchenElement>("Src", new Vector3Int(500, 18, 400), Vector3.zero);
        src.Movable = true;
        src.GroupId = 21;
        src.MaterialId = "oak";

        var result = (RadialShelfElement)ElementConverter.Convert(
            src, ElementConverter.TargetType.RadialShelf);
        Assert.IsNotNull(result);
        Assert.AreEqual("Src", result.PartName);
        Assert.AreEqual(true, result.Movable);
        Assert.AreEqual(21, result.GroupId);
        Assert.AreEqual("oak", result.MaterialId);
        Assert.AreEqual(200, result.CornerRadius);
        Assert.AreEqual(new Vector3Int(500, 18, 400), result.DimensionsMM);
    }

    [Test]
    public void RadialShelf_To_Part_PreservesCommon_DropsRadius()
    {
        var src = Make<RadialShelfElement>("Src", new Vector3Int(400, 25, 400), Vector3.zero);
        src.Movable = false;
        src.GroupId = 23;
        src.MaterialId = "cherry";

        var result = ElementConverter.Convert(src, ElementConverter.TargetType.Part);
        Assert.IsNotNull(result);
        Assert.IsNotInstanceOf<RadialShelfElement>(result);
        Assert.IsInstanceOf<KitchenElement>(result);
        AssertCommon(src, result);
        Assert.AreEqual(new Vector3Int(400, 25, 400), result.DimensionsMM);
    }

    // ── Facade → RadialShelf ──────────────────────────────────────────

    [Test]
    public void Facade_To_RadialShelf_PreservesCommon_DropsFacadeProperties()
    {
        var src = MakeFacade("Src", new Vector3Int(500, 350, 18), Vector3.zero,
            1, 2, 3, 4, DoorMode.HingeFrontRight, true);
        src.Movable = false;
        src.GroupId = 31;
        src.MaterialId = "wenge";

        var result = (RadialShelfElement)ElementConverter.Convert(
            src, ElementConverter.TargetType.RadialShelf);
        Assert.IsNotNull(result);
        Assert.IsInstanceOf<RadialShelfElement>(result);
        Assert.IsNotInstanceOf<FacadeElement>(result);
        Assert.AreEqual("Src", result.PartName);
        Assert.AreEqual(false, result.Movable);
        Assert.AreEqual(31, result.GroupId);
        Assert.AreEqual("wenge", result.MaterialId);
        Assert.AreEqual(18, result.CornerRadius, "default 200 clamped to min(width, depth)=18");
        Assert.AreEqual(new Vector3Int(500, 350, 18), result.DimensionsMM);
    }

    // ── Assembled → RadialShelf ────────────────────────────────────────

    [Test]
    public void Assembled_To_RadialShelf_PreservesCommon_DropsAssembledAndFacadeProperties()
    {
        var src = MakeAssembled("Src", new Vector3Int(700, 400, 18), Vector3.zero,
            1, 2, 3, 4, AssembledFill.Glass, 3);
        src.Movable = true;
        src.GroupId = 33;
        src.MaterialId = "oak";

        var result = (RadialShelfElement)ElementConverter.Convert(
            src, ElementConverter.TargetType.RadialShelf);
        Assert.IsNotNull(result);
        Assert.IsInstanceOf<RadialShelfElement>(result);
        Assert.IsNotInstanceOf<FacadeElement>(result);
        Assert.AreEqual("Src", result.PartName);
        Assert.AreEqual(true, result.Movable);
        Assert.AreEqual(33, result.GroupId);
        Assert.AreEqual("oak", result.MaterialId);
        Assert.AreEqual(18, result.CornerRadius, "default 200 clamped to min(width, depth)=18");
        Assert.AreEqual(new Vector3Int(700, 400, 18), result.DimensionsMM);
    }

    // ── RadialShelf → Facade ──────────────────────────────────────────

    [Test]
    public void RadialShelf_To_Facade_PreservesCommon_DropsRadius_HasDefaultGaps()
    {
        var src = Make<RadialShelfElement>("Src", new Vector3Int(500, 18, 500), Vector3.zero);
        src.Movable = false;
        src.GroupId = 35;
        src.MaterialId = "cherry";

        var result = (FacadeElement)ElementConverter.Convert(
            src, ElementConverter.TargetType.Facade);
        Assert.IsNotNull(result);
        Assert.IsInstanceOf<FacadeElement>(result);
        Assert.IsNotInstanceOf<AssembledFacadeElement>(result);
        Assert.IsNotInstanceOf<RadialShelfElement>(result);
        AssertCommon(src, result);
        Assert.AreEqual(2, result.GapLeft);
        Assert.AreEqual(2, result.GapRight);
        Assert.AreEqual(2, result.GapTop);
        Assert.AreEqual(2, result.GapBottom);
        Assert.AreEqual(DoorMode.HingeFrontLeft, result.Mode);
        Assert.IsFalse(result.IsOpen);
    }

    // ── RadialShelf → Assembled ───────────────────────────────────────

    [Test]
    public void RadialShelf_To_Assembled_PreservesCommon_DropsRadius_HasDefaultFill()
    {
        var src = Make<RadialShelfElement>("Src", new Vector3Int(600, 25, 600), Vector3.zero);
        src.Movable = true;
        src.GroupId = 37;
        src.MaterialId = "white";

        var result = (AssembledFacadeElement)ElementConverter.Convert(
            src, ElementConverter.TargetType.AssembledFacade);
        Assert.IsNotNull(result);
        Assert.IsInstanceOf<AssembledFacadeElement>(result);
        Assert.IsNotInstanceOf<RadialShelfElement>(result);
        AssertCommon(src, result);
        Assert.AreEqual(2, result.GapLeft);
        Assert.AreEqual(2, result.GapRight);
        Assert.AreEqual(2, result.GapTop);
        Assert.AreEqual(2, result.GapBottom);
        Assert.AreEqual(AssembledFill.Blind, result.Fill);
        Assert.AreEqual(AppConstants.ASSEMBLED_DEFAULT_GROOVES, result.GrooveCount);
    }

    // ── Drawer conversions (no-op) ────────────────────────────────────

    [Test]
    public void Drawer_ToAnyType_ReturnsSameInstance()
    {
        var src = MakeDrawer("Src", new Vector3Int(600, 400, 18), Vector3.zero);
        foreach (var target in new[] {
            ElementConverter.TargetType.Part,
            ElementConverter.TargetType.Facade,
            ElementConverter.TargetType.AssembledFacade,
            ElementConverter.TargetType.RadialShelf })
        {
            var result = ElementConverter.Convert(src, target);
            Assert.AreSame(src, result, $"Drawer → {target} should be no-op");
        }
    }

    [Test]
    public void AnyType_ToDrawer_ReturnsSameInstance()
    {
        var part = Make<KitchenElement>("Part", new Vector3Int(600, 400, 18), Vector3.zero);
        var result = ElementConverter.Convert(part, ElementConverter.TargetType.Drawer);
        Assert.AreSame(part, result, "Any → Drawer should be no-op");
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
        Assert.That(() => ElementConverter.Convert(null!, ElementConverter.TargetType.Part),
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

    // ── Пазы ──────────────────────────────────────────────────────────

    [Test]
    public void Convert_PartWithGrooves_ToFacade_DropsGrooves()
    {
        var src = Make<KitchenElement>("Grooved", new Vector3Int(600, 400, 18), Vector3.zero);
        src.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
        Assert.AreEqual(1, src.Grooves.Count);

        var result = ElementConverter.Convert(src, ElementConverter.TargetType.Facade);

        Assert.IsFalse(result.SupportsGrooves, "фасад пазов не поддерживает");
        Assert.AreEqual(0, result.Grooves.Count);
    }

    [Test]
    public void Convert_FacadeToPart_LeavesPartWithoutGrooves()
    {
        var src = Make<FacadeElement>("Facade", new Vector3Int(600, 400, 18), Vector3.zero);
        var result = ElementConverter.Convert(src, ElementConverter.TargetType.Part);

        Assert.IsTrue(result.SupportsGrooves);
        Assert.AreEqual(0, result.Grooves.Count);
    }

    // ── Мойки ─────────────────────────────────────────────────────────

    [Test]
    public void Convert_PartWithSink_ToFacade_DropsSinkAttachment()
    {
        var src = Make<KitchenElement>("Countertop", new Vector3Int(1200, 600, 38), Vector3.zero);
        var sinkGo = new GameObject("Sink");
        var sink = sinkGo.AddComponent<SinkElement>();
        sink.PartName = "Sink";
        src.RegisterCutout(sink);
        Assert.AreEqual(1, src.AttachedCutouts.Count);

        var result = ElementConverter.Convert(src, ElementConverter.TargetType.Facade);

        Assert.AreEqual(0, result.AttachedCutouts.Count, "у фасада проёма под мойку нет");
        Assert.AreEqual(2, result.CutoutHoleAxis, "без моек ось выреза каноническая");
        Object.DestroyImmediate(sinkGo);
    }

    // ── Кромкование ───────────────────────────────────────────────────

    [Test]
    public void Convert_FacadeToPart_KeepsEdgeSettings()
    {
        var src = Make<FacadeElement>("F1", new Vector3Int(600, 700, 18), Vector3.zero);
        src.EdgeThicknessMM = 2f;
        src.SetEdgeManual(EdgeSide.L1, true);

        var result = ElementConverter.Convert(src, ElementConverter.TargetType.Part);

        Assert.IsTrue(result.SupportsEdges, "деталь-лист кромкуется");
        Assert.IsTrue(result.EdgeBandingEnabled);
        Assert.AreEqual(2f, result.EdgeThicknessMM, 1e-4f);
        Assert.IsTrue(result.IsEdgeManual(EdgeSide.L1));
    }

    [Test]
    public void Convert_PartWithEdgesOff_ToFacadeAndBack_KeepsFlagOff()
    {
        var src = Make<KitchenElement>("B1", new Vector3Int(800, 400, 18), Vector3.zero);
        src.EdgeBandingEnabled = false;

        var facade = ElementConverter.Convert(src, ElementConverter.TargetType.Facade);
        Assert.IsFalse(facade.SupportsEdges, "фасад кромок не показывает");

        var back = ElementConverter.Convert(facade, ElementConverter.TargetType.Part);
        Assert.IsFalse(back.EdgeBandingEnabled, "снятая галочка переживает конвертацию туда-обратно");
    }

    // ── Mesh and collider cleanup after conversion ────────────────────

    private static void AssertMeshIsCube(GameObject go)
    {
        var mf = go.GetComponent<MeshFilter>();
        Assert.IsNotNull(mf, "MeshFilter is present");
        Assert.IsNotNull(mf!.sharedMesh, "sharedMesh is assigned");
        var mesh = mf.sharedMesh;
        Assert.That(mesh.name == "Cube" || mesh.name == "PartWithGrooves",
            "sharedMesh is a cube (built-in 'Cube' or GrooveMesh 'PartWithGrooves' without grooves)");
        Assert.AreEqual(24, mesh.vertexCount, "cube has 24 vertices");
    }

    private static void AssertBoxColliderOnly(GameObject go)
    {
        Assert.IsNull(go.GetComponent<MeshCollider>(), "no MeshCollider");
        Assert.IsNotNull(go.GetComponent<BoxCollider>(), "has BoxCollider");
    }

    [Test]
    public void RadialShelf_To_Part_HasCubeMeshAndBoxCollider()
    {
        var src = Make<RadialShelfElement>("RS", new Vector3Int(600, 18, 400), Vector3.zero);
        var go = src.gameObject;

        var converted = ElementConverter.Convert(src, ElementConverter.TargetType.Part);

        Assert.IsNotNull(converted as KitchenElement);
        Assert.IsNull(converted as RadialShelfElement);
        AssertMeshIsCube(go);
        AssertBoxColliderOnly(go);
    }

    [Test]
    public void RadialShelf_To_Facade_HasCubeMeshAndBoxCollider()
    {
        var src = Make<RadialShelfElement>("RS", new Vector3Int(500, 18, 500), Vector3.zero);
        var go = src.gameObject;

        var converted = ElementConverter.Convert(src, ElementConverter.TargetType.Facade);

        Assert.IsNotNull(converted as FacadeElement);
        AssertMeshIsCube(go);
        AssertBoxColliderOnly(go);
    }

    [Test]
    public void Assembled_To_Part_HasCubeMeshAndBoxCollider()
    {
        var src = MakeAssembled("AF", new Vector3Int(700, 400, 18), Vector3.zero,
            fill: AssembledFill.Blind);
        var go = src.gameObject;

        var converted = ElementConverter.Convert(src, ElementConverter.TargetType.Part);

        Assert.IsNotNull(converted as KitchenElement);
        Assert.IsNull(converted as AssembledFacadeElement);
        AssertMeshIsCube(go);
        AssertBoxColliderOnly(go);
    }

    [Test]
    public void Assembled_To_Facade_HasCubeMeshAndBoxCollider()
    {
        var src = MakeAssembled("AF", new Vector3Int(600, 350, 18), Vector3.zero,
            fill: AssembledFill.Glass);
        var go = src.gameObject;

        var converted = ElementConverter.Convert(src, ElementConverter.TargetType.Facade);

        Assert.IsNotNull(converted as FacadeElement);
        Assert.IsNull(converted as AssembledFacadeElement);
        AssertMeshIsCube(go);
        AssertBoxColliderOnly(go);
    }

    [Test]
    public void RadialShelfElement_OnDestroy_CleansUpOwnedMesh()
    {
        var go = new GameObject("TestShelf", typeof(MeshFilter), typeof(MeshRenderer));
        var shelf = go.AddComponent<RadialShelfElement>();
        shelf.PartName = "TestShelf";
        shelf.DimensionsMM = new Vector3Int(600, 18, 400);
        shelf.CornerRadius = 200;

        var mf = go.GetComponent<MeshFilter>();
        Assert.IsNotNull(mf.sharedMesh, "mesh built");
        Assert.AreNotEqual("Cube", mf.sharedMesh.name, "radial mesh is not a cube");

        Object.DestroyImmediate(shelf);

        Assert.That(mf.sharedMesh == null || mf.sharedMesh.name == "",
            "mesh is destroyed or invalid — radial mesh was cleaned up");
        Object.DestroyImmediate(go);
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
        "SupportsGrooves", "Grooves", "AttachedCutouts", "CutoutHoleAxis",
        "SupportsEdges", "EdgeBandingEnabled", "EdgeThicknessMM", "EdgeManualMask",
        // Накладки текстур принадлежат стене и полу, а конвертация ходит только
        // между структурными типами (деталь ↔ фасад ↔ полка) — переносить их
        // некуда и не из чего, см. TextureOverlayTests.
        "SupportsTextureOverlays", "TextureOverlays",
        // Производные от состояния, а не хранимые: конвертация их не переносит —
        // после смены типа они пересчитаются сами.
        "PoseFollowsTransform", "Transformable",
        // Счётчик изменений позы: рантайм-состояние для SceneChangeTracker,
        // в сейве его нет и переносить при конвертации нечего.
        "PoseVersion",
        // FacadeElement
        "GapLeft", "GapRight", "GapTop", "GapBottom", "GapMM", "Mode",
        "IsOpen", "DoorProgress", "IsDoorClosed", "ClosedPosition", "ClosedRotation",
        // AssembledFacadeElement
        "Fill", "GrooveCount",
        // RadialShelfElement
        "CornerRadius"
    };

    [Test]
    public void Reflection_AllPublicProperties_AreCovered()
    {
        var declared = new HashSet<string>();

        foreach (var type in new[] { typeof(KitchenElement), typeof(FacadeElement), typeof(AssembledFacadeElement), typeof(RadialShelfElement) })
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
