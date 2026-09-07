using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Полный round-trip всех типов элементов через capture → serialize → file → deserialize → restore.
/// Проверяется каждое свойство каждого типа элемента.
/// </summary>
public class RoundTripTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
        {
            if (go != null) Object.DestroyImmediate(go);
        }
        _spawned.Clear();

        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);

        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private static string TempPath() =>
        Path.Combine(Application.temporaryCachePath, $"rt_{System.Guid.NewGuid():N}.json");

    private KitchenElement GetElement(GameObject go)
    {
        var el = go.GetComponent<KitchenElement>();
        _spawned.Add(go);
        PartRegistry.Register(el);
        return el;
    }

    private KitchenElement MakeBoard(string name, Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreatePart(dims, name, pos);
        return GetElement(go);
    }

    private FacadeElement MakeFacade(string name, Vector3Int dims, Vector3 pos,
        int gapLeft = 2, int gapRight = 2, int gapTop = 2, int gapBottom = 2)
    {
        var go = ElementFactory.CreateFacade(dims, name, pos, gapLeft, gapRight, gapTop, gapBottom);
        _spawned.Add(go);
        PartRegistry.Register(go.GetComponent<KitchenElement>());
        return go.GetComponent<FacadeElement>();
    }

    private AssembledFacadeElement MakeAssembled(string name, Vector3Int dims, Vector3 pos,
        AssembledFill fill = AssembledFill.Blind)
    {
        var go = ElementFactory.CreateAssembledFacade(dims, name, pos, fill);
        _spawned.Add(go);
        PartRegistry.Register(go.GetComponent<KitchenElement>());
        return go.GetComponent<AssembledFacadeElement>();
    }

    private RadialShelfElement MakeRadial(string name, int width, int depth, int thickness,
        int cornerRadius, Vector3 pos)
    {
        var go = ElementFactory.CreateRadialShelf(width, depth, thickness, cornerRadius, name, pos);
        _spawned.Add(go);
        PartRegistry.Register(go.GetComponent<KitchenElement>());
        return go.GetComponent<RadialShelfElement>();
    }

    private KitchenElement MakeWall(string name, Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreateWall(dims, name, pos);
        return GetElement(go);
    }

    /// <summary>Полный цикл: capture всех _spawned → файл → очистка сцены → restore.</summary>
    private ProjectData FullRoundTrip()
    {
        var path = TempPath();
        var elements = new List<KitchenElement>();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) elements.Add(el);
        }

        var original = SaveLoadManager.CaptureScene(elements);
        SaveLoadManager.SaveToFile(path, original);

        // очистка сцены
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();

        var loaded = SaveLoadManager.LoadFromFile(path);
        Assert.IsNotNull(loaded, "LoadFromFile should not return null");
        SaveLoadManager.RestoreScene(loaded!);

        File.Delete(path);
        return loaded!;
    }

    /// <summary>Capture → Serialize → Deserialize (без файла).</summary>
    private ProjectData MemoryRoundTrip()
    {
        var elements = new List<KitchenElement>();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) elements.Add(el);
        }
        var data = SaveLoadManager.CaptureScene(elements);
        var json = SaveLoadManager.Serialize(data);
        return SaveLoadManager.Deserialize(json)!;
    }

    // ── 1. KitchenElement (Board) ────────────────────────────────────────

    [Test]
    public void Board_AllProperties_RoundTrip()
    {
        var board = MakeBoard("TestBoard", new Vector3Int(800, 400, 18), new Vector3(1.5f, 2.5f, 3.5f));
        board.transform.rotation = Quaternion.Euler(0, 45, 0);
        board.Movable = false;
        board.Transparent = true;
        board.MaterialId = "white";

        FullRoundTrip();

        var restored = Object.FindObjectsByType<KitchenElement>();
        Assert.AreEqual(1, restored.Length, "should restore exactly one element");

        var el = restored[0];
        Assert.AreEqual("TestBoard", el.PartName, "name");
        Assert.AreEqual(new Vector3Int(800, 400, 18), el.DimensionsMM, "dimensions");
        Assert.AreEqual(1.5f, el.transform.position.x, 0.001f, "pos.x");
        Assert.AreEqual(2.5f, el.transform.position.y, 0.001f, "pos.y");
        Assert.AreEqual(3.5f, el.transform.position.z, 0.001f, "pos.z");
        Assert.AreEqual(45f, el.transform.rotation.eulerAngles.y, 0.1f, "rotation.y");
        Assert.IsFalse(el.Movable, "movable");
        Assert.IsTrue(el.Transparent, "transparent");
        Assert.AreEqual("white", el.MaterialId, "materialId");
    }

    [Test]
    public void Board_MaterialDefault_RoundTrip()
    {
        MakeBoard("Board", new Vector3Int(600, 400, 18), Vector3.zero);
        FullRoundTrip();
        var el = Object.FindObjectsByType<KitchenElement>()[0];
        Assert.AreEqual(MaterialCatalog.DefaultId, el.MaterialId, "default material should be 'default'");
    }

    [Test]
    public void Board_Rotation_90_180_270_Preserved()
    {
        var rotations = new[] { Quaternion.identity, Quaternion.Euler(0, 90, 0),
            Quaternion.Euler(0, 180, 0), Quaternion.Euler(0, 270, 0),
            Quaternion.Euler(45, 30, 15) };
        var elements = new List<KitchenElement>();

        for (int i = 0; i < rotations.Length; i++)
        {
            var go = ElementFactory.CreatePart(new Vector3Int(400, 400, 18), $"Rot{i}", Vector3.zero);
            go.transform.rotation = rotations[i];
            _spawned.Add(go);
            PartRegistry.Register(go.GetComponent<KitchenElement>());
            elements.Add(go.GetComponent<KitchenElement>());
        }

        FullRoundTrip();

        var restored = Object.FindObjectsByType<KitchenElement>();
        Assert.AreEqual(rotations.Length, restored.Length);

        System.Array.Sort(restored, (a, b) => string.CompareOrdinal(a.PartName, b.PartName));

        for (int i = 0; i < rotations.Length; i++)
        {
            Assert.AreEqual(rotations[i].x, restored[i].transform.rotation.x, 0.001f, $"rot[{i}] {restored[i].PartName}.x");
            Assert.AreEqual(rotations[i].y, restored[i].transform.rotation.y, 0.001f, $"rot[{i}] {restored[i].PartName}.y");
            Assert.AreEqual(rotations[i].z, restored[i].transform.rotation.z, 0.001f, $"rot[{i}] {restored[i].PartName}.z");
            Assert.AreEqual(rotations[i].w, restored[i].transform.rotation.w, 0.001f, $"rot[{i}] {restored[i].PartName}.w");
        }
    }

    // ── 2. FacadeElement ─────────────────────────────────────────────────

    [Test]
    public void Facade_AllProperties_RoundTrip()
    {
        var facade = MakeFacade("MyFacade", new Vector3Int(600, 716, 18), new Vector3(0.5f, 0.4f, -2.0f),
            gapLeft: 3, gapRight: 5, gapTop: 2, gapBottom: 2);
        facade.transform.rotation = Quaternion.Euler(0, 90, 0);
        facade.Mode = DoorMode.HingeFrontRight;
        facade.Movable = false;
        facade.Transparent = true;
        facade.MaterialId = "wenge";

        // не открываем — проверяем ClosedPosition / ClosedRotation
        var closedPos = facade.ClosedPosition;
        var closedRot = facade.ClosedRotation;

        FullRoundTrip();

        var restored = Object.FindObjectsByType<KitchenElement>();
        Assert.AreEqual(1, restored.Length);

        var r = restored[0] as FacadeElement;
        Assert.IsNotNull(r, "restored should be FacadeElement");
        Assert.AreEqual("MyFacade", r!.PartName);
        Assert.AreEqual(new Vector3Int(600, 716, 18), r!.DimensionsMM);
        Assert.AreEqual(3, r!.GapLeft);
        Assert.AreEqual(5, r!.GapRight);
        Assert.AreEqual(2, r!.GapTop);
        Assert.AreEqual(2, r!.GapBottom);
        Assert.AreEqual(DoorMode.HingeFrontRight, r!.Mode, "doorMode");
        Assert.IsFalse(r!.IsOpen, "doorOpen should be false");
        Assert.IsFalse(r!.Movable);
        Assert.IsTrue(r!.Transparent);
        Assert.AreEqual("wenge", r!.MaterialId);

        // позиция должна совпадать с ClosedPosition
        Assert.AreEqual(closedPos.x, r!.transform.position.x, 0.001f, "closed pos.x");
        Assert.AreEqual(closedPos.y, r!.transform.position.y, 0.001f, "closed pos.y");
        Assert.AreEqual(closedPos.z, r!.transform.position.z, 0.001f, "closed pos.z");
        Assert.AreEqual(closedRot.x, r!.transform.rotation.x, 0.001f, "closed rot.x");
        Assert.AreEqual(closedRot.y, r!.transform.rotation.y, 0.001f, "closed rot.y");
        Assert.AreEqual(closedRot.z, r!.transform.rotation.z, 0.001f, "closed rot.z");
        Assert.AreEqual(closedRot.w, r!.transform.rotation.w, 0.001f, "closed rot.w");
    }

    [Test]
    public void Facade_OpenDoor_RoundTrip_PreservesClosedPose()
    {
        var facade = MakeFacade("OpenDoor", new Vector3Int(450, 700, 18), new Vector3(0.5f, 0.35f, -1.0f),
            gapLeft: 2, gapRight: 2, gapTop: 2, gapBottom: 2);

        var closedPos = facade.transform.position;
        var closedRot = facade.transform.rotation;

        // открываем дверцу
        facade.SetOpen(true);
        facade.StepDoor(0.2f);  // частично открыта (прогресс ~0.5)
        facade.StepDoor(0.3f);  // полностью открыта (прогресс 1.0)

        Assert.IsTrue(facade.IsOpen, "should be open before save");
        Assert.AreNotEqual(closedPos, facade.transform.position, "opened position should differ from closed");

        FullRoundTrip();

        var r = Object.FindObjectsByType<KitchenElement>()[0] as FacadeElement;
        Assert.IsNotNull(r);
        Assert.IsTrue(r!.IsOpen, "doorOpen flag should survive");
        // После загрузки дверца должна быть в закрытой позе (ClosedPosition)
        Assert.AreEqual(closedPos.x, r!.ClosedPosition.x, 0.001f, "closed pos.x survives open door");
        Assert.AreEqual(closedPos.y, r!.ClosedPosition.y, 0.001f, "closed pos.y survives open door");
        Assert.AreEqual(closedPos.z, r!.ClosedPosition.z, 0.001f, "closed pos.z survives open door");
    }

    [Test]
    public void Facade_DoorMode_MemoryRoundTrip()
    {
        var allModes = new[]
        {
            DoorMode.HingeFrontLeft, DoorMode.HingeFrontRight, DoorMode.HingeFrontTop, DoorMode.HingeFrontBottom,
            DoorMode.HingeBackLeft, DoorMode.HingeBackRight, DoorMode.HingeBackTop, DoorMode.HingeBackBottom,
            DoorMode.HingeEdgeTopLeft, DoorMode.HingeEdgeTopRight, DoorMode.HingeEdgeBottomLeft, DoorMode.HingeEdgeBottomRight,
            DoorMode.DrawerOut, DoorMode.DrawerIn, DoorMode.DrawerRight, DoorMode.DrawerLeft, DoorMode.DrawerUp, DoorMode.DrawerDown,
        };

        foreach (var mode in allModes)
        {
            var facade = MakeFacade("ModeTest", new Vector3Int(450, 700, 18), Vector3.zero);
            facade.Mode = mode;
            var restored = MemoryRoundTrip();
            Assert.AreEqual((int)mode, restored.elements[0].doorMode, $"doorMode enum for {mode} should survive memory round-trip");
            Object.DestroyImmediate(facade.gameObject);
            _spawned.Clear();
            PartRegistry.Clear();
        }
    }

    [Test]
    public void Facade_GapsOnly_RoundTrip()
    {
        var facade = MakeFacade("GapsOnly", new Vector3Int(400, 400, 18), Vector3.zero,
            gapLeft: 10, gapRight: 20, gapTop: 0, gapBottom: 0);
        FullRoundTrip();
        var r = Object.FindObjectsByType<KitchenElement>()[0] as FacadeElement;
        Assert.IsNotNull(r);
        Assert.AreEqual(10, r!.GapLeft);
        Assert.AreEqual(20, r!.GapRight);
        Assert.AreEqual(0, r!.GapTop);
        Assert.AreEqual(0, r!.GapBottom);
    }

    // ── 3. AssembledFacadeElement ────────────────────────────────────────

    [Test]
    public void AssembledFacade_Blind_RoundTrip()
    {
        var assembled = MakeAssembled("Blind", new Vector3Int(600, 700, 18), new Vector3(0.3f, 0.35f, -2.0f),
            AssembledFill.Blind);
        assembled.GrooveCount = 3;
        assembled.Mode = DoorMode.DrawerOut;

        FullRoundTrip();

        var r = Object.FindObjectsByType<KitchenElement>()[0] as AssembledFacadeElement;
        Assert.IsNotNull(r, "restored should be AssembledFacadeElement");
        Assert.AreEqual("Blind", r!.PartName);
        Assert.AreEqual(new Vector3Int(600, 700, 18), r!.DimensionsMM);
        Assert.AreEqual(AssembledFill.Blind, r!.Fill, "fill");
        Assert.AreEqual(3, r!.GrooveCount, "grooveCount");
        Assert.AreEqual(DoorMode.DrawerOut, r!.Mode, "doorMode");
    }

    [Test]
    public void AssembledFacade_Gaps_RoundTrip()
    {
        // Регрессия: зазоры сборного фасада сохранялись в JSON, но при загрузке
        // терялись (CreateAssembledFacade не принимает зазоры, а restore-блок
        // их не проставлял) — после загрузки было 0/0/0/0.
        var assembled = MakeAssembled("Gaps", new Vector3Int(600, 700, 18), new Vector3(0.3f, 0.35f, -2.0f),
            AssembledFill.Blind);
        assembled.GapLeft = 3;
        assembled.GapRight = 5;
        assembled.GapTop = 1;
        assembled.GapBottom = 4;

        FullRoundTrip();

        var r = Object.FindObjectsByType<KitchenElement>()[0] as AssembledFacadeElement;
        Assert.IsNotNull(r, "restored should be AssembledFacadeElement");
        Assert.AreEqual(3, r!.GapLeft, "gapLeft");
        Assert.AreEqual(5, r!.GapRight, "gapRight");
        Assert.AreEqual(1, r!.GapTop, "gapTop");
        Assert.AreEqual(4, r!.GapBottom, "gapBottom");
    }

    [Test]
    public void AssembledFacade_Glass_RoundTrip()
    {
        var assembled = MakeAssembled("Glass", new Vector3Int(450, 600, 18), new Vector3(0.1f, 0.3f, -1.5f),
            AssembledFill.Glass);
        assembled.GrooveCount = 0;
        assembled.Mode = DoorMode.HingeFrontLeft;
        assembled.MaterialId = "oak";

        FullRoundTrip();

        var r = Object.FindObjectsByType<KitchenElement>()[0] as AssembledFacadeElement;
        Assert.IsNotNull(r);
        Assert.AreEqual(AssembledFill.Glass, r!.Fill, "glass fill");
        Assert.AreEqual(0, r!.GrooveCount, "zero grooves");
        Assert.AreEqual("oak", r!.MaterialId);
        // Glass insert child должен существовать
        var glassChild = r.transform.Find("__Glass");
        Assert.IsNotNull(glassChild, "glass insert should be created for Glass fill");
    }

    [Test]
    public void AssembledFacade_Open_RoundTrip()
    {
        var assembled = MakeAssembled("Open", new Vector3Int(500, 800, 18), new Vector3(0.2f, 0.4f, -1.0f),
            AssembledFill.Open);
        assembled.GrooveCount = 2;

        FullRoundTrip();

        var r = Object.FindObjectsByType<KitchenElement>()[0] as AssembledFacadeElement;
        Assert.IsNotNull(r);
        Assert.AreEqual(AssembledFill.Open, r!.Fill);
        Assert.AreEqual(2, r!.GrooveCount);
        // Open fill — No glass insert
        var glassChild = r!.transform.Find("__Glass");
        Assert.IsTrue(glassChild == null || !glassChild.gameObject.activeSelf,
            "glass insert should not be visible for Open fill");
    }

    // ── 4. RadialShelfElement ────────────────────────────────────────────

    [Test]
    public void RadialShelf_AllProperties_RoundTrip()
    {
        var shelf = MakeRadial("RadialShelf", width: 600, depth: 400, thickness: 18,
            cornerRadius: 200, new Vector3(0.5f, 0.01f, -1.0f));
        shelf.transform.rotation = Quaternion.Euler(0, 90, 0);
        shelf.Movable = false;
        shelf.MaterialId = "oak";

        FullRoundTrip();

        var r = Object.FindObjectsByType<KitchenElement>()[0] as RadialShelfElement;
        Assert.IsNotNull(r, "should be RadialShelfElement");
        Assert.AreEqual("RadialShelf", r!.PartName, "name");
        Assert.AreEqual(200, r!.CornerRadius, "cornerRadius");
        Assert.AreEqual(18, r!.DimensionsMM.y, "thickness");
        Assert.AreEqual(600, r!.DimensionsMM.x, "width");
        Assert.AreEqual(400, r!.DimensionsMM.z, "depth");
        Assert.AreEqual(0.5f, r!.transform.position.x, 0.001f, "pos.x");
        Assert.AreEqual(0.01f, r!.transform.position.y, 0.001f, "pos.y");
        Assert.AreEqual(-1.0f, r!.transform.position.z, 0.001f, "pos.z");
        Assert.AreEqual(90f, r!.transform.rotation.eulerAngles.y, 0.1f, "rot.y");
        Assert.IsFalse(r!.Movable, "movable");
        Assert.AreEqual("oak", r!.MaterialId, "materialId");
    }

    [Test]
    public void RadialShelf_DefaultCornerRadius_RoundTrip()
    {
        var shelf = MakeRadial("DefaultRadius", width: 600, depth: 400, thickness: 18,
            cornerRadius: 200, Vector3.zero);
        FullRoundTrip();
        var r = Object.FindObjectsByType<KitchenElement>()[0] as RadialShelfElement;
        Assert.IsNotNull(r);
        Assert.AreEqual(200, r!.CornerRadius);
    }

    [Test]
    public void RadialShelf_CustomCornerRadius_RoundTrip()
    {
        var shelf = MakeRadial("BigRadius", width: 500, depth: 500, thickness: 18,
            cornerRadius: 500, Vector3.zero);
        FullRoundTrip();
        var r = Object.FindObjectsByType<KitchenElement>()[0] as RadialShelfElement;
        Assert.IsNotNull(r);
        Assert.AreEqual(500, r!.CornerRadius);
        Assert.AreEqual(500, r!.DimensionsMM.x);
        Assert.AreEqual(500, r!.DimensionsMM.z);
    }

    // ── 5. Wall ──────────────────────────────────────────────────────────

    [Test]
    public void Wall_AllProperties_RoundTrip()
    {
        var wall = MakeWall("Wall", new Vector3Int(100, 2700, 3000), new Vector3(-1.6f, 1.35f, 0.0f));
        wall.transform.rotation = Quaternion.Euler(0, 0, 0);
        wall.Movable = true;
        wall.MaterialId = "concrete";

        FullRoundTrip();

        var restored = Object.FindObjectsByType<KitchenElement>();
        Assert.AreEqual(1, restored.Length);

        var el = restored[0];
        Assert.AreEqual("Wall", el.PartName);
        Assert.AreEqual(new Vector3Int(100, 2700, 3000), el.DimensionsMM);
        Assert.AreEqual(-1.6f, el.transform.position.x, 0.001f);
        Assert.AreEqual(1.35f, el.transform.position.y, 0.001f);
        Assert.AreEqual(0.0f, el.transform.position.z, 0.001f);

        var wallComp = el.GetComponent<Wall>();
        Assert.IsNotNull(wallComp, "Wall component should survive round-trip");
        Assert.IsFalse(wallComp!.IsLowered, "wall should not be lowered after restore");
        Assert.IsTrue(el.Movable);
        Assert.AreEqual("concrete", el.MaterialId);
    }

    [Test]
    public void Wall_LoweredState_PreservesFullPosition()
    {
        var wallGo = ElementFactory.CreateWall(new Vector3Int(100, 2700, 3000), "LoweredWall",
            new Vector3(0, 1.35f, -3.5f));
        _spawned.Add(wallGo);
        PartRegistry.Register(wallGo.GetComponent<KitchenElement>());
        var wallComp = wallGo.GetComponent<Wall>();
        var fullPosY = wallComp.FullPosition.y;
        var fullScaleY = wallComp.FullScaleY;

        // опускаем стену
        wallComp.SetLowered(true, 0.1f);
        Assert.IsTrue(wallComp.IsLowered);
        Assert.AreNotEqual(fullPosY, wallGo.transform.position.y, "lowered position should differ from full");

        // сохраняем и загружаем
        FullRoundTrip();

        var restored = Object.FindObjectsByType<KitchenElement>()[0];
        var restoredWall = restored.GetComponent<Wall>();
        Assert.IsNotNull(restoredWall);
        Assert.IsFalse(restoredWall.IsLowered, "wall should not be lowered after restore");
        Assert.AreEqual(fullPosY, restored.transform.position.y, 0.001f,
            "full position should be restored, not lowered position");
    }

    // ── 6. BasePlate ─────────────────────────────────────────────────────

    [Test]
    public void BasePlate_RoundTrip()
    {
        // создаём пол через BasePlate.Create и регистрируем
        var bp = BasePlate.Create();
        var floorGo = bp.Element.gameObject;
        floorGo.tag = "Floor";
        bp.Element.DimensionsMM = new Vector3Int(4000, 18, 5000);
        floorGo.transform.position = new Vector3(0, -0.009f, 0);
        _spawned.Add(floorGo);

        FullRoundTrip();

        var foundFloor = GameObject.FindWithTag("Floor");
        Assert.IsNotNull(foundFloor, "floor should exist after restore");

        var basePlate = foundFloor.GetComponent<BasePlate>();
        Assert.IsNotNull(basePlate, "BasePlate component should exist");

        var el = basePlate.Element;
        Assert.IsNotNull(el, "BasePlate.Element should not be null");
        Assert.AreEqual(new Vector3Int(4000, 18, 5000), el.DimensionsMM, "floor dimensions");
        Assert.AreEqual(0f, foundFloor.transform.position.x, 0.001f, "floor pos.x");
        Assert.AreEqual(-0.009f, foundFloor.transform.position.y, 0.001f, "floor pos.y");
        Assert.AreEqual(0f, foundFloor.transform.position.z, 0.001f, "floor pos.z");
    }

    // ── 7. HandleMode ────────────────────────────────────────────────────

    [Test]
    public void HandleMode_RoundTrip_Resize()
    {
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);
        MakeBoard("ModeBoard", new Vector3Int(400, 400, 18), Vector3.zero);

        FullRoundTrip();

        Assert.AreEqual(ResizeHandleManager.HandleMode.Resize, ResizeHandleManager.Mode);
    }

    [Test]
    public void HandleMode_RoundTrip_Move()
    {
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Move);
        MakeBoard("ModeBoard", new Vector3Int(400, 400, 18), Vector3.zero);

        FullRoundTrip();

        Assert.AreEqual(ResizeHandleManager.HandleMode.Move, ResizeHandleManager.Mode);
    }

    // ── 8. JSON snapshot ─────────────────────────────────────────────────

    [Test]
    public void JsonSnapshot_ContainsAllExpectedFields()
    {
        var board = MakeBoard("SnapshotBoard", new Vector3Int(800, 400, 18), new Vector3(1.0f, 2.0f, 3.0f));
        board.MaterialId = "oak";

        var facade = MakeFacade("SnapshotFacade", new Vector3Int(450, 700, 18), new Vector3(0.5f, 0.35f, -1.0f),
            gapLeft: 3, gapRight: 3, gapTop: 2, gapBottom: 2);
        facade.Mode = DoorMode.HingeFrontRight;

        var assembled = MakeAssembled("SnapshotAssembled", new Vector3Int(600, 800, 18), new Vector3(0.3f, 0.4f, -2.0f),
            AssembledFill.Glass);
        assembled.GrooveCount = 2;

        var radialShelf = MakeRadial("SnapshotRadial", width: 600, depth: 400, thickness: 18,
            cornerRadius: 200, new Vector3(0.2f, 0.01f, -1.5f));

        var wall = MakeWall("SnapshotWall", new Vector3Int(100, 2700, 2000), new Vector3(-2.0f, 1.35f, 0.0f));

        var elements = new List<KitchenElement> { board, facade, assembled, radialShelf, wall };
        var data = SaveLoadManager.CaptureScene(elements);
        var json = SaveLoadManager.Serialize(data);

        Assert.IsNotNull(json);
        Assert.IsTrue(json.Length > 0);

        // Проверяем структуру JSON: все поля присутствуют
        Assert.IsTrue(json.Contains("\"version\""), "version");
        Assert.IsTrue(json.Contains("\"elements\""), "elements");
        Assert.IsTrue(json.Contains("\"handleMode\""), "handleMode");

        // ключевые поля элементов
        Assert.IsTrue(json.Contains("\"name\""), "name field");
        Assert.IsTrue(json.Contains("\"dimensionsMM\""), "dimensionsMM field");
        Assert.IsTrue(json.Contains("\"position\""), "position field");
        Assert.IsTrue(json.Contains("\"rotation\""), "rotation field");
        Assert.IsTrue(json.Contains("\"movable\""), "movable field");
        Assert.IsTrue(json.Contains("\"transparent\""), "transparent field");
        Assert.IsTrue(json.Contains("\"materialId\""), "materialId field");

        // фасадные поля
        Assert.IsTrue(json.Contains("\"isFacade\""), "isFacade field");
        Assert.IsTrue(json.Contains("\"gapLeft\""), "gapLeft field");
        Assert.IsTrue(json.Contains("\"gapRight\""), "gapRight field");
        Assert.IsTrue(json.Contains("\"gapTop\""), "gapTop field");
        Assert.IsTrue(json.Contains("\"gapBottom\""), "gapBottom field");
        Assert.IsTrue(json.Contains("\"doorMode\""), "doorMode field");
        Assert.IsTrue(json.Contains("\"doorOpen\""), "doorOpen field");

        // сборный фасад
        Assert.IsTrue(json.Contains("\"assembled\""), "assembled field");
        Assert.IsTrue(json.Contains("\"assembledFill\""), "assembledFill field");
        Assert.IsTrue(json.Contains("\"grooveCount\""), "grooveCount field");

        // радиусная полка
        Assert.IsTrue(json.Contains("\"isRadialShelf\""), "isRadialShelf field");
        Assert.IsTrue(json.Contains("\"cornerRadius\""), "cornerRadius field");

        // стена
        Assert.IsTrue(json.Contains("\"isWall\""), "isWall field");

        // basePlate
        Assert.IsTrue(json.Contains("\"basePlate\""), "basePlate field");
        Assert.IsTrue(json.Contains("\"basePlateValid\""), "basePlateValid field");
    }

    // ── 9. Multiple element types in one scene ───────────────────────────

    [Test]
    public void MixedElements_AllTypes_RoundTrip()
    {
        MakeBoard("BoardA", new Vector3Int(800, 400, 18), new Vector3(0.5f, 0.2f, 1.0f));
        MakeFacade("FacadeA", new Vector3Int(450, 700, 18), new Vector3(0.5f, 0.35f, -1.0f),
            gapLeft: 3, gapRight: 3, gapTop: 2, gapBottom: 2);
        MakeAssembled("AssembledA", new Vector3Int(600, 800, 18), new Vector3(-0.3f, 0.4f, -2.0f),
            AssembledFill.Blind);
        MakeRadial("RadialA", width: 600, depth: 400, thickness: 18,
            cornerRadius: 200, new Vector3(0.2f, 0.01f, -1.5f));
        MakeWall("WallA", new Vector3Int(100, 2700, 3000), new Vector3(-1.6f, 1.35f, 0.0f));

        FullRoundTrip();

        var all = Object.FindObjectsByType<KitchenElement>();
        Assert.AreEqual(5, all.Length, "all 5 elements should be restored");

        // Проверяем каждый тип
        int boardCount = 0, facadeCount = 0, assembledCount = 0, radialCount = 0, wallCount = 0;
        foreach (var el in all)
        {
            if (el is AssembledFacadeElement) assembledCount++;
            else if (el is FacadeElement) facadeCount++;
            else if (el is RadialShelfElement) radialCount++;
            else if (el.GetComponent<Wall>() != null) wallCount++;
            else boardCount++;
        }

        Assert.AreEqual(1, boardCount, "one board");
        Assert.AreEqual(1, facadeCount, "one facade");
        Assert.AreEqual(1, assembledCount, "one assembled facade");
        Assert.AreEqual(1, radialCount, "one radial shelf");
        Assert.AreEqual(1, wallCount, "one wall");
    }

    // ── 10. Negative / edge case tests ───────────────────────────────────

    [Test]
    public void Element_ZeroDimensions_FailsGracefully()
    {
        // Проверяем: нулевой размер должен быть скорректирован до 1
        MakeBoard("Zero", new Vector3Int(0, 0, 0), Vector3.zero);

        FullRoundTrip();

        var restored = Object.FindObjectsByType<KitchenElement>()[0];
        var dims = restored.DimensionsMM;
        Assert.Greater(dims.x, 0, "dim.x should be clamped to >=1");
        Assert.Greater(dims.y, 0, "dim.y should be clamped to >=1");
        Assert.Greater(dims.z, 0, "dim.z should be clamped to >=1");
    }

    [Test]
    public void Element_MaxDimensions_RoundTrip()
    {
        int max = 10000;
        MakeBoard("Huge", new Vector3Int(max, max, 18), new Vector3(5.0f, 0.5f, 0.0f));

        FullRoundTrip();

        var restored = Object.FindObjectsByType<KitchenElement>()[0];
        Assert.AreEqual(new Vector3Int(max, max, 18), restored.DimensionsMM);
    }

    [Test]
    public void EmptyScene_RoundTrip()
    {
        FullRoundTrip();

        var all = Object.FindObjectsByType<KitchenElement>();
        Assert.AreEqual(0, all.Length, "empty scene should restore zero elements");
    }

    // ── 11. KitchenSettings round-trip (все 12 полей) ────────────────────

    [Test]
    public void Settings_All12Fields_RoundTrip()
    {
        var gs = KitchenSettings.Instance;

        // Устанавливаем не-дефолтные значения
        gs.GridStep = 32;
        gs.GridEnabled = false;
        gs.SnapEnabled = false;
        gs.SnapThreshold = 80f;
        gs.BlockOnViolation = false;
        gs.AutoSave = true;
        gs.AutoSaveInterval = 300;
        gs.SpatialGrid = true;
        gs.WindowedMode = false;
        gs.NormalView.edgeOutline = true;
        gs.NormalView.wallsEnabled = false;
        gs.NormalView.lowerNearWalls = true;

        var data = gs.ToData();

        // Сбрасываем ВСЕ поля в другие значения
        gs.GridStep = 1;
        gs.GridEnabled = true;
        gs.SnapEnabled = true;
        gs.SnapThreshold = 50f;
        gs.BlockOnViolation = true;
        gs.AutoSave = false;
        gs.AutoSaveInterval = 60;
        gs.SpatialGrid = false;
        gs.WindowedMode = true;
        gs.NormalView.edgeOutline = false;
        gs.NormalView.wallsEnabled = true;
        gs.NormalView.lowerNearWalls = false;

        // Применяем — должны восстановиться все 12 полей
        gs.ApplyFrom(data);

        Assert.AreEqual(32, gs.GridStep, "GridStep");
        Assert.IsFalse(gs.GridEnabled, "GridEnabled");
        Assert.IsFalse(gs.SnapEnabled, "SnapEnabled");
        Assert.AreEqual(80f, gs.SnapThreshold, 0.001f, "SnapThreshold");
        Assert.IsFalse(gs.BlockOnViolation, "BlockOnViolation");
        Assert.IsTrue(gs.AutoSave, "AutoSave");
        Assert.AreEqual(300, gs.AutoSaveInterval, "AutoSaveInterval");
        Assert.IsTrue(gs.SpatialGrid, "SpatialGrid");
        Assert.IsFalse(gs.WindowedMode, "WindowedMode");
        Assert.IsTrue(gs.NormalView.edgeOutline, "EdgeOutline");
        Assert.IsFalse(gs.NormalView.wallsEnabled, "WallsEnabled");
        Assert.IsTrue(gs.NormalView.lowerNearWalls, "LowerNearWalls");

        // Возвращаем дефолт
        gs.GridStep = 1; gs.GridEnabled = true; gs.SnapEnabled = true;
        gs.SnapThreshold = 50f; gs.BlockOnViolation = true;
        gs.AutoSave = false; gs.AutoSaveInterval = 60;
        gs.SpatialGrid = false; gs.WindowedMode = true;
        gs.NormalView.edgeOutline = false; gs.NormalView.wallsEnabled = true; gs.NormalView.lowerNearWalls = false;
    }

    [Test]
    public void Settings_WallsEnabled_RoundTrip()
    {
        var gs = KitchenSettings.Instance;
        bool prevWalls = gs.NormalView.wallsEnabled;

        // false round-trips correctly
        gs.NormalView.wallsEnabled = false;
        var data = gs.ToData();
        gs.NormalView.wallsEnabled = true;
        gs.ApplyFrom(data);
        Assert.IsFalse(gs.NormalView.wallsEnabled, "false round-trips correctly");

        // true round-trips correctly
        gs.NormalView.wallsEnabled = true;
        data = gs.ToData();
        gs.NormalView.wallsEnabled = false;
        gs.ApplyFrom(data);
        Assert.IsTrue(gs.NormalView.wallsEnabled, "true round-trips correctly");

        gs.NormalView.wallsEnabled = prevWalls;
    }

    [Test]
    public void Settings_DisplayAndInput_RoundTrip()
    {
        var gs = KitchenSettings.Instance;
        var backup = gs.ToData();

        gs.NormalView.wallOutline = false;
        gs.NormalView.hideOpeningsOnLoweredWalls = true;
        gs.NormalView.objectsVisible = false;
        gs.NormalView.hideLightSources = true;
        gs.MouseSensitivity = 2.5f;
        gs.WasdSpeed = 0.4f;
        gs.ArrowSpeed = 1.8f;

        var data = gs.ToData();

        gs.NormalView.wallOutline = true;
        gs.NormalView.hideOpeningsOnLoweredWalls = false;
        gs.NormalView.objectsVisible = true;
        gs.NormalView.hideLightSources = false;
        gs.MouseSensitivity = 1f;
        gs.WasdSpeed = 1f;
        gs.ArrowSpeed = 1f;

        gs.ApplyFrom(data);

        Assert.IsFalse(gs.NormalView.wallOutline, "WallOutline");
        Assert.IsTrue(gs.NormalView.hideOpeningsOnLoweredWalls, "HideOpeningsOnLoweredWalls");
        Assert.IsFalse(gs.NormalView.objectsVisible, "ObjectsVisible");
        Assert.IsTrue(gs.NormalView.hideLightSources, "HideLightSources");
        Assert.AreEqual(2.5f, gs.MouseSensitivity, 0.001f, "MouseSensitivity");
        Assert.AreEqual(0.4f, gs.WasdSpeed, 0.001f, "WasdSpeed");
        Assert.AreEqual(1.8f, gs.ArrowSpeed, 0.001f, "ArrowSpeed");

        gs.ApplyFrom(backup);
    }

    /// <summary>Старый проект без новых полей: JsonUtility оставляет
    /// инициализаторы, а не нули — иначе стены молча теряли бы контур,
    /// а камера останавливалась бы (множители 0).</summary>
    [Test]
    public void Settings_OldSave_WithoutNewFields_KeepsDefaults()
    {
        var data = JsonUtility.FromJson<KitchenSettingsData>(
            "{\"gridStep\":18,\"gridEnabled\":true,\"wallsEnabled\":true}");

        Assert.IsTrue(data.wallOutline, "wallOutline");
        Assert.IsTrue(data.objectsVisible, "objectsVisible");
        Assert.IsFalse(data.hideOpeningsOnLoweredWalls, "hideOpeningsOnLoweredWalls");
        Assert.IsFalse(data.hideLightSources, "hideLightSources");
        Assert.AreEqual(1f, data.mouseSensitivity, 0.001f, "mouseSensitivity");
        Assert.AreEqual(1f, data.wasdSpeed, 0.001f, "wasdSpeed");
        Assert.AreEqual(1f, data.arrowSpeed, 0.001f, "arrowSpeed");
    }

    /// <summary>Пресеты вида сохраняются раздельно: правка одного не утекает
    /// во второй ни при записи, ни при чтении.</summary>
    [Test]
    public void Settings_BothViewPresets_RoundTrip()
    {
        var gs = KitchenSettings.Instance;
        var backup = gs.ToData();

        gs.NormalView.wallsEnabled = false;
        gs.NormalView.hideLightSources = true;
        gs.RoomView.wallsEnabled = true;
        gs.RoomView.hideLightSources = false;
        gs.RoomView.objectsVisible = false;

        var data = gs.ToData();
        gs.ResetToDefaults();
        gs.ApplyFrom(data);

        Assert.IsFalse(gs.NormalView.wallsEnabled, "обычный: стены");
        Assert.IsTrue(gs.NormalView.hideLightSources, "обычный: свет");
        Assert.IsTrue(gs.RoomView.wallsEnabled, "помещение: стены");
        Assert.IsFalse(gs.RoomView.hideLightSources, "помещение: свет");
        Assert.IsFalse(gs.RoomView.objectsVisible, "помещение: объекты");

        gs.ApplyFrom(backup);
    }

    /// <summary>Миграция фоторежима. В сборках, где у URPForwardRenderer не был
    /// назначен postProcessData, вся постобработка молча не выполнялась: тонемаппинг,
    /// экспозиция, свечение и виньетка не влияли ни на один пиксель. Числа, которые
    /// пользователь крутил в тот период, поэтому не несут его выбора — они не были
    /// ничем проверены глазом. Когда постобработка ожила, свечение 103 % при пороге
    /// 108 % выжгло 33 % кадра добела, а виньетка 70 % задавила 24 %.
    /// Отсутствие photoSchema в файле — точный штамп той эпохи: его пишет только
    /// сборка с уже живым конвейером. Так что вся секция возвращается к умолчаниям,
    /// а одноразовый флаг ConsumePhotoLookMigratedNotice поднимает всплывашку в UI —
    /// молча переписывать чужие настройки нельзя.
    /// Кадрирование камеры не трогается: оно живёт в ProjectData, а не здесь.</summary>
    [Test]
    public void Settings_PhotoLookFromDeadPipelineEra_ResetsToDefaultsAndSaysSo()
    {
        var gs = KitchenSettings.Instance;
        var backup = gs.ToData();

        var data = JsonUtility.FromJson<KitchenSettingsData>(
            "{\"photoBloomPct\":103,\"photoBloomThresholdPct\":108,"
            + "\"photoVignettePct\":70,\"photoExposurePct\":3,\"photoAmbientPct\":99,"
            + "\"photoFloorBouncePct\":97,\"photoSSGI\":true,\"photoAoFalloffM\":11}");
        Assert.AreEqual(0, data.photoSchema, "старый файл без штампа схемы фоторежима");

        gs.ApplyFrom(data);

        Assert.IsTrue(gs.ConsumePhotoLookMigratedNotice(), "миграция должна себя объявить");
        Assert.IsFalse(gs.ConsumePhotoLookMigratedNotice(),
            "объявляется один раз: иначе всплывашка вернётся при каждом следующем открытии");
        Assert.AreEqual(KitchenSettings.PHOTO_BLOOM_DEFAULT_PCT, gs.PhotoBloomPct, "свечение");
        Assert.AreEqual(KitchenSettings.PHOTO_BLOOM_THRESHOLD_DEFAULT_PCT,
            gs.PhotoBloomThresholdPct, "порог свечения");
        Assert.AreEqual(KitchenSettings.PHOTO_BLOOM_CLAMP_DEFAULT_PCT,
            gs.PhotoBloomClampPct, "предел свечения");
        Assert.AreEqual(KitchenSettings.PHOTO_VIGNETTE_DEFAULT_PCT, gs.PhotoVignettePct, "виньетка");
        Assert.AreEqual(KitchenSettings.PHOTO_EXPOSURE_DEFAULT_PCT, gs.PhotoExposurePct, "экспозиция");
        Assert.AreEqual(KitchenSettings.PHOTO_FLOOR_BOUNCE_DEFAULT_PCT,
            gs.PhotoFloorBouncePct, "отскок от пола");
        Assert.AreEqual(KitchenSettings.PHOTO_AO_FALLOFF_DEFAULT_M, gs.PhotoAoFalloffM, "затухание AO");
        Assert.IsFalse(gs.PhotoSSGI, "SSGI выключен по умолчанию");

        gs.ApplyFrom(backup);
    }

    /// <summary>Обратная сторона той же миграции: файл, записанный уже живым
    /// конвейером, несёт photoSchema = 1, и его числа — осознанный выбор. Их не
    /// трогают, иначе пользователь не смог бы сохранить ни одну свою настройку.
    /// Без этого теста первый был бы зелёным и при «сбрасывать всегда».</summary>
    [Test]
    public void Settings_PhotoLookSavedByALivePipeline_SurvivesTheRoundTrip()
    {
        var gs = KitchenSettings.Instance;
        var backup = gs.ToData();

        gs.PhotoBloomPct = 77;
        gs.PhotoVignettePct = 41;
        gs.PhotoBloomClampPct = 913;
        var saved = gs.ToData();
        Assert.AreEqual(KitchenSettingsData.CURRENT_PHOTO_SCHEMA, saved.photoSchema,
            "сохранение всегда ставит текущий штамп");

        gs.ResetToDefaults();
        gs.ApplyFrom(saved);

        Assert.IsFalse(gs.ConsumePhotoLookMigratedNotice(), "новый файл не мигрируют");
        Assert.AreEqual(77, gs.PhotoBloomPct, "свечение пережило круг");
        Assert.AreEqual(41, gs.PhotoVignettePct, "виньетка пережила круг");
        Assert.AreEqual(913, gs.PhotoBloomClampPct, "предел свечения пережил круг");

        gs.ApplyFrom(backup);
    }

    /// <summary>Миграция: у старого проекта (viewSchema = 0) единственный набор
    /// флагов был общим на все режимы — он становится пресетом обычного, а
    /// «помещение» получает значения из коробки.</summary>
    [Test]
    public void Settings_OldSave_MigratesFlatFlagsIntoNormalPreset()
    {
        var gs = KitchenSettings.Instance;
        var backup = gs.ToData();

        var data = JsonUtility.FromJson<KitchenSettingsData>(
            "{\"gridStep\":18,\"gridEnabled\":true,\"wallsEnabled\":false,"
            + "\"objectsVisible\":false,\"hideLightSources\":true}");
        Assert.AreEqual(0, data.viewSchema, "старый файл без версии");

        gs.ApplyFrom(data);

        Assert.IsFalse(gs.NormalView.wallsEnabled, "обычный: стены из старых полей");
        Assert.IsFalse(gs.NormalView.objectsVisible, "обычный: объекты из старых полей");
        Assert.IsTrue(gs.NormalView.hideLightSources, "обычный: свет из старых полей");

        Assert.IsTrue(gs.RoomView.wallsEnabled, "помещение: значения из коробки");
        Assert.IsTrue(gs.RoomView.objectsVisible, "помещение: значения из коробки");
        Assert.IsFalse(gs.RoomView.hideLightSources, "помещение: значения из коробки");

        gs.ApplyFrom(backup);
    }

    // ── 12. Full ProjectData round-trip (groups + camera + baseplate) ────

    [Test]
    public void FullProjectData_WithGroups_Camera_BasePlate_RoundTrip()
    {
        // Элементы
        var board = MakeBoard("BoardG", new Vector3Int(800, 400, 18), new Vector3(0.5f, 0.2f, 0));
        var facade = MakeFacade("FacadeG", new Vector3Int(450, 700, 18), new Vector3(0, 0.35f, -1.0f));

        // Группа
        var group = GroupManager.Link(new List<KitchenElement> { board, facade });
        Assert.IsNotNull(group);
        Assert.AreEqual(group!.id, board.GroupId);
        Assert.AreEqual(group.id, facade.GroupId);

        // BasePlate
        var bp = BasePlate.Create();
        bp.Element.DimensionsMM = new Vector3Int(3500, 18, 4000);
        bp.transform.position = new Vector3(0, -0.009f, 0);
        _spawned.Add(bp.gameObject);
        PartRegistry.Register(bp.Element);

        // Capture
        var elements = new List<KitchenElement> { board, facade };
        var data = SaveLoadManager.CaptureScene(elements);

        // Проверяем capture
        Assert.IsTrue(data.basePlateValid, "basePlateValid");
        Assert.AreEqual(new[] { 3500, 18, 4000 }, data.basePlate!.dimensionsMM);
        Assert.AreEqual(1, data.groups.Length, "groups count");
        Assert.AreEqual(group!.id, data.groups[0].id);
        Assert.AreEqual("Группа", data.groups[0].name);

        // Serialize → Deserialize
        var json = SaveLoadManager.Serialize(data);
        var restored = SaveLoadManager.Deserialize(json);

        // Элементы
        Assert.AreEqual(2, restored!.elements.Length);
        Assert.AreEqual("BoardG", restored.elements[0].name);
        Assert.AreEqual("FacadeG", restored.elements[1].name);

        // Группы
        Assert.AreEqual(1, restored.groups.Length);
        Assert.AreEqual(group.id, restored.groups[0].id);
        Assert.IsTrue(restored.groups[0].movable);

        // BasePlate
        Assert.IsTrue(restored.basePlateValid);
        Assert.AreEqual(new[] { 3500, 18, 4000 }, restored!.basePlate!.dimensionsMM);

        // HandleMode
        Assert.AreEqual("Resize", restored.handleMode);
    }

    /// <summary>Все пять параметров винтовой опоры переживают сохранение.
    ///
    /// Значения намеренно НЕ по умолчанию: на дефолтах поле, которое вообще не
    /// пишется, читается как правильное, и тест зелёный против кода, который
    /// его потерял.</summary>
    [Test]
    public void ScrewLeg_AllProperties_RoundTrip()
    {
        var go = ElementFactory.CreateScrewLeg("Опора1", new Vector3(0.4f, 0.045f, -1.2f));
        var leg = (ScrewLegElement)GetElement(go);
        leg.Thread = ScrewLegSpec.ThreadM8;
        leg.BaseDiameterMM = 30;
        leg.BaseHeightMM = 10;
        leg.ThreadLengthMM = 70;
        leg.InsertionDepthMM = 20;

        FullRoundTrip();

        var r = Object.FindObjectsByType<KitchenElement>()[0] as ScrewLegElement;
        Assert.IsNotNull(r, "восстановиться обязана именно опора, а не доска");
        Assert.AreEqual(ScrewLegSpec.ThreadM8, r!.Thread, "резьба");
        Assert.AreEqual(30, r!.BaseDiameterMM, "диаметр основания");
        Assert.AreEqual(10, r!.BaseHeightMM, "высота основания");
        Assert.AreEqual(70, r!.ThreadLengthMM, "длина резьбы");
        Assert.AreEqual(20, r!.InsertionDepthMM, "заход в корпус");
        Assert.AreEqual(60, r!.HeightAboveFloorMM, "высота над полом = 70 − 20 + 10");
        Assert.AreEqual(new Vector3Int(30, 80, 30), r!.DimensionsMM,
            "габарит опоры — пятка плюс вся резьба");
    }

    /// <summary>Труба сохраняет ИСТОЧНИК ИСТИНЫ и только его: условный проход и
    /// длину. Наружный, внутренний и толщина стенки в файл не попадают — их
    /// пересчитывают из ДУ при загрузке, поэтому проверять их надо ПОСЛЕ
    /// круга: значение, записанное в файл, пережило бы даже правку таблицы
    /// ГОСТ, а вычисленное — нет (conventions/SERIALIZATION.md → «Golden rule:
    /// serialize SOURCE OF TRUTH»).</summary>
    [Test]
    public void Pipe_KeepsItsBoreAndLength_AndRederivesEveryDiameter()
    {
        var go = ElementFactory.CreatePipe(KitchenDesigner.Core.Plumbing.PipeSpec.Dn32, 1450,
            "Стояк1", new Vector3(0.4f, 0.725f, -1.2f));
        Assert.IsNotNull(GetElement(go));

        FullRoundTrip();

        var r = Object.FindObjectsByType<KitchenElement>()[0] as PipeElement;
        Assert.IsNotNull(r, "восстановиться обязана именно труба, а не доска");
        Assert.AreEqual(KitchenDesigner.Core.Plumbing.PipeSpec.Dn32, r!.SizeId, "условный проход");
        Assert.AreEqual(1450, r!.LengthMM, "длина");
        Assert.AreEqual(42.3f, r!.OuterDiameterMm, 0.001f, "наружный Ø выведен из ДУ 32");
        Assert.AreEqual(35.9f, r!.InnerDiameterMm, 0.001f, "внутренний Ø тоже");
        Assert.AreEqual(new Vector3Int(42, 1450, 42), r!.DimensionsMM,
            "габарит: сечение по наружному диаметру, высота — длина трассы");
    }
}
