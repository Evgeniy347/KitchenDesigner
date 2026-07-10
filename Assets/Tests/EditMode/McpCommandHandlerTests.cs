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
}
