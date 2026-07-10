using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

public class McpCommandHandlerTests
{
    private McpCommandHandler _handler;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        _handler = new McpCommandHandler();
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
        MaterialCatalog.ClearDynamic();
    }

    private McpRequest MakeReq(string method, object data)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        return new McpRequest
        {
            id = "test",
            method = method,
            Params = Newtonsoft.Json.Linq.JObject.Parse(json)
        };
    }

    private KitchenElement MakeElement(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [Test]
    public void CreateElement_UsesTemplateName_WhenNameNotGiven()
    {
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "TestBoard", width = 600, height = 400, depth = 18, x = 0f, y = 0f, z = 0f
        }));

        Assert.AreEqual("result", resp.type);
        var el = PartRegistry.GetAll().Find(e => e.PartName == "TestBoard");
        Assert.NotNull(el);
        Assert.AreEqual("TestBoard", el.PartName);
    }

    [Test]
    public void CreateElement_UsesName_WhenProvided()
    {
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "tmpl", name = "CustomName", width = 600, height = 400, depth = 18, x = 0f, y = 0f, z = 0f
        }));

        Assert.AreEqual("result", resp.type);
        var el = PartRegistry.GetAll().Find(e => e.PartName == "CustomName");
        Assert.NotNull(el);
        Assert.AreEqual("CustomName", el.PartName);
    }

    [Test]
    public void CreateElement_AddsWall_WhenIsWallTrue()
    {
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "WallBoard", width = 2000, height = 2500, depth = 100, x = 0f, y = 1.25f, z = 0f, is_wall = true
        }));

        Assert.AreEqual("result", resp.type);
        var el = PartRegistry.GetAll().Find(e => e.PartName == "WallBoard");
        Assert.NotNull(el);
        Assert.NotNull(el.GetComponent<Wall>(), "Wall component should be added when is_wall=true");
    }

    [Test]
    public void CreateElement_DoesNotAddWall_WhenIsWallFalse()
    {
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "RegularBoard", width = 800, height = 400, depth = 18, x = 0f, y = 0f, z = 0f, is_wall = false
        }));

        Assert.AreEqual("result", resp.type);
        var el = PartRegistry.GetAll().Find(e => e.PartName == "RegularBoard");
        Assert.NotNull(el);
        Assert.IsNull(el.GetComponent<Wall>(), "Wall component should NOT be added when is_wall=false");
    }

    [Test]
    public void CreateElement_DefaultsIsWallFalse_WhenOmitted()
    {
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "DefaultBoard", width = 800, height = 400, depth = 18, x = 0f, y = 0f, z = 0f
        }));

        Assert.AreEqual("result", resp.type);
        var el = PartRegistry.GetAll().Find(e => e.PartName == "DefaultBoard");
        Assert.NotNull(el);
        Assert.IsNull(el.GetComponent<Wall>(), "Wall component should NOT be added when is_wall omitted");
    }

    [Test]
    public void ResizeElement_UsesWidthHeightDepth()
    {
        var el = MakeElement("TestBoard", new Vector3Int(800, 400, 18), Vector3.zero);
        var resp = _handler.Handle(MakeReq("resize_element", new
        {
            name = "TestBoard", width = 1200, height = 600, depth = 36
        }));

        if (resp.type == "error")
            Assert.Fail("ResizeElement failed: data=" + (resp.data != null ? resp.data.GetType() + ": " + resp.data.ToString() : "null"));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3Int(1200, 600, 36), el.DimensionsMM);
    }

    [Test]
    public void ResizeElement_UsesDimXDimYDimZ_AsAliases()
    {
        var el = MakeElement("TestBoard", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler.Handle(MakeReq("resize_element", new
        {
            name = "TestBoard", dimX = 3170, dimY = 2500, dimZ = 7240
        }));

        if (resp.type == "error")
            Assert.Fail("ResizeElement failed: data=" + (resp.data != null ? resp.data.GetType() + ": " + resp.data.ToString() : "null"));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3Int(3170, 2500, 7240), el.DimensionsMM);
    }

    [Test]
    public void ResizeElement_PrefersWidthOverDimX()
    {
        var el = MakeElement("TestBoard", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler.Handle(MakeReq("resize_element", new
        {
            name = "TestBoard", width = 100, height = 200, depth = 300, dimX = 9999, dimY = 9999, dimZ = 9999
        }));

        if (resp.type == "error")
            Assert.Fail("ResizeElement failed: data=" + (resp.data != null ? resp.data.GetType() + ": " + resp.data.ToString() : "null"));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3Int(100, 200, 300), el.DimensionsMM);
    }

    [Test]
    public void AddWallComponent_AddsWallToElement()
    {
        var el = MakeElement("TestBoard", new Vector3Int(800, 400, 18), Vector3.zero);
        Assert.IsNull(el.GetComponent<Wall>());

        var resp = _handler.Handle(MakeReq("add_wall_component", new { name = "TestBoard" }));

        if (resp.type == "error")
            Assert.Fail("AddWallComponent failed: data=" + (resp.data != null ? resp.data.GetType() + ": " + resp.data.ToString() : "null"));

        Assert.AreEqual("result", resp.type);
        Assert.NotNull(el.GetComponent<Wall>());
    }

    [Test]
    public void AddWallComponent_Idempotent_WhenAlreadyWall()
    {
        var el = MakeElement("TestBoard", new Vector3Int(800, 400, 18), Vector3.zero);
        el.gameObject.AddComponent<Wall>();

        var resp = _handler.Handle(MakeReq("add_wall_component", new { name = "TestBoard" }));

        if (resp.type == "error")
            Assert.Fail("AddWallComponent failed: data=" + (resp.data != null ? resp.data.GetType() + ": " + resp.data.ToString() : "null"));

        Assert.AreEqual("result", resp.type);
        Assert.NotNull(el.GetComponent<Wall>());
    }

    [Test]
    public void AddWallComponent_Errors_WhenElementNotFound()
    {
        var resp = _handler.Handle(MakeReq("add_wall_component", new { name = "NonExistent" }));

        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void WallElement_CreatedViaMCP_IsAnchor_NotViolation()
    {
        _handler.Handle(MakeReq("create_element", new
        {
            template_name = "TestWall", width = 2000, height = 2500, depth = 100, x = 0f, y = 1.25f, z = 0f, is_wall = true
        }));

        var wall = PartRegistry.GetAll().Find(e => e.PartName == "TestWall");
        Assert.NotNull(wall);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { wall });
        Assert.IsFalse(result.violations.Exists(e => e.GetComponent<Wall>() != null));
    }

    private static T GetProp<T>(object obj, string name)
    {
        var p = obj.GetType().GetProperty(name);
        Assert.NotNull(p, $"Property '{name}' not found on {obj.GetType()}");
        return (T)p.GetValue(obj, null);
    }

    private KitchenElement MakeWall(string name, Vector3Int dims, Vector3 pos)
    {
        var el = MakeElement(name, dims, pos);
        el.gameObject.AddComponent<Wall>();
        return el;
    }

    [Test]
    public void CreateElement_Wall_Response_HasNoViolations()
    {
        MakeWall("Existing", new Vector3Int(2000, 2500, 100), Vector3.zero);
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "Wall2", width = 2000, height = 2500, depth = 100, x = 2.2f, y = 1.25f, z = 0f, is_wall = true
        }));

        Assert.AreEqual("result", resp.type);
        Assert.IsFalse(GetProp<bool>(resp.data, "hasViolations"));
    }

    [Test]
    public void CreateElement_Response_HasViolations_WhenOverlapping()
    {
        MakeElement("Existing", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "Overlap", width = 600, height = 400, depth = 18, x = 0f, y = 0f, z = 0f
        }));

        Assert.AreEqual("result", resp.type);
        Assert.IsTrue(GetProp<bool>(resp.data, "hasViolations"));
    }

    [Test]
    public void CreateElement_Wall_Response_NoViolations_WhenOverlappingWithAnotherWall()
    {
        MakeWall("W1", new Vector3Int(2000, 2500, 100), Vector3.zero);
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "W2", width = 2000, height = 2500, depth = 100, x = 0f, y = 1.25f, z = 0f, is_wall = true
        }));

        Assert.AreEqual("result", resp.type);
        Assert.IsFalse(GetProp<bool>(resp.data, "hasViolations"));
    }

    [Test]
    public void GetElementInfo_Response_HasViolations_WhenOverlapping()
    {
        MakeElement("A", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        MakeElement("B", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        var resp = _handler.Handle(MakeReq("get_element_info", new { name = "A" }));

        Assert.AreEqual("result", resp.type);
        var info = (ElementInfo)resp.data;
        Assert.IsTrue(info.hasViolations, "A overlaps with B → violation");
    }

    [Test]
    public void GetElementInfo_Wall_Response_HasNoViolations()
    {
        MakeWall("W", new Vector3Int(2000, 2500, 100), Vector3.zero);
        var resp = _handler.Handle(MakeReq("get_element_info", new { name = "W" }));

        Assert.AreEqual("result", resp.type);
        var info = (ElementInfo)resp.data;
        Assert.IsFalse(info.hasViolations, "Стена — якорь → нет нарушений");
    }

    [Test]
    public void GetAllElements_Response_ContainsHasViolations()
    {
        MakeElement("A", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        MakeElement("B", new Vector3Int(1000, 1000, 1000), Vector3.zero);

        var resp = _handler.Handle(MakeReq("get_all_elements", new { }));

        Assert.AreEqual("result", resp.type);
        var list = (List<ElementInfo>)resp.data;
        Assert.AreEqual(2, list.Count);
        foreach (var info in list)
            Assert.IsTrue(info.hasViolations, $"{info.name} overlaps → violation");
    }

    [Test]
    public void MoveElement_Response_HasViolations_WhenOverlapping()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));
        var resp = _handler.Handle(MakeReq("move_element", new { name = "A", x = 1.8f, y = 0f, z = 0f }));

        Assert.AreEqual("result", resp.type);
        Assert.IsTrue(GetProp<bool>(resp.data, "hasViolations"));
    }

    [Test]
    public void ResizeElement_Response_HasViolations_WhenOverlapping()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(0.45f, 0f, 0f));
        var resp = _handler.Handle(MakeReq("resize_element", new { name = "A", width = 1000, height = 400, depth = 18 }));

        Assert.AreEqual("result", resp.type);
        Assert.IsTrue(GetProp<bool>(resp.data, "hasViolations"));
    }

    [Test]
    public void GetViolations_ReturnsOverlappingElements()
    {
        MakeElement("A", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        MakeElement("B", new Vector3Int(1000, 1000, 1000), Vector3.zero);

        var resp = _handler.Handle(MakeReq("get_violations", new { }));

        Assert.AreEqual("result", resp.type);
        var count = GetProp<int>(resp.data, "count");
        Assert.AreEqual(2, count);
        var violations = GetProp<object>(resp.data, "violations") as System.Collections.IList;
        Assert.IsNotNull(violations);
        Assert.AreEqual(2, violations.Count);
    }

    [Test]
    public void GetViolations_ReturnsOverlapDetails()
    {
        MakeElement("A", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        MakeElement("B", new Vector3Int(1000, 1000, 1000), new Vector3(0f, 0f, 0f));

        var resp = _handler.Handle(MakeReq("get_violations", new { }));
        var violations = GetProp<object>(resp.data, "violations") as System.Collections.IList;

        var vA = violations[0];
        Assert.AreEqual("A", GetProp<string>(vA, "name"));
        Assert.IsFalse(GetProp<bool>(vA, "disconnected"));
        var overlapsA = GetProp<object>(vA, "overlapsWith") as System.Collections.IList;
        Assert.IsNotNull(overlapsA);
        Assert.AreEqual(1, overlapsA.Count);
        var overlap = overlapsA[0];
        Assert.AreEqual("B", GetProp<string>(overlap, "neighbor"));
        Assert.Greater(GetProp<float>(overlap, "overlapXmm"), 990);
        Assert.Greater(GetProp<float>(overlap, "overlapYmm"), 990);
        Assert.Greater(GetProp<float>(overlap, "overlapZmm"), 990);
    }

    [Test]
    public void GetViolations_ReturnsEmpty_WhenNoOverlaps()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(0.5f, 0f, 0f));

        var resp = _handler.Handle(MakeReq("get_violations", new { }));

        Assert.AreEqual("result", resp.type);
        var count = GetProp<int>(resp.data, "count");
        Assert.AreEqual(0, count);
    }

    [Test]
    public void CreateFacade_ReturnsOk()
    {
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "TestFacade", name = "F1", x = 0f, y = 0f, z = 0f,
            width = 400, height = 300, depth = 18, is_facade = true,
            gapLeft = 1, gapRight = 2, gapTop = 3, gapBottom = 4
        }));

        Assert.AreEqual("result", resp.type);
        Assert.IsTrue(GetProp<bool>(resp.data, "is_facade"));
        var el = FindBoard("F1");
        Assert.IsNotNull(el);
        var facade = el.GetComponent<FacadeElement>();
        Assert.IsNotNull(facade);
        Assert.AreEqual(1, facade.GapLeft);
        Assert.AreEqual(2, facade.GapRight);
        Assert.AreEqual(3, facade.GapTop);
        Assert.AreEqual(4, facade.GapBottom);
        Assert.AreEqual(new Vector3Int(400, 300, 18), facade.DimensionsMM);
    }

    [Test]
    public void CreateFacade_DefaultGap_IsTwoOnAllSides()
    {
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "Facade2", name = "F2", x = 0f, y = 0f, z = 0f,
            width = 400, height = 300, depth = 18, is_facade = true
        }));

        Assert.AreEqual("result", resp.type);
        var el = FindBoard("F2");
        var facade = el.GetComponent<FacadeElement>();
        Assert.AreEqual(2, facade.GapLeft);
        Assert.AreEqual(2, facade.GapRight);
        Assert.AreEqual(2, facade.GapTop);
        Assert.AreEqual(2, facade.GapBottom);
    }

    [Test]
    public void CreateRadialShelf_UsesProvidedRadiusAndThickness()
    {
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "RS1", name = "RS1", x = 0.5f, y = 0.1f, z = -1f,
            depth = 25, radius = 450, is_radial_shelf = true
        }));

        Assert.AreEqual("result", resp.type);
        Assert.IsTrue(GetProp<bool>(resp.data, "is_radial_shelf"));
        Assert.AreEqual(450, GetProp<int>(resp.data, "radius"));
        var el = FindBoard("RS1");
        Assert.IsNotNull(el);
        var shelf = el.GetComponent<RadialShelfElement>();
        Assert.IsNotNull(shelf);
        Assert.AreEqual(450, shelf.Radius);
        Assert.AreEqual(new Vector3Int(450, 25, 450), shelf.DimensionsMM);
    }

    [Test]
    public void CreateRadialShelf_DefaultsRadius_WhenNotProvided()
    {
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "RS2", name = "RS2", x = 0f, y = 0f, z = 0f,
            is_radial_shelf = true
        }));

        Assert.AreEqual("result", resp.type);
        Assert.IsTrue(GetProp<bool>(resp.data, "is_radial_shelf"));
        Assert.AreEqual(300, GetProp<int>(resp.data, "radius"));
        var el = FindBoard("RS2");
        Assert.IsNotNull(el);
        Assert.AreEqual(300, el.GetComponent<RadialShelfElement>().Radius);
    }

    [Test]
    public void ConvertElement_PartToRadialShelf_ChangesType()
    {
        _handler.Handle(MakeReq("create_element", new
        {
            template_name = "Part1", name = "Part1", x = 0f, y = 0f, z = 0f,
            width = 400, height = 18, depth = 300
        }));

        var resp = _handler.Handle(MakeReq("convert_element", new
        {
            name = "Part1", target = "radial_shelf"
        }));

        Assert.AreEqual("result", resp.type);
        var el = FindBoard("Part1");
        Assert.IsNotNull(el);
        Assert.IsNotNull(el.GetComponent<RadialShelfElement>());
        Assert.AreEqual(400, el.GetComponent<RadialShelfElement>().Radius);
    }

    [Test]
    public void SetElementLock_LocksElement()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        Assert.IsTrue(el.Movable);

        var resp = _handler.Handle(MakeReq("set_element_lock", new { name = "Board", locked = true }));

        Assert.AreEqual("result", resp.type);
        Assert.IsFalse(el.Movable);
    }

    [Test]
    public void SetElementLock_UnlocksElement()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.Movable = false;

        var resp = _handler.Handle(MakeReq("set_element_lock", new { name = "Board", locked = false }));

        Assert.AreEqual("result", resp.type);
        Assert.IsTrue(el.Movable);
    }

    [Test]
    public void SetElementLock_Errors_WhenNameMissing()
    {
        var resp = _handler.Handle(MakeReq("set_element_lock", new { }));
        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void SetElementLock_Errors_WhenElementNotFound()
    {
        var resp = _handler.Handle(MakeReq("set_element_lock", new { name = "NonExistent", locked = true }));
        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void MoveElement_Errors_WhenLocked()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.Movable = false;

        var resp = _handler.Handle(MakeReq("move_element", new { name = "Board", x = 1f, y = 0f, z = 0f }));

        Assert.AreEqual("error", resp.type);
        Assert.AreEqual(Vector3.zero, el.transform.position);
    }

    [Test]
    public void ResizeElement_Errors_WhenLocked()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.Movable = false;

        var resp = _handler.Handle(MakeReq("resize_element", new { name = "Board", width = 1200, height = 600, depth = 36 }));

        Assert.AreEqual("error", resp.type);
        Assert.AreEqual(new Vector3Int(800, 400, 18), el.DimensionsMM);
    }

    [Test]
    public void RotateElement_Errors_WhenLocked()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.Movable = false;

        var resp = _handler.Handle(MakeReq("rotate_element", new { name = "Board", x = 0f, y = 90f, z = 0f }));

        Assert.AreEqual("error", resp.type);
        Assert.AreEqual(Quaternion.identity, el.transform.rotation);
    }

    [Test]
    public void DeleteElement_Errors_WhenLocked()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.Movable = false;

        var resp = _handler.Handle(MakeReq("delete_element", new { name = "Board" }));

        Assert.AreEqual("error", resp.type);
        Assert.IsNotNull(FindBoard("Board"), "Element should not be deleted");
    }

    [Test]
    public void LockedElement_CanBeUnlockedAndMoved()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.Movable = false;

        var unlockResp = _handler.Handle(MakeReq("set_element_lock", new { name = "Board", locked = false }));
        Assert.AreEqual("result", unlockResp.type);
        Assert.IsTrue(el.Movable);

        var moveResp = _handler.Handle(MakeReq("move_element", new { name = "Board", x = 1f, y = 0f, z = 0f }));
        Assert.AreEqual("result", moveResp.type);
        Assert.AreEqual(new Vector3(1, 0, 0), el.transform.position);
    }

    // ── Params object format tests ──────────────────────────────────────

    [Test]
    public void ParamsObject_CreateElement_WorksIdenticalToStringFormat()
    {
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "ObjBoard", width = 600, height = 400, depth = 18, x = 0f, y = 0f, z = 0f
        }));

        Assert.AreEqual("result", resp.type);
        var el = PartRegistry.GetAll().Find(e => e.PartName == "ObjBoard");
        Assert.NotNull(el);
        Assert.AreEqual(new Vector3Int(600, 400, 18), el.DimensionsMM);
    }

    [Test]
    public void ParamsObject_MoveElement_WorksIdenticalToStringFormat()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler.Handle(MakeReq("move_element", new { name = "Board", x = 2.5f, y = 1.0f, z = 0.5f }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3(2.5f, 1f, 0.5f), el.transform.position);
    }

    [Test]
    public void ParamsObject_ResizeElement_PrefersWidthOverDimX()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler.Handle(MakeReq("resize_element", new
        {
            name = "Board", width = 500, height = 600, depth = 36,
            dimX = 9999, dimY = 9999, dimZ = 9999
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3Int(500, 600, 36), el.DimensionsMM);
    }

    [Test]
    public void ParamsObject_Serialization_DoesNotEscape()
    {
        var data = new { name = "TestBoard", x = 1.5, y = 0f, z = 0f };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(
            new { id = "test", method = "move_element", Params = data });

        var parsed = Newtonsoft.Json.Linq.JObject.Parse(json);
        var p = parsed["Params"];
        Assert.IsNotNull(p);
        Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Object, p.Type,
            "Params должен быть JSON-объектом, не строкой");
        Assert.AreEqual("TestBoard", p.Value<string>("name"));
        Assert.AreEqual(1.5, p.Value<double>("x"), 0.001);
    }

    [Test]
    public void McpRequest_DeserializesParamsAsObject_NotString()
    {
        var json = @"{""id"":""req-1"",""method"":""move_element"",""params"":{""name"":""Board1"",""x"":1.5,""y"":0,""z"":0}}";
        var req = Newtonsoft.Json.JsonConvert.DeserializeObject<McpRequest>(json);

        Assert.AreEqual("req-1", req.id);
        Assert.AreEqual("move_element", req.method);
        Assert.IsNotNull(req.Params, "Params должен быть десериализован из JSON-объекта");
        Assert.AreEqual("Board1", req.Params.Value<string>("name"));
    }

    [Test]
    public void ParamsObject_SimulateMove_ReturnsSimulateResult()
    {
        var go = new GameObject("Board");
        go.transform.position = Vector3.zero;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = "Board";
        e.DimensionsMM = new Vector3Int(800, 400, 18);
        go.AddComponent<Wall>();
        PartRegistry.Register(e);
        _spawned.Add(go);

        var resp = _handler.Handle(MakeReq("simulate_move", new { name = "Board", x = 2f, y = 0f, z = 0f }));

        Assert.AreEqual("result", resp.type);
        var sim = resp.data as SimulateResult;
        Assert.IsNotNull(sim, "simulate_move должен вернуть SimulateResult");
        Assert.AreEqual("Board", sim.name);
        Assert.IsFalse(sim.wouldHaveViolations,
            "Перемещение в пустое место не должно создавать нарушений");
    }

    [Test]
    public void ParamsObject_GetElementInfo_ReturnsElementInfo()
    {
        MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler.Handle(MakeReq("get_element_info", new { name = "Board" }));

        Assert.AreEqual("result", resp.type);
        var info = resp.data as ElementInfo;
        Assert.IsNotNull(info);
        Assert.AreEqual("Board", info.name);
        Assert.AreEqual(800, info.dimX);
    }

    [Test]
    public void ParamsObject_EmptyObject_ReturnsError()
    {
        var resp = _handler.Handle(MakeReq("move_element", new { }));

        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void ParamsObject_WithNull_DefaultsToEmpty()
    {
        var req = new McpRequest
        {
            id = "test",
            method = "get_all_elements",
            Params = null
        };

        var resp = _handler.Handle(req);
        Assert.AreEqual("result", resp.type);
    }

    [Test]
    public void GetElements_ReturnsEtag()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        var resp = _handler.Handle(MakeReq("get_all_elements", new { }));
        Assert.AreEqual("result", resp.type);
        Assert.IsNotEmpty(resp.etag, "etag should be present");
    }

    [Test]
    public void GetElements_NotModified_WhenEtagMatches()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        var first = _handler.Handle(MakeReq("get_all_elements", new { }));
        Assert.AreEqual("result", first.type);
        var etag = first.etag;

        // Повторный запрос без изменений — должен вернуть not_modified
        var req = MakeReq("get_all_elements", new { });
        req.Headers = new Dictionary<string, string> { { "If-None-Match", etag } };
        var second = _handler.Handle(req);
        Assert.AreEqual("not_modified", second.type);
        Assert.AreEqual(etag, second.etag);
    }

    [Test]
    public void GetElements_NotModified_WithoutHeaders_ReturnsResult()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        var first = _handler.Handle(MakeReq("get_all_elements", new { }));
        // Второй запрос без заголовка If-None-Match — обычный result
        var second = _handler.Handle(MakeReq("get_all_elements", new { }));
        Assert.AreEqual("result", second.type);
        Assert.AreEqual(first.etag, second.etag);
    }

    [Test]
    public void GetElements_ReturnsNewResult_AfterElementChange()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        var first = _handler.Handle(MakeReq("get_all_elements", new { }));
        var etag = first.etag;

        // Добавляем новый элемент
        MakeElement("B", new Vector3Int(600, 500, 18), Vector3.zero);

        // Запрос со старым etag — должен вернуть новый result
        var req = MakeReq("get_all_elements", new { });
        req.Headers = new Dictionary<string, string> { { "If-None-Match", etag } };
        var second = _handler.Handle(req);
        Assert.AreEqual("result", second.type);
        Assert.AreNotEqual(etag, second.etag, "etag changed after adding element");
    }

    private static KitchenElement FindBoard(string name)
    {
        foreach (var el in PartRegistry.GetAll())
            if (el.PartName == name) return el;
        return null;
    }

    // ── Nullable-координаты: омитить ось = оставить текущее (не 0) ───────

    [Test]
    public void MoveElement_OmittedAxes_KeepCurrentValue()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), new Vector3(1f, 2f, 3f));

        // Двигаем только по X — Y и Z должны остаться (2,3), а не сброситься в 0.
        var resp = _handler.Handle(MakeReq("move_element", new { name = "Board", x = 5f }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3(5f, 2f, 3f), el.transform.position);
    }

    [Test]
    public void SimulateMove_OmittedAxes_KeepCurrentValue()
    {
        var go = new GameObject("Board");
        go.transform.position = new Vector3(1f, 0f, 3f);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = "Board";
        e.DimensionsMM = new Vector3Int(800, 400, 18);
        go.AddComponent<Wall>();
        PartRegistry.Register(e);
        _spawned.Add(go);

        // Только z задан — x берётся текущий (1), а не 0.
        var resp = _handler.Handle(MakeReq("simulate_move", new { name = "Board", z = 2f }));

        Assert.AreEqual("result", resp.type);
        var sim = resp.data as SimulateResult;
        Assert.IsNotNull(sim);
        // simulatedAABB центрируется вокруг (1,0,2): проверяем, что X не уехал в 0.
        Assert.AreEqual(1f, (sim.simulatedAABB.minX + sim.simulatedAABB.maxX) * 0.5f, 0.001f,
            "X должен остаться текущим (1 м), а не сброситься в 0");
    }

    [Test]
    public void ResizeElement_OmittedDimensions_KeepCurrentSize()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        // Меняем только ширину — высота (400) и толщина (18) сохраняются.
        var resp = _handler.Handle(MakeReq("resize_element", new { name = "Board", width = 1000 }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3Int(1000, 400, 18), el.DimensionsMM);
    }

    [Test]
    public void RotateElement_OmittedAxes_KeepCurrentAngle()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.transform.eulerAngles = new Vector3(0f, 45f, 0f);

        // Задаём только X — Y должен остаться 45.
        var resp = _handler.Handle(MakeReq("rotate_element", new { name = "Board", x = 10f }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(45f, el.transform.eulerAngles.y, 0.01f, "Y-угол должен сохраниться");
    }

    // ── Конвертация типа + создание сборного фасада ─────────────────────

    [Test]
    public void ConvertElement_PartToAssembledFacade_ChangesTypeKeepsName()
    {
        var el = MakeElement("F1", new Vector3Int(600, 716, 18), new Vector3(1f, 0.5f, 2f));

        var resp = _handler.Handle(MakeReq("convert_element",
            new { name = "F1", target = "assembled_facade", fill = "glass" }));

        Assert.AreEqual("result", resp.type);
        // Компонент сменился на том же GameObject, имя/размер сохранены.
        var go = _spawned.Find(g => g != null && g.name == "F1");
        Assert.IsNotNull(go);
        var asm = go.GetComponent<AssembledFacadeElement>();
        Assert.IsNotNull(asm, "элемент должен стать сборным фасадом");
        Assert.AreEqual("F1", asm.PartName);
        Assert.AreEqual(AssembledFill.Glass, asm.Fill, "fill=glass должен примениться");
    }

    [Test]
    public void ConvertElement_UnknownTarget_ReturnsError()
    {
        MakeElement("F1", new Vector3Int(600, 716, 18), Vector3.zero);
        var resp = _handler.Handle(MakeReq("convert_element", new { name = "F1", target = "banana" }));
        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void ConvertElement_NotFound_ReturnsError()
    {
        var resp = _handler.Handle(MakeReq("convert_element", new { name = "Nope", target = "facade" }));
        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void ConvertElement_Locked_ReturnsError()
    {
        var el = MakeElement("F1", new Vector3Int(600, 716, 18), Vector3.zero);
        el.Movable = false;
        var resp = _handler.Handle(MakeReq("convert_element", new { name = "F1", target = "assembled_facade" }));
        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void CreateElement_Assembled_CreatesAssembledFacade_WithFill()
    {
        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "Asm1", x = 1f, y = 0.5f, z = 2f,
            width = 450, height = 700, depth = 18, is_assembled = true, fill = "open"
        }));

        Assert.AreEqual("result", resp.type);
        var el = FindBoard("Asm1");
        Assert.IsNotNull(el);
        var asm = el as AssembledFacadeElement;
        Assert.IsNotNull(asm, "is_assembled=true должен создать AssembledFacadeElement");
        Assert.AreEqual(AssembledFill.Open, asm.Fill);
        Assert.AreEqual(new Vector3Int(450, 700, 18), asm.DimensionsMM);
    }

    // ── Материалы / текстуры ────────────────────────────────────────────

    [Test]
    public void SetMaterial_ById_AppliesToElement()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler.Handle(MakeReq("set_material", new { name = "Board", material = "oak" }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual("oak", el.MaterialId);
    }

    [Test]
    public void SetMaterial_ByDisplayName_AppliesToElement()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler.Handle(MakeReq("set_material", new { name = "Board", material = "Венге" }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual("wenge", el.MaterialId);
    }

    [Test]
    public void SetMaterial_UnknownMaterial_ReturnsError_AndKeepsCurrent()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler.Handle(MakeReq("set_material", new { name = "Board", material = "no-such" }));

        Assert.AreEqual("error", resp.type);
        Assert.AreEqual(MaterialCatalog.DefaultId, el.MaterialId, "неизвестный материал не должен менять текущий");
    }

    [Test]
    public void SetMaterial_MissingName_ReturnsError()
    {
        var resp = _handler.Handle(MakeReq("set_material", new { material = "oak" }));
        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void SetMaterial_MissingMaterial_ReturnsError()
    {
        MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        var resp = _handler.Handle(MakeReq("set_material", new { name = "Board" }));
        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void SetMaterial_OnFacade_Applies()
    {
        _handler.Handle(MakeReq("create_element", new
        {
            template_name = "F", name = "F1", x = 0f, y = 0f, z = 0f,
            width = 400, height = 300, depth = 18, is_facade = true
        }));

        var resp = _handler.Handle(MakeReq("set_material", new { name = "F1", material = "concrete" }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual("concrete", FindBoard("F1").MaterialId);
    }

    [Test]
    public void ListMaterials_ReturnsCatalog()
    {
        var resp = _handler.Handle(MakeReq("list_materials", new { }));

        Assert.AreEqual("result", resp.type);
        var materials = GetProp<object>(resp.data, "materials") as System.Collections.IList;
        Assert.IsNotNull(materials);
        Assert.AreEqual(MaterialCatalog.All.Count, materials.Count);
        Assert.AreEqual(MaterialCatalog.DefaultId, GetProp<string>(resp.data, "defaultId"));
    }

    [Test]
    public void GetElementInfo_IncludesMaterialId()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.MaterialId = "oak";

        var resp = _handler.Handle(MakeReq("get_element_info", new { name = "Board" }));

        Assert.AreEqual("result", resp.type);
        var info = (ElementInfo)resp.data;
        Assert.AreEqual("oak", info.materialId);
    }

    [Test]
    public void SetMaterial_DynamicExternalDecor_Applies()
    {
        // Имитируем декор, подгруженный из внешней папки (ExternalTextureCatalog).
        MaterialCatalog.RegisterDynamic(
            new MaterialDef("abrikos_ba_03_cd_100_100", "abrikos ba 03 cd", "ЛДСП", Color.white, null, 100)
            { tileHeightMM = 100 });

        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        var resp = _handler.Handle(MakeReq("set_material",
            new { name = "Board", material = "abrikos_ba_03_cd_100_100" }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual("abrikos_ba_03_cd_100_100", el.MaterialId);
    }

    [Test]
    public void ListMaterials_IncludesDynamicDecors()
    {
        MaterialCatalog.RegisterDynamic(new MaterialDef("ext_a", "Ext A", "ЛДСП", Color.white));
        var resp = _handler.Handle(MakeReq("list_materials", new { }));

        Assert.AreEqual("result", resp.type);
        var materials = GetProp<object>(resp.data, "materials") as System.Collections.IList;
        Assert.AreEqual(MaterialCatalog.All.Count, materials.Count, "динамические декоры входят в список");
    }

    // ── Highlight refresh after mutation ────────────────────────────────

    private ElementHighlighter SetupHighlighter()
    {
        var go = new GameObject("ElementHighlighter");
        var hl = go.AddComponent<ElementHighlighter>();
        _spawned.Add(go);
        return hl;
    }

    [Test]
    public void MoveElement_RefreshesHighlights_AfterMutation()
    {
        var hl = SetupHighlighter();
        int before = hl.RefreshCount;
        MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler.Handle(MakeReq("move_element", new { name = "Board", x = 1f, y = 0f, z = 0f }));

        Assert.AreEqual("result", resp.type);
        Assert.Greater(hl.RefreshCount, before, "RefreshHighlights should be called after move");
    }

    [Test]
    public void ResizeElement_RefreshesHighlights_AfterMutation()
    {
        var hl = SetupHighlighter();
        int before = hl.RefreshCount;
        MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler.Handle(MakeReq("resize_element", new { name = "Board", width = 1200, height = 600, depth = 36 }));

        Assert.AreEqual("result", resp.type);
        Assert.Greater(hl.RefreshCount, before, "RefreshHighlights should be called after resize");
    }

    [Test]
    public void RotateElement_RefreshesHighlights_AfterMutation()
    {
        var hl = SetupHighlighter();
        int before = hl.RefreshCount;
        MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler.Handle(MakeReq("rotate_element", new { name = "Board", x = 0f, y = 90f, z = 0f }));

        Assert.AreEqual("result", resp.type);
        Assert.Greater(hl.RefreshCount, before, "RefreshHighlights should be called after rotate");
    }

    [Test]
    public void CreateElement_RefreshesHighlights_AfterMutation()
    {
        var hl = SetupHighlighter();
        int before = hl.RefreshCount;

        var resp = _handler.Handle(MakeReq("create_element", new
        {
            template_name = "NewBoard", width = 600, height = 400, depth = 18, x = 2f, y = 0f, z = 0f
        }));

        Assert.AreEqual("result", resp.type);
        Assert.Greater(hl.RefreshCount, before, "RefreshHighlights should be called after create");
    }

    [Test]
    public void DeleteElement_RefreshesHighlights_AfterMutation()
    {
        var hl = SetupHighlighter();
        MakeElement("Keep", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("Remove", new Vector3Int(500, 400, 18), new Vector3(1f, 0f, 0f));
        int before = hl.RefreshCount;

        var resp = _handler.Handle(MakeReq("delete_element", new { name = "Remove" }));

        Assert.AreEqual("result", resp.type);
        Assert.IsNull(FindBoard("Remove"));
        Assert.Greater(hl.RefreshCount, before, "RefreshHighlights should be called after delete");
    }

    [Test]
    public void Undo_RefreshesHighlights_AfterMutation()
    {
        var hl = SetupHighlighter();
        MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        _handler.Handle(MakeReq("move_element", new { name = "Board", x = 1f, y = 0f, z = 0f }));
        hl.RefreshCount = 0;

        var resp = _handler.Handle(MakeReq("undo", new { }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(Vector3.zero, FindBoard("Board").transform.position);
        Assert.Greater(hl.RefreshCount, 0, "RefreshHighlights should be called after undo");
    }

    [Test]
    public void Redo_RefreshesHighlights_AfterMutation()
    {
        var hl = SetupHighlighter();
        MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        _handler.Handle(MakeReq("move_element", new { name = "Board", x = 1f, y = 0f, z = 0f }));
        _handler.Handle(MakeReq("undo", new { }));
        hl.RefreshCount = 0;

        var resp = _handler.Handle(MakeReq("redo", new { }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3(1, 0, 0), FindBoard("Board").transform.position);
        Assert.Greater(hl.RefreshCount, 0, "RefreshHighlights should be called after redo");
    }

    [Test]
    public void ResizeFloor_RefreshesHighlights_AfterMutation()
    {
        var hl = SetupHighlighter();
        var plate = BasePlate.Create();
        plate.Element.PartName = "Floor";
        PartRegistry.Register(plate.Element);
        _spawned.Add(plate.gameObject);
        int before = hl.RefreshCount;

        var resp = _handler.Handle(MakeReq("resize_floor", new { width = 4000, height = 1, depth = 3000 }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3Int(4000, 1, 3000), plate.Element.DimensionsMM);
        Assert.Greater(hl.RefreshCount, before, "RefreshHighlights should be called after floor resize");
    }

    [Test]
    public void NonMutatingCommand_DoesNotCallRefreshHighlights()
    {
        var hl = SetupHighlighter();
        MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        int before = hl.RefreshCount;

        var resp = _handler.Handle(MakeReq("get_element_info", new { name = "Board" }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(before, hl.RefreshCount, "get_element_info is read-only — should not refresh highlights");
    }
}
