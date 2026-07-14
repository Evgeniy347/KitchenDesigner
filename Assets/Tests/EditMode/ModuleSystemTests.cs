using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>
/// Система модулей: сборка модуля из деталей (именованная группа), режим
/// редактирования модуля (детали модуля редактируются, остальное заблокировано)
/// и MCP-доступ к конфигурации («модуль X состоит из…»).
/// </summary>
public class ModuleSystemTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private McpCommandHandler? _handler;

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos)
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

    private static McpRequest Req(string method, object? data = null)
    {
        var json = data != null
            ? Newtonsoft.Json.JsonConvert.SerializeObject(data)
            : "{}";
        return new McpRequest
        {
            id = "t",
            method = method,
            Params = Newtonsoft.Json.Linq.JObject.Parse(json)
        };
    }

    [SetUp]
    public void Setup()
    {
        _handler = new McpCommandHandler();
        ModuleEditMode.Exit();
        GroupManager.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        ModuleEditMode.Exit();
        GroupManager.Clear();
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            PartRegistry.Unregister(go.GetComponent<KitchenElement>());
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    // ── Режим редактирования модуля ─────────────────────────────────────

    [Test]
    public void NoActiveModule_EverythingEditable()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        Assert.IsFalse(ModuleEditMode.IsActive);
        Assert.IsTrue(ModuleEditMode.IsEditable(a));
    }

    [Test]
    public void ActiveModule_OnlyMembersEditable()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), new Vector3(1f, 0f, 0f));
        var outsider = Make("Out", new Vector3Int(600, 300, 18), new Vector3(2f, 0f, 0f));
        var g = GroupManager.Link(new List<KitchenElement> { a, b });

        ModuleEditMode.Enter(g!);

        Assert.IsTrue(ModuleEditMode.IsActive);
        Assert.IsTrue(ModuleEditMode.IsEditable(a), "деталь модуля редактируется");
        Assert.IsTrue(ModuleEditMode.IsEditable(b));
        Assert.IsFalse(ModuleEditMode.IsEditable(outsider), "чужой элемент заблокирован");
        Assert.IsFalse(ModuleEditMode.IsEditable(null!));
    }

    [Test]
    public void EnterExit_FiresChangedEvent()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.right);
        var g = GroupManager.Link(new List<KitchenElement> { a, b });

        int fired = 0;
        System.Action onChanged = () => fired++;
        ModuleEditMode.Changed += onChanged;
        try
        {
            ModuleEditMode.Enter(g!);
            ModuleEditMode.Enter(g!); // повторный вход — без события
            ModuleEditMode.Exit();
            ModuleEditMode.Exit();   // повторный выход — без события
        }
        finally { ModuleEditMode.Changed -= onChanged; }

        Assert.AreEqual(2, fired, "по событию на вход и на выход");
    }

    [Test]
    public void UnlinkActiveModule_ExitsEditMode()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.right);
        var g = GroupManager.Link(new List<KitchenElement> { a, b });

        ModuleEditMode.Enter(g!);
        GroupManager.Unlink(g!);

        Assert.IsFalse(ModuleEditMode.IsActive, "роспуск модуля выходит из режима");
        Assert.IsTrue(ModuleEditMode.IsEditable(a), "после роспуска всё редактируется");
    }

    // ── MCP: конфигурация модуля ────────────────────────────────────────

    [Test]
    public void Mcp_CreateModule_AndReadConfiguration()
    {
        Make("Бок левый", new Vector3Int(500, 720, 18), Vector3.zero);
        Make("Бок правый", new Vector3Int(500, 720, 18), new Vector3(0.582f, 0f, 0f));
        Make("Дно", new Vector3Int(564, 18, 500), new Vector3(0.291f, -0.351f, 0f));

        var create = _handler!.Handle(Req("create_module", new
        {
            name = "Тумба с ящиками",
            members = new[] { "Бок левый", "Бок правый", "Дно" }
        }));
        Assert.AreEqual("result", create.type, "create_module успешен");
        var created = (ModuleInfo)create.data!;
        Assert.AreEqual("Тумба с ящиками", created.name);
        Assert.AreEqual(3, created.elementCount);

        // Конфигурация видна: модуль и его состав.
        var info = _handler!.Handle(Req("module_info", new { module = "Тумба с ящиками" }));
        var m = (ModuleInfo)info.data!;
        Assert.AreEqual(3, m.elements.Count, "состав модуля виден через MCP");
        CollectionAssert.AreEquivalent(
            new[] { "Бок левый", "Бок правый", "Дно" },
            m.elements.ConvertAll(e => e.name));
        Assert.IsNotNull(m.boundsSizeMM, "габариты модуля посчитаны");
        Assert.AreEqual(1082, m!.boundsSizeMM![0], 2, "ширина: 582мм смещение + 500мм деталь");

        // Принадлежность видна и на самом элементе.
        var elInfo = _handler!.Handle(Req("get_element_info", new { name = "Дно" }));
        var el = (ElementInfo)elInfo.data!;
        Assert.AreEqual("Тумба с ящиками", el.moduleName);
        Assert.AreEqual(m.id, el.moduleId);
    }

    [Test]
    public void Mcp_CreateModule_MissingElement_Fails()
    {
        Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var resp = _handler!.Handle(Req("create_module", new
        {
            name = "X",
            members = new[] { "A", "НетТакой" }
        }));
        Assert.AreEqual("error", resp.type);
        StringAssert.Contains("НетТакой", resp.data!.ToString());
    }

    [Test]
    public void Mcp_EnterExitModuleEdit()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.right);
        var outsider = Make("Out", new Vector3Int(600, 300, 18), new Vector3(2f, 0f, 0f));
        _handler!.Handle(Req("create_module", new { name = "М1", members = new[] { "A", "B" } }));

        var enter = _handler!.Handle(Req("enter_module_edit", new { module = "М1" }));
        Assert.AreEqual("result", enter.type);
        Assert.IsTrue(ModuleEditMode.IsActive);
        Assert.IsFalse(ModuleEditMode.IsEditable(outsider), "остальная сцена заблокирована");

        // Статус редактирования виден в конфигурации.
        var m = (ModuleInfo)_handler!.Handle(Req("module_info", new { module = "М1" })).data!;
        Assert.IsTrue(m.editing);

        _handler!.Handle(Req("exit_module_edit"));
        Assert.IsFalse(ModuleEditMode.IsActive);
    }

    [Test]
    public void Mcp_AddAndRemoveFromModule()
    {
        Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        Make("B", new Vector3Int(800, 400, 18), Vector3.right);
        var extra = Make("Полка", new Vector3Int(564, 18, 450), new Vector3(0f, 1f, 0f));
        _handler!.Handle(Req("create_module", new { name = "М1", members = new[] { "A", "B" } }));

        var add = _handler!.Handle(Req("add_to_module", new { module = "М1", name = "Полка" }));
        Assert.AreEqual("result", add.type);
        Assert.AreEqual(3, ((ModuleInfo)add.data!).elementCount, "полка добавлена в модуль");
        Assert.AreNotEqual(0, extra.GroupId);

        var rem = _handler!.Handle(Req("remove_from_module", new { name = "Полка" }));
        Assert.AreEqual("result", rem.type);
        Assert.AreEqual(0, extra.GroupId, "полка исключена из модуля");
    }

    [Test]
    public void Mcp_GetModules_ListsAll()
    {
        Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        Make("B", new Vector3Int(800, 400, 18), Vector3.right);
        Make("C", new Vector3Int(800, 400, 18), new Vector3(2f, 0f, 0f));
        Make("D", new Vector3Int(800, 400, 18), new Vector3(3f, 0f, 0f));
        _handler!.Handle(Req("create_module", new { name = "М1", members = new[] { "A", "B" } }));
        _handler!.Handle(Req("create_module", new { name = "М2", members = new[] { "C", "D" } }));

        var resp = _handler!.Handle(Req("get_modules"));
        var list = (List<ModuleInfo>)resp.data!;
        Assert.AreEqual(2, list.Count);
        CollectionAssert.AreEquivalent(new[] { "М1", "М2" }, list.ConvertAll(m => m.name));
    }

    // ── Сохранение/загрузка: модуль переживает round-trip ───────────────

    [Test]
    public void Module_SurvivesSaveLoad()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), new Vector3(0f, 0.2f, 0f));
        var b = Make("B", new Vector3Int(800, 400, 18), new Vector3(1f, 0.2f, 0f));
        var g = GroupManager.Link(new List<KitchenElement> { a, b });
        g!.name = "Тумба";

        var json = SaveLoadManager.Serialize(
            SaveLoadManager.CaptureScene(new List<KitchenElement> { a, b }));

        // «Перезапуск»: чистим сцену и группы, восстанавливаем из json.
        foreach (var go in _spawned)
        {
            PartRegistry.Unregister(go.GetComponent<KitchenElement>());
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
        GroupManager.Clear();

        var created = SaveLoadManager.RestoreScene(SaveLoadManager.Deserialize(json)!);
        var restored = new List<KitchenElement>();
        foreach (var go in created)
        {
            _spawned.Add(go);
            var e = go.GetComponent<KitchenElement>();
            if (e != null) restored.Add(e);
        }

        Assert.AreEqual(2, restored.Count);
        var group = GroupManager.GroupOf(restored[0]);
        Assert.IsNotNull(group, "модуль восстановлен");
        Assert.AreEqual("Тумба", group!.name, "имя модуля сохранилось");
        Assert.AreEqual(group, GroupManager.GroupOf(restored[1]), "обе детали в одном модуле");
    }
}
