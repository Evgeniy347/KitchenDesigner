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

    private McpRequest MakeReq(string method, string paramsRaw)
    {
        return new McpRequest { id = "test", method = method, parameters = paramsRaw };
    }

    private KitchenElement MakeElement(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.BoardName = name;
        e.DimensionsMM = dims;
        BoardRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [Test]
    public void CreateElement_UsesTemplateName_WhenNameNotGiven()
    {
        var resp = _handler.Handle(MakeReq("create_element",
            @"{""template_name"":""TestBoard"",""width"":600,""height"":400,""depth"":18,""x"":0,""y"":0,""z"":0}"));

        Assert.AreEqual("result", resp.type);
        var el = BoardRegistry.GetAll().Find(e => e.BoardName == "TestBoard");
        Assert.NotNull(el);
        Assert.AreEqual("TestBoard", el.BoardName);
    }

    [Test]
    public void CreateElement_UsesName_WhenProvided()
    {
        var resp = _handler.Handle(MakeReq("create_element",
            @"{""template_name"":""tmpl"",""name"":""CustomName"",""width"":600,""height"":400,""depth"":18,""x"":0,""y"":0,""z"":0}"));

        Assert.AreEqual("result", resp.type);
        var el = BoardRegistry.GetAll().Find(e => e.BoardName == "CustomName");
        Assert.NotNull(el);
        Assert.AreEqual("CustomName", el.BoardName);
    }

    [Test]
    public void CreateElement_AddsWall_WhenIsWallTrue()
    {
        var resp = _handler.Handle(MakeReq("create_element",
            @"{""template_name"":""WallBoard"",""width"":2000,""height"":2500,""depth"":100,""x"":0,""y"":1.25,""z"":0,""is_wall"":true}"));

        Assert.AreEqual("result", resp.type);
        var el = BoardRegistry.GetAll().Find(e => e.BoardName == "WallBoard");
        Assert.NotNull(el);
        Assert.NotNull(el.GetComponent<Wall>(), "Wall component should be added when is_wall=true");
    }

    [Test]
    public void CreateElement_DoesNotAddWall_WhenIsWallFalse()
    {
        var resp = _handler.Handle(MakeReq("create_element",
            @"{""template_name"":""RegularBoard"",""width"":800,""height"":400,""depth"":18,""x"":0,""y"":0,""z"":0,""is_wall"":false}"));

        Assert.AreEqual("result", resp.type);
        var el = BoardRegistry.GetAll().Find(e => e.BoardName == "RegularBoard");
        Assert.NotNull(el);
        Assert.IsNull(el.GetComponent<Wall>(), "Wall component should NOT be added when is_wall=false");
    }

    [Test]
    public void CreateElement_DefaultsIsWallFalse_WhenOmitted()
    {
        var resp = _handler.Handle(MakeReq("create_element",
            @"{""template_name"":""DefaultBoard"",""width"":800,""height"":400,""depth"":18,""x"":0,""y"":0,""z"":0}"));

        Assert.AreEqual("result", resp.type);
        var el = BoardRegistry.GetAll().Find(e => e.BoardName == "DefaultBoard");
        Assert.NotNull(el);
        Assert.IsNull(el.GetComponent<Wall>(), "Wall component should NOT be added when is_wall omitted");
    }

    [Test]
    public void ResizeElement_UsesWidthHeightDepth()
    {
        var el = MakeElement("TestBoard", new Vector3Int(800, 400, 18), Vector3.zero);
        var resp = _handler.Handle(MakeReq("resize_element",
            @"{""name"":""TestBoard"",""width"":1200,""height"":600,""depth"":36}"));

        if (resp.type == "error")
            Assert.Fail("ResizeElement failed: data=" + (resp.data != null ? resp.data.GetType() + ": " + resp.data.ToString() : "null"));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3Int(1200, 600, 36), el.DimensionsMM);
    }

    [Test]
    public void ResizeElement_UsesDimXDimYDimZ_AsAliases()
    {
        var el = MakeElement("TestBoard", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler.Handle(MakeReq("resize_element",
            @"{""name"":""TestBoard"",""dimX"":3170,""dimY"":2500,""dimZ"":7240}"));

        if (resp.type == "error")
            Assert.Fail("ResizeElement failed: data=" + (resp.data != null ? resp.data.GetType() + ": " + resp.data.ToString() : "null"));

        Assert.AreEqual("result", resp.type);
        Assert.AreEqual(new Vector3Int(3170, 2500, 7240), el.DimensionsMM);
    }

    [Test]
    public void ResizeElement_PrefersWidthOverDimX()
    {
        var el = MakeElement("TestBoard", new Vector3Int(800, 400, 18), Vector3.zero);

        var resp = _handler.Handle(MakeReq("resize_element",
            @"{""name"":""TestBoard"",""width"":100,""height"":200,""depth"":300,""dimX"":9999,""dimY"":9999,""dimZ"":9999}"));

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

        var resp = _handler.Handle(MakeReq("add_wall_component",
            @"{""name"":""TestBoard""}"));

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

        var resp = _handler.Handle(MakeReq("add_wall_component",
            @"{""name"":""TestBoard""}"));

        if (resp.type == "error")
            Assert.Fail("AddWallComponent failed: data=" + (resp.data != null ? resp.data.GetType() + ": " + resp.data.ToString() : "null"));

        Assert.AreEqual("result", resp.type);
        Assert.NotNull(el.GetComponent<Wall>());
    }

    [Test]
    public void AddWallComponent_Errors_WhenElementNotFound()
    {
        var resp = _handler.Handle(MakeReq("add_wall_component",
            @"{""name"":""NonExistent""}"));

        Assert.AreEqual("error", resp.type);
    }

    [Test]
    public void WallElement_CreatedViaMCP_IsAnchor_NotViolation()
    {
        _handler.Handle(MakeReq("create_element",
            @"{""template_name"":""TestWall"",""width"":2000,""height"":2500,""depth"":100,""x"":0,""y"":1.25,""z"":0,""is_wall"":true}"));

        var wall = BoardRegistry.GetAll().Find(e => e.BoardName == "TestWall");
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

    // ── hasViolations ────────────────────────────────────────────────────

    /// <summary>Создаёт стену (якорь графа связности).</summary>
    private KitchenElement MakeWall(string name, Vector3Int dims, Vector3 pos)
    {
        var el = MakeElement(name, dims, pos);
        el.gameObject.AddComponent<Wall>();
        return el;
    }

    [Test]
    public void CreateElement_Wall_Response_HasNoViolations()
    {
        // Стена — якорь, всегда без нарушений.
        MakeWall("Existing", new Vector3Int(2000, 2500, 100), Vector3.zero);
        var resp = _handler.Handle(MakeReq("create_element",
            @"{""template_name"":""Wall2"",""width"":2000,""height"":2500,""depth"":100,""x"":2.2,""y"":1.25,""z"":0,""is_wall"":true}"));

        Assert.AreEqual("result", resp.type);
        Assert.IsFalse(GetProp<bool>(resp.data, "hasViolations"));
    }

    [Test]
    public void CreateElement_Response_HasViolations_WhenOverlapping()
    {
        MakeElement("Existing", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        var resp = _handler.Handle(MakeReq("create_element",
            @"{""template_name"":""Overlap"",""width"":600,""height"":400,""depth"":18,""x"":0,""y"":0,""z"":0}"));

        Assert.AreEqual("result", resp.type);
        Assert.IsTrue(GetProp<bool>(resp.data, "hasViolations"));
    }

    [Test]
    public void CreateElement_Wall_Response_NoViolations_WhenOverlappingWithAnotherWall()
    {
        // Две стены могут пересекаться — они якоря, валидатор их не проверяет.
        MakeWall("W1", new Vector3Int(2000, 2500, 100), Vector3.zero);
        var resp = _handler.Handle(MakeReq("create_element",
            @"{""template_name"":""W2"",""width"":2000,""height"":2500,""depth"":100,""x"":0,""y"":1.25,""z"":0,""is_wall"":true}"));

        Assert.AreEqual("result", resp.type);
        Assert.IsFalse(GetProp<bool>(resp.data, "hasViolations"));
    }

    [Test]
    public void GetElementInfo_Response_HasViolations_WhenOverlapping()
    {
        MakeElement("A", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        MakeElement("B", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        var resp = _handler.Handle(MakeReq("get_element_info", @"{""name"":""A""}"));

        Assert.AreEqual("result", resp.type);
        var info = (ElementInfo)resp.data;
        Assert.IsTrue(info.hasViolations, "A overlaps with B → violation");
    }

    [Test]
    public void GetElementInfo_Wall_Response_HasNoViolations()
    {
        MakeWall("W", new Vector3Int(2000, 2500, 100), Vector3.zero);
        var resp = _handler.Handle(MakeReq("get_element_info", @"{""name"":""W""}"));

        Assert.AreEqual("result", resp.type);
        var info = (ElementInfo)resp.data;
        Assert.IsFalse(info.hasViolations, "Стена — якорь → нет нарушений");
    }

    [Test]
    public void GetAllElements_Response_ContainsHasViolations()
    {
        MakeElement("A", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        MakeElement("B", new Vector3Int(1000, 1000, 1000), Vector3.zero);

        var resp = _handler.Handle(MakeReq("get_all_elements", "{}"));

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
        var resp = _handler.Handle(MakeReq("move_element",
            @"{""name"":""A"",""x"":1.8,""y"":0,""z"":0}"));

        Assert.AreEqual("result", resp.type);
        Assert.IsTrue(GetProp<bool>(resp.data, "hasViolations"));
    }

    [Test]
    public void ResizeElement_Response_HasViolations_WhenOverlapping()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(0.45f, 0f, 0f));
        var resp = _handler.Handle(MakeReq("resize_element",
            @"{""name"":""A"",""width"":1000,""height"":400,""depth"":18}"));

        Assert.AreEqual("result", resp.type);
        Assert.IsTrue(GetProp<bool>(resp.data, "hasViolations"));
    }
}
