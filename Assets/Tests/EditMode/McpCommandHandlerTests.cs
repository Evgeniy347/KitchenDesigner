using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

public class McpCommandHandlerTests
{
    private McpCommandHandler? _handler;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        _handler = new McpCommandHandler();
        PartRegistry.Clear();
        ProjectInstructions.Reset();
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
        MaterialCatalog.Reset();
        ProjectInstructions.Reset();
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
    public void CreateElement_UsesName()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "TestBoard", width = 600, height = 400, depth = 18, x = 0f, y = 0f, z = 0f } }
        }));

        Assert.AreEqual("result", resp.type);
        var el = PartRegistry.GetAll().Find(e => e.PartName == "TestBoard");
        Assert.NotNull(el);
        Assert.AreEqual("TestBoard", el.PartName);
    }

    [Test]
    public void CreateFloor_MakesSizedFloorElement_NotBasePlate()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "RoomFloor", type = "floor", width = 3000, height = 18, depth = 4000, x = 0f, y = -0.009f, z = 0f } }
        }));

        Assert.AreEqual("result", resp.type);
        var el = PartRegistry.GetAll().Find(e => e.PartName == "RoomFloor");
        Assert.NotNull(el, "floor element created");
        Assert.IsInstanceOf<FloorElement>(el, "type floor → FloorElement (не BasePlate-синглтон)");
        Assert.AreEqual(3000, el!.DimensionsMM.x);
        Assert.AreEqual(4000, el.DimensionsMM.z);
    }

    [Test]
    public void CreateWindow_SnapsToNearbyWall_OnMcpCreate()
    {
        // Стена (N-S, тонкая по X) + окно рядом: MCP-создание должно привязать
        // окно к стене (раньше проём не резался — привязка была только мышью).
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "TestWall", type = "wall", width = 100, height = 2700, depth = 3000, x = 0f, y = 1.35f, z = 0f } }
        }));
        var resp = _handler.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "TestWin", type = "window", width = 900, height = 1200, depth = 100, x = 0f, y = 1.2f, z = 0f } }
        }));

        Assert.AreEqual("result", resp.type);
        var win = PartRegistry.GetAll().Find(e => e.PartName == "TestWin") as WindowElement;
        Assert.NotNull(win, "window created");
        Assert.AreEqual("TestWall", win!.AttachedWallName, "окно привязалось к стене при MCP-создании");
    }

    [Test]
    public void SetProjectInstructions_UpdatesHolder_AndGetReturnsIt()
    {
        const string text = "Несущие 250мм, перегородки 100мм. ЛДСП 16мм.";
        var setResp = _handler!.Handle(MakeReq("set_project_instructions", new { text }));
        Assert.AreEqual("result", setResp.type);
        Assert.AreEqual(text, ProjectInstructions.Text);

        var getResp = _handler.Handle(MakeReq("get_project_instructions", new { }));
        Assert.AreEqual("result", getResp.type);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(getResp.data);
        var jo = Newtonsoft.Json.Linq.JObject.Parse(json);
        Assert.AreEqual(text, jo["text"]!.ToString());
    }

    [Test]
    public void GetStatus_IncludesProjectInstructions()
    {
        ProjectInstructions.Text = "тест-инструкции";
        var resp = _handler!.Handle(MakeReq("get_status", new { }));
        Assert.AreEqual("result", resp.type);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(resp.data);
        var jo = Newtonsoft.Json.Linq.JObject.Parse(json);
        Assert.AreEqual("тест-инструкции", jo["projectInstructions"]!.ToString());
    }

    [Test]
    public void CreateElement_UsesCustomName()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "CustomName", width = 600, height = 400, depth = 18, x = 0f, y = 0f, z = 0f } }
        }));

        Assert.AreEqual("result", resp.type);
        var el = PartRegistry.GetAll().Find(e => e.PartName == "CustomName");
        Assert.NotNull(el);
        Assert.AreEqual("CustomName", el.PartName);
    }

    [Test]
    public void CreateElement_AddsWall_WhenTypeIsWall()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "WallBoard", width = 2000, height = 2500, depth = 100, x = 0f, y = 1.25f, z = 0f, type = "wall" } }
        }));

        Assert.AreEqual("result", resp.type);
        var el = PartRegistry.GetAll().Find(e => e.PartName == "WallBoard");
        Assert.NotNull(el);
        Assert.NotNull(el.GetComponent<Wall>(), "Wall component should be added when type=wall");
    }

    [Test]
    public void CreateElement_DoesNotAddWall_WhenTypeIsBoard()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "RegularBoard", width = 800, height = 400, depth = 18, x = 0f, y = 0f, z = 0f } }
        }));

        Assert.AreEqual("result", resp.type);
        var el = PartRegistry.GetAll().Find(e => e.PartName == "RegularBoard");
        Assert.NotNull(el);
        Assert.IsNull(el.GetComponent<Wall>(), "Wall component should NOT be added for default board");
    }

    [Test]
    public void CreateElement_DefaultsToBoard_WhenTypeOmitted()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "DefaultBoard", width = 800, height = 400, depth = 18, x = 0f, y = 0f, z = 0f } }
        }));

        Assert.AreEqual("result", resp.type);
        var el = PartRegistry.GetAll().Find(e => e.PartName == "DefaultBoard");
        Assert.NotNull(el);
        Assert.IsNull(el.GetComponent<Wall>(), "Wall component should NOT be added when type omitted");
    }

    [Test]
    public void ResizeElement_UsesWidthHeightDepth()
    {
        var el = MakeElement("TestBoard", new Vector3Int(800, 400, 18), Vector3.zero);
        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "TestBoard", width = 1200, height = 600, depth = 36 } }
        }));

        if (resp.type == "error")
            Assert.Fail("ResizeElement failed: data=" + (resp.data != null ? resp.data.GetType() + ": " + resp.data.ToString() : "null"));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3Int(1200, 600, 36), el.DimensionsMM);
    }

    [Test]
    public void ResizeElement_PrefersWidthOverDimX()
    {
        var el = MakeElement("TestBoard", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "TestBoard", width = 100, height = 200, depth = 300, dimX = 9999, dimY = 9999, dimZ = 9999 } }
        }));

        if (resp.type == "error")
            Assert.Fail("ResizeElement failed: data=" + (resp.data != null ? resp.data.GetType() + ": " + resp.data.ToString() : "null"));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3Int(100, 200, 300), el.DimensionsMM);
    }

    [Test]
    public void WallElement_CreatedViaMCP_IsAnchor_NotViolation()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "TestWall", width = 2000, height = 2500, depth = 100, x = 0f, y = 1.25f, z = 0f, type = "wall" } }
        }));

        var wall = PartRegistry.GetAll().Find(e => e.PartName == "TestWall");
        Assert.NotNull(wall);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { wall! });
        Assert.IsFalse(result.violations.Exists(e => e.GetComponent<Wall>() != null));
    }

    private static T GetProp<T>(object obj, string name)
    {
        var p = obj.GetType().GetProperty(name);
        Assert.NotNull(p, $"Property '{name}' not found on {obj.GetType()}");
        return (T)p.GetValue(obj, null);
    }

    private static ElementInfo EnvElement(McpResponse resp)
    {
        var jObj = Newtonsoft.Json.Linq.JObject.FromObject(resp.data!);
        var elements = jObj["elements"] as Newtonsoft.Json.Linq.JArray;
        Assert.NotNull(elements);
        Assert.Greater(elements!.Count, 0);
        return elements[0].ToObject<ElementInfo>()!;
    }

    private static Newtonsoft.Json.Linq.JObject EditResult(McpResponse resp)
    {
        var jObj = Newtonsoft.Json.Linq.JObject.FromObject(resp.data!);
        var results = jObj["results"] as Newtonsoft.Json.Linq.JArray;
        Assert.NotNull(results);
        Assert.Greater(results!.Count, 0);
        return (Newtonsoft.Json.Linq.JObject)results![0];
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
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "Wall2", width = 2000, height = 2500, depth = 100, x = 2.2f, y = 1.25f, z = 0f, type = "wall" } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.IsFalse(EnvElement(resp).hasViolations);
    }

    [Test]
    public void CreateElement_Response_HasViolations_WhenOverlapping()
    {
        MakeElement("Existing", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "Overlap", width = 600, height = 400, depth = 18, x = 0f, y = 0f, z = 0f } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.IsTrue(EnvElement(resp).hasViolations);
    }

    /// <summary>Раньше здесь ожидалось ОБРАТНОЕ: пересечение двух стен считалось
    /// штатным, потому что стены — якоря и «их ставит приложение». С блочными
    /// стенами планировки это допущение сломалось: блок въезжал в блок на десятки
    /// миллиметров, а сцена показывала ноль ошибок.</summary>
    [Test]
    public void CreateElement_Wall_Response_HasViolations_WhenOverlappingWithAnotherWall()
    {
        MakeWall("W1", new Vector3Int(2000, 2500, 100), Vector3.zero);
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "W2", width = 2000, height = 2500, depth = 100, x = 0f, y = 1.25f, z = 0f, type = "wall" } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.IsTrue(EnvElement(resp).hasViolations, "стена в стене — ошибка геометрии");
    }

    [Test]
    public void GetElementInfo_Response_HasViolations_WhenOverlapping()
    {
        MakeElement("A", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        MakeElement("B", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "A" } }));

        Assert.AreEqual("result", resp.type);
        var jObj = Newtonsoft.Json.Linq.JObject.FromObject(resp.data!);
        var info = (jObj["elements"] as Newtonsoft.Json.Linq.JArray)![0].ToObject<ElementInfo>()!;
        Assert.IsTrue(info.hasViolations, "A overlaps with B → violation");
    }

    [Test]
    public void GetElementInfo_Wall_Response_HasNoViolations()
    {
        MakeWall("W", new Vector3Int(2000, 2500, 100), Vector3.zero);
        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "W" } }));

        Assert.AreEqual("result", resp.type);
        var jObj = Newtonsoft.Json.Linq.JObject.FromObject(resp.data!);
        var info = (jObj["elements"] as Newtonsoft.Json.Linq.JArray)![0].ToObject<ElementInfo>()!;
        Assert.IsFalse(info.hasViolations, "Стена — якорь → нет нарушений");
    }

    [Test]
    public void GetAllElements_Response_ContainsHasViolations()
    {
        MakeElement("A", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        MakeElement("B", new Vector3Int(1000, 1000, 1000), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("get_all_elements", new { }));

        Assert.AreEqual("result", resp.type);
        var list = (List<ElementInfo>)resp.data!;
        Assert.AreEqual(2, list.Count);
        foreach (var info in list)
            Assert.IsTrue(info.hasViolations, $"{info.name} overlaps → violation");
    }

    [Test]
    public void MoveElement_Response_HasViolations_WhenOverlapping()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));
        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "A", x = 1.8f, y = 0f, z = 0f } }
        }));

        Assert.AreEqual("result", resp.type);
        var r = EditResult(resp);
        var viol = r["violations"] as Newtonsoft.Json.Linq.JArray;
        Assert.IsNotNull(viol);
        Assert.Greater(viol!.Count, 0);
    }

    [Test]
    public void ResizeElement_Response_HasViolations_WhenOverlapping()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(0.45f, 0f, 0f));
        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "A", width = 1000, height = 400, depth = 18 } }
        }));

        Assert.AreEqual("result", resp.type);
        var r = EditResult(resp);
        var viol = r["violations"] as Newtonsoft.Json.Linq.JArray;
        Assert.IsNotNull(viol);
        Assert.Greater(viol!.Count, 0);
    }

    [Test]
    public void GetViolations_ReturnsOverlappingElements()
    {
        MakeElement("A", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        MakeElement("B", new Vector3Int(1000, 1000, 1000), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("get_violations", new { }));

        Assert.AreEqual("result", resp.type);
        var count = GetProp<int>(resp.data!, "count");
        Assert.AreEqual(2, count);
        var violations = GetProp<object>(resp.data!, "violations") as System.Collections.IList;
        Assert.IsNotNull(violations);
        Assert.AreEqual(2, violations!.Count);
    }

    [Test]
    public void GetViolations_ReturnsOverlapDetails()
    {
        MakeElement("A", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        MakeElement("B", new Vector3Int(1000, 1000, 1000), new Vector3(0f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("get_violations", new { }));
        var violations = GetProp<object>(resp.data!, "violations") as System.Collections.IList;

        var vA = Newtonsoft.Json.Linq.JObject.FromObject(violations![0]!);
        Assert.AreEqual("A", vA.Value<string>("name"));
        Assert.IsFalse(vA.Value<bool>("disconnected"));
        var overlapsA = vA["overlapsWith"] as Newtonsoft.Json.Linq.JArray;
        Assert.IsNotNull(overlapsA);
        Assert.AreEqual(1, overlapsA!.Count);
        var overlap = Newtonsoft.Json.Linq.JObject.FromObject(overlapsA![0]!);
        Assert.AreEqual("B", overlap.Value<string>("neighbor"));
        Assert.Greater(overlap.Value<float>("overlapXmm"), 990);
        Assert.Greater(overlap.Value<float>("overlapYmm"), 990);
        Assert.Greater(overlap.Value<float>("overlapZmm"), 990);
    }

    [Test]
    public void GetViolations_ReturnsEmpty_WhenNoOverlaps()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(0.5f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("get_violations", new { }));

        Assert.AreEqual("result", resp.type);
        var count = GetProp<int>(resp.data!, "count");
        Assert.AreEqual(0, count);
    }

    [Test]
    public void CreateFacade_ReturnsOk()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "F1", x = 0f, y = 0f, z = 0f,
                width = 400, height = 300, depth = 18, type = "facade" } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual("FacadeElement", EnvElement(resp).type);
        var el = FindBoard("F1");
        Assert.IsNotNull(el);
        var facade = el!.GetComponent<FacadeElement>();
        Assert.IsNotNull(facade);
    }

    [Test]
    public void CreateFacade_WithGaps_AppliesGaps()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "F1", x = 0f, y = 0f, z = 0f,
                width = 400, height = 300, depth = 18, type = "facade" } }
        }));
        Assert.AreEqual("result", resp.type);

        var editResp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "F1", gap_left = 1, gap_right = 2, gap_top = 3, gap_bottom = 4 } }
        }));
        Assert.AreEqual("result", editResp.type);

        var el = FindBoard("F1");
        var facade = el!.GetComponent<FacadeElement>();
        Assert.IsNotNull(facade);
        Assert.AreEqual(1, facade.GapLeft);
        Assert.AreEqual(2, facade.GapRight);
        Assert.AreEqual(3, facade.GapTop);
        Assert.AreEqual(4, facade.GapBottom);
        Assert.AreEqual(new Vector3Int(400, 300, 18), facade.DimensionsMM);
    }

    [Test]
    public void CreateFacade_SetGaps_ViaEditElements()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "F2", x = 0f, y = 0f, z = 0f,
                width = 400, height = 300, depth = 18, type = "facade" } }
        }));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "F2", gap_left = 1, gap_right = 2, gap_top = 3, gap_bottom = 4 } }
        }));
        Assert.AreEqual("result", resp.type);
        var facade = FindBoard("F2")!.GetComponent<FacadeElement>();
        Assert.AreEqual(1, facade.GapLeft);
        Assert.AreEqual(2, facade.GapRight);
        Assert.AreEqual(3, facade.GapTop);
        Assert.AreEqual(4, facade.GapBottom);
    }

    [Test]
    public void CreateRadialShelf_UsesProvidedDimensionsAndCornerRadius()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "RS1", x = 0.5f, y = 0.1f, z = -1f,
                width = 600, height = 18, depth = 400, type = "radial_shelf" } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual("RadialShelfElement", EnvElement(resp).type);

        _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "RS1", corner_radius = 200 } }
        }));

        var el = FindBoard("RS1");
        Assert.IsNotNull(el);
        var shelf = el!.GetComponent<RadialShelfElement>();
        Assert.IsNotNull(shelf);
        Assert.AreEqual(200, shelf.CornerRadius);
        Assert.AreEqual(new Vector3Int(600, 18, 400), shelf.DimensionsMM);
    }

    [Test]
    public void CreateRadialShelf_Defaults_WhenNotProvided()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "RS2", x = 0f, y = 0f, z = 0f, type = "radial_shelf" } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual("RadialShelfElement", EnvElement(resp).type);
        var el = FindBoard("RS2");
        Assert.IsNotNull(el);
        var shelf = el!.GetComponent<RadialShelfElement>();
        Assert.AreEqual(new Vector3Int(600, 18, 400), shelf.DimensionsMM);
        Assert.AreEqual(200, shelf.CornerRadius);
    }

    [Test]
    public void SetRadialShelfProperties_ChangesCornerRadius()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "RS4", x = 0f, y = 0f, z = 0f,
                width = 600, height = 18, depth = 400, type = "radial_shelf" } }
        }));
        _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "RS4", corner_radius = 200 } }
        }));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "RS4", corner_radius = 120 } }
        }));

        Assert.AreEqual("result", resp.type);
        var el = FindBoard("RS4");
        Assert.AreEqual(120, el!.GetComponent<RadialShelfElement>().CornerRadius);
    }

    [Test]
    public void CreateStool_Defaults_ToTheAgreedSizeAndASquareSeat()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "ST1", x = 0f, y = 0f, z = 0f, type = "stool" } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual("StoolElement", EnvElement(resp).type,
            "тип, которого нет в реестре спаунеров, уходит в SpawnPlainCube и молча "
            + "возвращает обычную доску");

        var stool = FindBoard("ST1")!.GetComponent<StoolElement>();
        Assert.AreEqual(new Vector3Int(360, 450, 360), stool.DimensionsMM);
        Assert.AreEqual(0, stool.CornerRadiusMM,
            "по умолчанию табуретка квадратная; круглую агент получает через corner_radius");
    }

    [Test]
    public void SetStoolCornerRadius_MakesItRound_AndReportsTheShape()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "ST2", x = 0f, y = 0f, z = 0f, type = "stool" } }
        }));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "ST2", corner_radius = 180 } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(180, FindBoard("ST2")!.GetComponent<StoolElement>().CornerRadiusMM);

        var info = EnvElement(_handler!.Handle(MakeReq("get_elements", new { names = new[] { "ST2" } })));
        Assert.IsNotNull(info.stool,
            "у табуретки обязан быть свой под-объект в ответе: без него агент не увидит "
            + "ни формы, ни декоров сиденья");
        Assert.AreEqual("round", info.stool!.shape,
            "форма — производная от радиуса, и агент читает её из ответа, а не пересчитывает");
        Assert.AreEqual(180, info.cornerRadius,
            "общее поле cornerRadius обязано отвечать и за табуретку: агент правит её тем же "
            + "полем corner_radius, что и радиусную полку");
    }

    [Test]
    public void SetStoolCornerRadius_ToZero_IsAccepted_NotRejectedAsBelowTheMinimum()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "ST3", x = 0f, y = 0f, z = 0f, type = "stool" } }
        }));
        _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "ST3", corner_radius = 180 } }
        }));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "ST3", corner_radius = 0 } }
        }));

        Assert.AreEqual("result", resp.type,
            "у радиусной полки нижняя граница corner_radius равна 1, у табуретки — 0: "
            + "квадратная табуретка законна, и схема обязана её пропускать");
        Assert.AreEqual(0, FindBoard("ST3")!.GetComponent<StoolElement>().CornerRadiusMM);
    }

    [Test]
    public void SetStoolLegInset_IsRejected_WhileTheSeatAndLegsDecorsAreAccepted()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "ST4", x = 0f, y = 0f, z = 0f, type = "stool" } }
        }));

        var rejected = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "ST4", leg_inset_mm = 50 } }
        }));
        Assert.AreEqual("error", rejected.type,
            "отступ ножек у табуретки — константа конструкции, а не параметр; принять его "
            + "молча значило бы вернуть успех на то, что никуда не применилось");

        var accepted = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "ST4", tabletop_material = MaterialCatalog.DefaultId } }
        }));
        Assert.AreEqual("result", accepted.type,
            "декоры сиденья и ножек у табуретки есть — она носитель ITabletop, и правило "
            + "для них разведено с правилом для leg_inset_mm");
    }

    [Test]
    public void SetRadialShelfProperties_Errors_WhenNotRadialShelf()
    {
        MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", corner_radius = 120 } }
        }));

        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void ConvertElement_PartToRadialShelf_ChangesType()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "Part1", x = 0f, y = 0f, z = 0f,
                width = 400, height = 18, depth = 300 } }
        }));

        var resp = _handler!.Handle(MakeReq("convert_elements", new
        {
            ops = new[] { new { name = "Part1", target = "radial_shelf" } }
        }));

        Assert.AreEqual("result", resp.type);
        var el = FindBoard("Part1");
        Assert.IsNotNull(el);
        Assert.IsNotNull(el!.GetComponent<RadialShelfElement>());
        Assert.AreEqual(new Vector3Int(400, 18, 300), el!.GetComponent<KitchenElement>().DimensionsMM);
        Assert.AreEqual(200, el!.GetComponent<RadialShelfElement>().CornerRadius);
    }

    [Test]
    public void SetElementLock_LocksElement()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        Assert.IsTrue(el.Movable);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", locked = true } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.IsFalse(el.Movable);
    }

    [Test]
    public void SetElementLock_UnlocksElement()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.Movable = false;

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", locked = false } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.IsTrue(el.Movable);
    }

    [Test]
    public void SetElementLock_Errors_WhenNameMissing()
    {
        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { locked = true } }
        }));
        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void SetElementLock_Errors_WhenElementNotFound()
    {
        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "NonExistent", locked = true } }
        }));
        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void MoveElement_Errors_WhenLocked()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.Movable = false;

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", x = 1f, y = 0f, z = 0f } }
        }));

        Assert.AreEqual("error", resp.type);
        Assert.AreEqual(Vector3.zero, el.transform.position);
    }

    [Test]
    public void ResizeElement_Errors_WhenLocked()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.Movable = false;

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", width = 1200, height = 600, depth = 36 } }
        }));

        Assert.AreEqual("error", resp.type);
        Assert.AreEqual(new Vector3Int(800, 400, 18), el.DimensionsMM);
    }

    [Test]
    public void RotateElement_Errors_WhenLocked()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.Movable = false;

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", rot_y = 90 } }
        }));

        Assert.AreEqual("error", resp.type);
        Assert.AreEqual(Quaternion.identity, el.transform.rotation);
    }

    [Test]
    public void DeleteElement_Errors_WhenLocked()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.Movable = false;

        var resp = _handler!.Handle(MakeReq("delete_elements", new { names = new[] { "Board" } }));

        Assert.AreEqual("error", resp.type);
        Assert.IsNotNull(FindBoard("Board"), "Element should not be deleted");
    }

    [Test]
    public void LockedElement_CanBeUnlockedAndMoved()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.Movable = false;

        var unlockResp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", locked = false } }
        }));
        Assert.AreEqual("result", unlockResp.type);
        Assert.IsTrue(el.Movable);

        var moveResp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", x = 1f, y = 0f, z = 0f } }
        }));
        Assert.AreEqual("result", moveResp.type);
        Assert.AreEqual(new Vector3(1, 0, 0), el.transform.position);
    }

    // ── Params object format tests ──────────────────────────────────────

    [Test]
    public void ParamsObject_CreateElement_WorksIdenticalToStringFormat()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "ObjBoard", width = 600, height = 400, depth = 18, x = 0f, y = 0f, z = 0f } }
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

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", x = 2.5f, y = 1.0f, z = 0.5f } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3(2.5f, 1f, 0.5f), el.transform.position);
    }

    [Test]
    public void ParamsObject_ResizeElement_PrefersWidthOverDimX()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", width = 500, height = 600, depth = 36,
                dimX = 9999, dimY = 9999, dimZ = 9999 } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3Int(500, 600, 36), el.DimensionsMM);
    }

    [Test]
    public void ParamsObject_Serialization_DoesNotEscape()
    {
        var data = new { ops = new[] { new { name = "TestBoard", x = 1.5, y = 0f, z = 0f } } };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(
            new { id = "test", method = "edit_elements", Params = data });

        var parsed = Newtonsoft.Json.Linq.JObject.Parse(json);
        var p = parsed["Params"];
        Assert.IsNotNull(p);
        Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Object, p!.Type,
            "Params должен быть JSON-объектом, не строкой");
    }

    [Test]
    public void McpRequest_DeserializesParamsAsObject_NotString()
    {
        var json = @"{""id"":""req-1"",""method"":""edit_elements"",""params"":{""ops"":[{""name"":""Board1"",""x"":1.5,""y"":0,""z"":0}]}}";
        var req = Newtonsoft.Json.JsonConvert.DeserializeObject<McpRequest>(json);

        Assert.AreEqual("req-1", req!.id);
        Assert.AreEqual("edit_elements", req.method);
        Assert.IsNotNull(req.Params, "Params должен быть десериализован из JSON-объекта");
    }

    [Test]
    public void ParamsObject_GetElementInfo_ReturnsElementInfo()
    {
        MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "Board" } }));

        Assert.AreEqual("result", resp.type);
        var jObj = Newtonsoft.Json.Linq.JObject.FromObject(resp.data!);
        var info = (jObj["elements"] as Newtonsoft.Json.Linq.JArray)![0].ToObject<ElementInfo>()!;
        Assert.IsNotNull(info);
        Assert.AreEqual("Board", info.name);
        Assert.AreEqual(800, info.dimX);
    }

    [Test]
    public void ParamsObject_EmptyObject_ReturnsError()
    {
        var resp = _handler!.Handle(MakeReq("edit_elements", new { }));

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

        var resp = _handler!.Handle(req);
        Assert.AreEqual("result", resp.type);
    }

    [Test]
    public void GetElements_ReturnsEtag()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        var resp = _handler!.Handle(MakeReq("get_all_elements", new { }));
        Assert.AreEqual("result", resp.type);
        Assert.IsNotEmpty(resp.etag, "etag should be present");
    }

    [Test]
    public void GetElements_NotModified_WhenEtagMatches()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        var first = _handler!.Handle(MakeReq("get_all_elements", new { }));
        Assert.AreEqual("result", first.type);
        var etag = first.etag;

        var req = MakeReq("get_all_elements", new { });
        req.Headers = new Dictionary<string, string> { { "If-None-Match", etag! } };
        var second = _handler!.Handle(req);
        Assert.AreEqual("not_modified", second.type);
        Assert.AreEqual(etag, second.etag);
    }

    [Test]
    public void GetElements_NotModified_WithoutHeaders_ReturnsResult()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        var first = _handler!.Handle(MakeReq("get_all_elements", new { }));
        var second = _handler!.Handle(MakeReq("get_all_elements", new { }));
        Assert.AreEqual("result", second.type);
        Assert.AreEqual(first.etag, second.etag);
    }

    [Test]
    public void GetElements_ReturnsNewResult_AfterElementChange()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        var first = _handler!.Handle(MakeReq("get_all_elements", new { }));
        var etag = first.etag;

        MakeElement("B", new Vector3Int(600, 500, 18), Vector3.zero);

        var req = MakeReq("get_all_elements", new { });
        req.Headers = new Dictionary<string, string> { { "If-None-Match", etag! } };
        var second = _handler!.Handle(req);
        Assert.AreEqual("result", second.type);
        Assert.AreNotEqual(etag, second.etag, "etag changed after adding element");
    }

    private static KitchenElement? FindBoard(string name)
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

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", x = 5f } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3(5f, 2f, 3f), el.transform.position);
    }

    [Test]
    public void ResizeElement_OmittedDimensions_KeepCurrentSize()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", width = 1000 } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3Int(1000, 400, 18), el.DimensionsMM);
    }

    [Test]
    public void RotateElement_OmittedAxes_KeepCurrentAngle()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.transform.eulerAngles = new Vector3(0f, 45f, 0f);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", rot_x = 10 } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(45f, el.transform.eulerAngles.y, 0.01f, "Y-угол должен сохраниться");
    }

    // ── Конвертация типа + создание сборного фасада ─────────────────────

    [Test]
    public void ConvertElement_PartToAssembledFacade_ChangesTypeKeepsName()
    {
        var el = MakeElement("F1", new Vector3Int(600, 716, 18), new Vector3(1f, 0.5f, 2f));

        var resp = _handler!.Handle(MakeReq("convert_elements",
            new { ops = new[] { new { name = "F1", target = "assembled_facade", fill = "glass" } } }));

        Assert.AreEqual("result", resp.type);
        var go = _spawned.Find(g => g != null && g.name == "F1");
        Assert.IsNotNull(go);
        var asm = go!.GetComponent<AssembledFacadeElement>();
        Assert.IsNotNull(asm, "элемент должен стать сборным фасадом");
        Assert.AreEqual("F1", asm!.PartName);
        Assert.AreEqual(AssembledFill.Glass, asm!.Fill, "fill=glass должен примениться");
    }

    [Test]
    public void ConvertElement_UnknownTarget_ReturnsError()
    {
        MakeElement("F1", new Vector3Int(600, 716, 18), Vector3.zero);
        var resp = _handler!.Handle(MakeReq("convert_elements",
            new { ops = new[] { new { name = "F1", target = "banana" } } }));
        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void ConvertElement_NotFound_ReturnsError()
    {
        var resp = _handler!.Handle(MakeReq("convert_elements",
            new { ops = new[] { new { name = "Nope", target = "facade" } } }));
        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void ConvertElement_Locked_ReturnsError()
    {
        var el = MakeElement("F1", new Vector3Int(600, 716, 18), Vector3.zero);
        el.Movable = false;
        var resp = _handler!.Handle(MakeReq("convert_elements",
            new { ops = new[] { new { name = "F1", target = "assembled_facade" } } }));
        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void CreateElement_Assembled_CreatesAssembledFacade_WithFill()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "Asm1", x = 1f, y = 0.5f, z = 2f,
                width = 450, height = 700, depth = 18, type = "assembled_facade" } }
        }));

        Assert.AreEqual("result", resp.type);
        var el = FindBoard("Asm1");
        Assert.IsNotNull(el);
        var asm = el! as AssembledFacadeElement;
        Assert.IsNotNull(asm, "type=assembled_facade должен создать AssembledFacadeElement");
        Assert.AreEqual(new Vector3Int(450, 700, 18), asm!.DimensionsMM);

        var editResp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Asm1", fill = "open" } }
        }));
        Assert.AreEqual("result", editResp.type);
        Assert.AreEqual(AssembledFill.Open, asm!.Fill);
    }

    // ── Материалы / текстуры ────────────────────────────────────────────

    [Test]
    public void SetMaterial_ById_AppliesToElement()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", material = "oak" } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual("oak", el.MaterialId);
    }

    [Test]
    public void SetMaterial_ByDisplayName_AppliesToElement()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", material = "Венге" } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual("wenge", el.MaterialId);
    }

    [Test]
    public void SetMaterial_UnknownMaterial_ReturnsError_AndKeepsCurrent()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", material = "no-such" } }
        }));

        Assert.AreEqual("error", resp.type);
        Assert.AreEqual(MaterialCatalog.DefaultId, el.MaterialId, "неизвестный материал не должен менять текущий");
    }

    [Test]
    public void SetMaterial_MissingName_ReturnsError()
    {
        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { material = "oak" } }
        }));
        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void SetMaterial_OnFacade_Applies()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "F1", x = 0f, y = 0f, z = 0f,
                width = 400, height = 300, depth = 18, type = "facade" } }
        }));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "F1", material = "concrete" } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual("concrete", FindBoard("F1")!.MaterialId);
    }

    [Test]
    public void ListMaterials_ReturnsCatalog()
    {
        var resp = _handler!.Handle(MakeReq("list_materials", new { }));

        Assert.AreEqual("result", resp.type);
        var materials = GetProp<object>(resp.data!, "materials") as System.Collections.IList;
        Assert.IsNotNull(materials);
        Assert.AreEqual(MaterialCatalog.All.Count, materials!.Count);
        Assert.AreEqual(MaterialCatalog.DefaultId, GetProp<string>(resp.data!, "defaultId"));
    }

    [Test]
    public void GetElementInfo_IncludesMaterialId()
    {
        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        el.MaterialId = "oak";

        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "Board" } }));

        Assert.AreEqual("result", resp.type);
        var jObj = Newtonsoft.Json.Linq.JObject.FromObject(resp.data!);
        var info = (jObj["elements"] as Newtonsoft.Json.Linq.JArray)![0].ToObject<ElementInfo>()!;
        Assert.AreEqual("oak", info.materialId);
    }

    [Test]
    public void SetMaterial_DynamicExternalDecor_Applies()
    {
        MaterialCatalog.Register(
            new MaterialDef("abrikos_ba_03_cd_100_100", "abrikos ba 03 cd", "ЛДСП", Color.white, null, 100)
            { tileHeightMM = 100 });

        var el = MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        var resp = _handler!.Handle(MakeReq("edit_elements",
            new { ops = new[] { new { name = "Board", material = "abrikos_ba_03_cd_100_100" } } }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual("abrikos_ba_03_cd_100_100", el.MaterialId);
    }

    [Test]
    public void ListMaterials_ReportsTextureFileAndLoadState()
    {
        // Агент по этим полям решает, годится ли декор. Картинки грузятся лениво,
        // поэтому hasTexture=true при textureLoaded=false — норма, а не сбой.
        MaterialCatalog.Register(new MaterialDef("mcp_tex", "Tex", "ЛДСП", Color.white, "some.png", 800)
            { tileHeightMM = 800, textureState = TextureState.Loading });
        MaterialCatalog.Register(new MaterialDef("mcp_plain", "Plain", "ЛДСП", Color.gray));

        var resp = _handler!.Handle(MakeReq("list_materials", new { }));

        Assert.AreEqual("result", resp.type);
        var jObj = Newtonsoft.Json.Linq.JObject.FromObject(resp.data!);
        var arr = (Newtonsoft.Json.Linq.JArray)jObj["materials"]!;
        Newtonsoft.Json.Linq.JToken Find(string id)
        {
            foreach (var t in arr) if ((string?)t["id"] == id) return t;
            Assert.Fail($"декор '{id}' не попал в list_materials");
            return null!;
        }

        var tex = Find("mcp_tex");
        Assert.IsTrue((bool)tex["hasTexture"]!, "у декора задан файл картинки");
        Assert.IsFalse((bool)tex["textureLoaded"]!, "но она ещё не прочитана");

        var plain = Find("mcp_plain");
        Assert.IsFalse((bool)plain["hasTexture"]!, "чисто цветовой декор картинки не имеет");
        Assert.IsFalse((bool)plain["textureLoaded"]!);
    }

    [Test]
    public void ListMaterials_IncludesDynamicDecors()
    {
        MaterialCatalog.Register(new MaterialDef("ext_a", "Ext A", "ЛДСП", Color.white));
        var resp = _handler!.Handle(MakeReq("list_materials", new { }));

        Assert.AreEqual("result", resp.type);
        var materials = GetProp<object>(resp.data!, "materials") as System.Collections.IList;
        Assert.AreEqual(MaterialCatalog.All.Count, materials!.Count, "динамические декоры входят в список");
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

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", x = 1f, y = 0f, z = 0f } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.Greater(hl.RefreshCount, before, "RefreshHighlights should be called after move");
    }

    [Test]
    public void ResizeElement_RefreshesHighlights_AfterMutation()
    {
        var hl = SetupHighlighter();
        int before = hl.RefreshCount;
        MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", width = 1200, height = 600, depth = 36 } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.Greater(hl.RefreshCount, before, "RefreshHighlights should be called after resize");
    }

    [Test]
    public void RotateElement_RefreshesHighlights_AfterMutation()
    {
        var hl = SetupHighlighter();
        int before = hl.RefreshCount;
        MakeElement("Board", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new[] { new { name = "Board", rot_y = 90 } }
        }));

        Assert.AreEqual("result", resp.type);
        Assert.Greater(hl.RefreshCount, before, "RefreshHighlights should be called after rotate");
    }

    [Test]
    public void CreateElement_RefreshesHighlights_AfterMutation()
    {
        var hl = SetupHighlighter();
        int before = hl.RefreshCount;

        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name = "NewBoard", width = 600, height = 400, depth = 18, x = 2f, y = 0f, z = 0f } }
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

        var resp = _handler!.Handle(MakeReq("delete_elements", new { names = new[] { "Remove" } }));

        Assert.AreEqual("result", resp.type);
        Assert.IsNull(FindBoard("Remove"));
        Assert.Greater(hl.RefreshCount, before, "RefreshHighlights should be called after delete");
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

        var resp = _handler!.Handle(MakeReq("resize_floor", new { width = 4000, height = 1, depth = 3000 }));

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

        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "Board" } }));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(before, hl.RefreshCount, "get_elements is read-only — should not refresh highlights");
    }
}
