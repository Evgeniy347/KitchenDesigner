using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using Newtonsoft.Json.Linq;

public class DrawerMcpTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private McpCommandHandler _handler;

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

    private static object MakeCreateDrawerArgs(string templateName, string drawerType, int length, string color, int width)
    {
        return new { template_name = templateName, is_drawer = true, drawer_type = drawerType,
            drawer_length = length, drawer_color = color, drawer_internal_width = width, x = 0f, y = 0f, z = 0f };
    }

    [Test]
    public void CreateDrawer_ThroughMcp_CreatesDrawerElement()
    {
        var resp = _handler.Handle(MakeReq("create_element", MakeCreateDrawerArgs("TestDrawer", "A", 350, "Anthracite", 400)));
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
        _handler.Handle(MakeReq("create_element", MakeCreateDrawerArgs("InfoDrawer", "A", 350, "Anthracite", 400)));
        var go = GameObject.Find("InfoDrawer");
        if (go != null) _spawned.Add(go);

        var resp = _handler.Handle(MakeReq("get_element_info", new { name = "InfoDrawer" }));
        Assert.AreEqual("result", resp.type);

        var json = JObject.FromObject(resp.data);
        Assert.IsNotNull(json["drawer"]);
    }

    [Test]
    public void GetAllElements_IncludesDrawerInfo()
    {
        _handler.Handle(MakeReq("create_element", MakeCreateDrawerArgs("MixedDrawer", "B", 300, "White", 500)));
        var drawerGo = GameObject.Find("MixedDrawer");
        if (drawerGo != null) _spawned.Add(drawerGo);

        var resp = _handler.Handle(MakeReq("get_all_elements", new { }));
        Assert.AreEqual("result", resp.type);

        var list = (List<ElementInfo>)resp.data;
        Assert.Greater(list.Count, 0);
        bool hasDrawer = false;
        foreach (var info in list)
            if (info.drawer != null) hasDrawer = true;
        Assert.IsTrue(hasDrawer, "get_all_elements should include drawer info");
    }

    [Test]
    public void SetDrawerProperties_ChangesType()
    {
        _handler.Handle(MakeReq("create_element", MakeCreateDrawerArgs("TypeDrawer", "A", 350, "Anthracite", 400)));
        var go = GameObject.Find("TypeDrawer");
        if (go != null) _spawned.Add(go);

        var resp = _handler.Handle(MakeReq("set_drawer_properties", new { name = "TypeDrawer", drawer_type = "B" }));
        Assert.AreEqual("result", resp.type);

        var drawer = PartRegistry.GetAll().Find(e => e.PartName == "TypeDrawer") as DrawerElement;
        Assert.IsNotNull(drawer);
        Assert.AreEqual(DrawerType.B, drawer.Type);
    }

    [Test]
    public void SetDrawerProperties_ChangesColor()
    {
        _handler.Handle(MakeReq("create_element", MakeCreateDrawerArgs("ColorDrawer", "A", 350, "Anthracite", 400)));
        var go = GameObject.Find("ColorDrawer");
        if (go != null) _spawned.Add(go);

        var resp = _handler.Handle(MakeReq("set_drawer_properties", new { name = "ColorDrawer", drawer_color = "White" }));
        Assert.AreEqual("result", resp.type);

        var drawer = PartRegistry.GetAll().Find(e => e.PartName == "ColorDrawer") as DrawerElement;
        Assert.IsNotNull(drawer);
        Assert.AreEqual(DrawerColor.White, drawer.Color);
    }

    [Test]
    public void SetDrawerProperties_ChangesIsDouble()
    {
        _handler.Handle(MakeReq("create_element", MakeCreateDrawerArgs("DoubleDrawer", "A", 350, "Anthracite", 400)));
        var go = GameObject.Find("DoubleDrawer");
        if (go != null) _spawned.Add(go);

        var resp = _handler.Handle(MakeReq("set_drawer_properties", new { name = "DoubleDrawer", is_double = true, is_upper = true }));
        Assert.AreEqual("result", resp.type);

        var drawer = PartRegistry.GetAll().Find(e => e.PartName == "DoubleDrawer") as DrawerElement;
        Assert.IsNotNull(drawer);
        Assert.IsTrue(drawer.IsDouble);
        Assert.IsTrue(drawer.IsUpperDrawer);
    }

    [Test]
    public void CycleDrawerAnimation_ClosedToBothOpen()
    {
        _handler.Handle(MakeReq("create_element", new { template_name = "CycleDrawer", is_drawer = true,
            drawer_type = "A", drawer_length = 350, drawer_color = "Anthracite", drawer_internal_width = 400,
            x = 0f, y = 0f, z = 0f }));
        var go = GameObject.Find("CycleDrawer");
        if (go != null) _spawned.Add(go);

        // Одиночный ящик: cycle — это toggle открыт/закрыт, DoubleState не трогается.
        var resp = _handler.Handle(MakeReq("cycle_drawer_animation", new { name = "CycleDrawer" }));
        Assert.AreEqual("result", resp.type);

        var drawer = PartRegistry.GetAll().Find(e => e.PartName == "CycleDrawer") as DrawerElement;
        Assert.IsNotNull(drawer);
        Assert.IsTrue(drawer.IsOpen, "одиночный ящик открылся");
        Assert.AreEqual(DoubleDrawerState.Closed, drawer.DoubleState);

        // Двойной ящик: cycle гоняет трёхфазный цикл состояний.
        _handler.Handle(MakeReq("set_drawer_properties", new { name = "CycleDrawer", is_double = true }));
        var resp2 = _handler.Handle(MakeReq("cycle_drawer_animation", new { name = "CycleDrawer" }));
        Assert.AreEqual("result", resp2.type);
        Assert.AreEqual(DoubleDrawerState.BothOpen, drawer.DoubleState);
    }

    [Test]
    public void GetViolations_IncludesDrawerValidation()
    {
        _handler.Handle(MakeReq("create_element", MakeCreateDrawerArgs("ViolationDrawer", "A", 350, "Anthracite", 400)));
        var go = GameObject.Find("ViolationDrawer");
        if (go != null) _spawned.Add(go);

        var resp = _handler.Handle(MakeReq("get_violations", new { }));
        Assert.AreEqual("result", resp.type);
    }

    [Test]
    public void CreateDrawer_InvalidLength_StillSucceeds()
    {
        var resp = _handler.Handle(MakeReq("create_element", new { template_name = "BadLength",
            is_drawer = true, drawer_type = "A", drawer_length = 200, drawer_color = "Anthracite",
            drawer_internal_width = 400, x = 0f, y = 0f, z = 0f }));
        Assert.AreEqual("result", resp.type);

        var go = GameObject.Find("BadLength");
        if (go != null) _spawned.Add(go);
    }

    [Test]
    public void CreateDrawer_FacadeAttachment()
    {
        _handler.Handle(MakeReq("create_element", MakeCreateDrawerArgs("AttachDrawer", "A", 350, "Anthracite", 400)));
        var go = GameObject.Find("AttachDrawer");
        if (go != null) _spawned.Add(go);

        // Привязка к несуществующему фасаду — ошибка (валидация имени).
        var respMissing = _handler.Handle(MakeReq("set_drawer_properties", new { name = "AttachDrawer", attached_facade_name = "TestFacade" }));
        Assert.AreEqual("error", respMissing.type, "несуществующий фасад отклоняется");

        _handler.Handle(MakeReq("create_element", new { template_name = "TestFacade", is_facade = true,
            width = 400, height = 86, depth = 18, x = 0f, y = 0f, z = 0.3f }));
        var facadeGo = GameObject.Find("TestFacade");
        if (facadeGo != null) _spawned.Add(facadeGo);

        var resp = _handler.Handle(MakeReq("set_drawer_properties", new { name = "AttachDrawer", attached_facade_name = "TestFacade" }));
        Assert.AreEqual("result", resp.type);

        var drawer = PartRegistry.GetAll().Find(e => e.PartName == "AttachDrawer") as DrawerElement;
        Assert.IsNotNull(drawer);
        Assert.AreEqual("TestFacade", drawer.AttachedFacadeName);

        // Пустая строка отвязывает фасад.
        var respDetach = _handler.Handle(MakeReq("set_drawer_properties", new { name = "AttachDrawer", attached_facade_name = "" }));
        Assert.AreEqual("result", respDetach.type);
        Assert.IsEmpty(drawer.AttachedFacadeName);
    }
}
