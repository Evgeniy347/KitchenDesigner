using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using Newtonsoft.Json.Linq;

public class DrawerMcpTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private McpCommandHandler? _handler;

    [SetUp]
    public void Setup()
    {
        _handler = new McpCommandHandler();
        PartRegistry.Clear();
        GroupManager.Clear();
        MaterialCatalog.RegisterDynamic(new MaterialDef("gtv_anthracite", "Антрацит (GTV)", "Металл", new Color(0.25f, 0.25f, 0.27f)));
        MaterialCatalog.RegisterDynamic(new MaterialDef("gtv_white", "Белый (GTV)", "Металл", new Color(0.92f, 0.92f, 0.90f)));
        MaterialCatalog.RegisterDynamic(new MaterialDef("gtv_black", "Чёрный (GTV)", "Металл", new Color(0.10f, 0.10f, 0.11f)));
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
        GroupManager.Clear();
    }

    private McpRequest MakeReq(string method, object data)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        return new McpRequest { id = "test", method = method, Params = JObject.Parse(json) };
    }

    private McpResponse CreateDrawer(string name)
    {
        return _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[] { new { name, type = "drawer", x = 0f, y = 0f, z = 0f } }
        }));
    }

    private McpResponse CreateDrawerWithProps(string name, string drawerType, int length, string color, int width)
    {
        var resp = CreateDrawer(name);
        if (resp.type != "result") return resp;
        return _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name, drawer_type = drawerType, drawer_length = length, drawer_color = color, internal_width = width } }
        }));
    }

    [Test]
    public void CreateDrawer_ThroughMcp_CreatesDrawerElement()
    {
        var resp = CreateDrawer("TestDrawer");
        Assert.AreEqual("result", resp.type);

        var go = GameObject.Find("TestDrawer");
        if (go != null) _spawned.Add(go);
        var el = PartRegistry.GetAll().Find(e => e.PartName == "TestDrawer");
        Assert.IsNotNull(el);
        Assert.IsTrue(el is DrawerElement);
    }

    [Test]
    public void GetElementInfo_Drawer_ReturnsDrawerType()
    {
        CreateDrawer("InfoDrawer");
        var go = GameObject.Find("InfoDrawer");
        if (go != null) _spawned.Add(go);

        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "InfoDrawer" } }));
        Assert.AreEqual("result", resp.type);

        var json = JObject.FromObject(resp.data!);
        Assert.IsNotNull(json["elements"]![0]!["drawer"]);
    }

    [Test]
    public void GetAllElements_IncludesDrawerInfo()
    {
        CreateDrawerWithProps("MixedDrawer", "B", 300, "White", 500);
        var drawerGo = GameObject.Find("MixedDrawer");
        if (drawerGo != null) _spawned.Add(drawerGo);

        var resp = _handler!.Handle(MakeReq("get_all_elements", new { }));
        Assert.AreEqual("result", resp.type);

        var list = (List<ElementInfo>)resp.data!;
        Assert.Greater(list.Count, 0);
        bool hasDrawer = false;
        foreach (var info in list)
            if (info.drawer != null) hasDrawer = true;
        Assert.IsTrue(hasDrawer, "get_all_elements should include drawer info");
    }

    [Test]
    public void SetDrawerProperties_ChangesType()
    {
        CreateDrawer("TypeDrawer");
        var go = GameObject.Find("TypeDrawer");
        if (go != null) _spawned.Add(go);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "TypeDrawer", drawer_type = "B" } }
        }));
        Assert.AreEqual("result", resp.type);

        var drawer = PartRegistry.GetAll().Find(e => e.PartName == "TypeDrawer") as DrawerElement;
        Assert.IsNotNull(drawer);
        Assert.AreEqual(DrawerType.B, drawer!.Type);
    }

    [Test]
    public void SetDrawerProperties_ChangesColor()
    {
        CreateDrawer("ColorDrawer");
        var go = GameObject.Find("ColorDrawer");
        if (go != null) _spawned.Add(go);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "ColorDrawer", drawer_color = "White" } }
        }));
        Assert.AreEqual("result", resp.type);

        var drawer = PartRegistry.GetAll().Find(e => e.PartName == "ColorDrawer") as DrawerElement;
        Assert.IsNotNull(drawer);
        Assert.AreEqual(DrawerColor.White, drawer!.Color);
    }

    [Test]
    public void SetDrawerProperties_ChangesIsDouble()
    {
        CreateDrawer("DoubleDrawer");
        var go = GameObject.Find("DoubleDrawer");
        if (go != null) _spawned.Add(go);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "DoubleDrawer", is_double = true, is_upper = true } }
        }));
        Assert.AreEqual("result", resp.type);

        var drawer = PartRegistry.GetAll().Find(e => e.PartName == "DoubleDrawer") as DrawerElement;
        Assert.IsNotNull(drawer);
        Assert.IsTrue(drawer!.IsDouble);
        Assert.IsTrue(drawer.IsUpperDrawer);
    }

    [Test]
    public void CycleDrawerAnimation_ClosedToBothOpen()
    {
        CreateDrawer("CycleDrawer");
        var go = GameObject.Find("CycleDrawer");
        if (go != null) _spawned.Add(go);

        var resp = _handler!.Handle(MakeReq("cycle_drawer_animation", new { names = new[] { "CycleDrawer" } }));
        Assert.AreEqual("result", resp.type);

        var drawer = PartRegistry.GetAll().Find(e => e.PartName == "CycleDrawer") as DrawerElement;
        Assert.IsNotNull(drawer);
        Assert.IsTrue(drawer!.IsOpen);
        Assert.AreEqual(DoubleDrawerState.Closed, drawer.DoubleState);

        _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "CycleDrawer", is_double = true } }
        }));
        var resp2 = _handler!.Handle(MakeReq("cycle_drawer_animation", new { names = new[] { "CycleDrawer" } }));
        Assert.AreEqual("result", resp2.type);
        Assert.AreEqual(DoubleDrawerState.BothOpen, drawer!.DoubleState);
    }

    [Test]
    public void GetViolations_IncludesDrawerValidation()
    {
        CreateDrawer("ViolationDrawer");
        var go = GameObject.Find("ViolationDrawer");
        if (go != null) _spawned.Add(go);

        var resp = _handler!.Handle(MakeReq("get_violations", new { }));
        Assert.AreEqual("result", resp.type);
    }

    [Test]
    public void CreateDrawer_InvalidLength_StillSucceeds()
    {
        var resp = CreateDrawer("BadLength");
        Assert.AreEqual("result", resp.type);

        var go = GameObject.Find("BadLength");
        if (go != null) _spawned.Add(go);

        var resp2 = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "BadLength", drawer_length = 200 } }
        }));
        Assert.AreEqual("result", resp2.type);
    }

    [Test]
    public void CreateDrawer_FacadeAttachment()
    {
        CreateDrawer("AttachDrawer");
        var go = GameObject.Find("AttachDrawer");
        if (go != null) _spawned.Add(go);

        var respMissing = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "AttachDrawer", attached_facade_name = "TestFacade" } }
        }));
        Assert.AreEqual("result", respMissing.type);

        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[] { new { name = "TestFacade", type = "facade", width = 400, height = 86, depth = 18, x = 0f, y = 0f, z = 0.3f } }
        }));
        var facadeGo = GameObject.Find("TestFacade");
        if (facadeGo != null) _spawned.Add(facadeGo);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "AttachDrawer", attached_facade_name = "TestFacade" } }
        }));
        Assert.AreEqual("result", resp.type);

        var drawer = PartRegistry.GetAll().Find(e => e.PartName == "AttachDrawer") as DrawerElement;
        Assert.IsNotNull(drawer);
        Assert.AreEqual("TestFacade", drawer!.AttachedFacadeName);

        var respDetach = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "AttachDrawer", attached_facade_name = "" } }
        }));
        Assert.AreEqual("result", respDetach.type);
        Assert.IsEmpty(drawer!.AttachedFacadeName);
    }
}
