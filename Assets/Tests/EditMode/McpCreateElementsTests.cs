using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>create_elements: реестр ElementSpawners (тип → фабрика) и то, что
/// происходит вокруг него — отказ по модели прибора и привязка проёмов после
/// применения батча.</summary>
public class McpCreateElementsTests
{
    private McpCommandHandler? _handler;

    [SetUp]
    public void Setup()
    {
        _handler = new McpCommandHandler();
        PartRegistry.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    private McpRequest MakeReq(string method, object data) => new McpRequest
    {
        id = "test",
        method = method,
        Params = JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(data))
    };

    private static string ErrorMessage(McpResponse resp) =>
        JObject.FromObject(resp.data!)["message"]!.Value<string>()!;

    private static T? Find<T>(string name) where T : KitchenElement =>
        PartRegistry.GetAll().Find(e => e.PartName == name) as T;

    [Test]
    public void CreateElements_WallAndWindowInOneBatch_TheWindowStillFindsTheWall()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[]
            {
                new { name = "BatchWall", type = "wall", width = 100, height = 2700, depth = 3000, anchor_x_mm = 0f, anchor_y_mm = 1350f, anchor_z_mm = 0f },
                new { name = "BatchWin", type = "window", width = 900, height = 1200, depth = 100, anchor_x_mm = 0f, anchor_y_mm = 1200f, anchor_z_mm = 0f },
            }
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        var window = Find<WindowElement>("BatchWin");
        Assert.NotNull(window);
        Assert.AreEqual("BatchWall", window!.AttachedWallName,
            "проёмы привязываются ПОСЛЕ применения батча: стена из этого же батча " +
            "к моменту привязки уже зарегистрирована, иначе окно не найдёт её и проём не прорежется");
    }

    [Test]
    public void CreateElements_UnknownApplianceModel_IsRejectedInsteadOfMakingAFreeSizeAppliance()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[] { new { name = "Hob", type = "cooktop", model = "Bosch NOT-A-MODEL", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } }
        }));

        Assert.AreEqual("error", resp.type,
            "неизвестная модель молча дала бы свободный прибор «почти того» размера");
        StringAssert.Contains("Unknown appliance model", ErrorMessage(resp));
        Assert.IsNull(Find<KitchenElement>("Hob"), "ничего не создано");
    }

    [Test]
    public void CreateElements_UnknownType_IsRejectedInsteadOfSilentlyMakingAPlainBoard()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[]
            {
                new { name = "Typo", type = "movnto_drawer", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f },
                new { name = "Good", type = "panel", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f },
            }
        }));

        Assert.AreEqual("error", resp.type,
            "опечатка в типе раньше молча давала обычную деталь 800x400x18 и ok — " +
            "клиент считал, что получил ящик Movento");
        StringAssert.Contains("Unknown type 'movnto_drawer' for 'Typo'", ErrorMessage(resp));
        StringAssert.Contains("movento_drawer", ErrorMessage(resp),
            "в отказе перечислены допустимые типы: список статичен и короток, " +
            "отдельного инструмента-справочника для него нет, поэтому опечатка чинится за один вызов");
        Assert.IsNull(Find<KitchenElement>("Typo"), "ничего не создано");
        Assert.IsNull(Find<KitchenElement>("Good"),
            "батч атомарен: исправный элемент того же батча тоже не создан");
    }

    [Test]
    public void CreateElements_FacadeAndWall_GetTheirOwnComponents()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[]
            {
                new { name = "Door1", type = "facade", width = 450, height = 700, depth = 18, anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f },
                new { name = "Wall1", type = "wall", width = 100, height = 2700, depth = 3000, anchor_x_mm = 5000f, anchor_y_mm = 1350f, anchor_z_mm = 0f },
            }
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        Assert.IsInstanceOf<FacadeElement>(Find<KitchenElement>("Door1"),
            "type=facade даёт FacadeElement, иначе у дверцы не будет ни петель, ни зазоров");
        Assert.NotNull(Find<KitchenElement>("Wall1")!.GetComponent<Wall>(),
            "type=wall — обычная деталь ПЛЮС компонент Wall: он делает её несущей");
    }

    [Test]
    public void CreateElements_MoventoDrawer_GetsTheMoventoRunnerSystem()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[] { new { name = "Movento1", type = "movento_drawer", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } }
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        var drawer = Find<DrawerElement>("Movento1");
        Assert.NotNull(drawer,
            "ящик Movento — не отдельный класс, а тот же DrawerElement под другой системой выдвижения");
        Assert.AreEqual(DrawerSystem.Movento, drawer!.System,
            "именно система выдвижения отличает Movento от GTV: она решает, уйдёт ящик в " +
            "спецификацию одной покупной строкой или раскладкой деревянных деталей");
        Assert.AreEqual(ElementSpawners.MOVENTO_DRAWER_DEFAULT_LENGTH_MM, drawer.NominalLength);
        Assert.AreEqual(ElementSpawners.MOVENTO_DRAWER_DEFAULT_INTERNAL_WIDTH_MM, drawer.InternalWidth);
    }

    [Test]
    public void CreateElements_TypeListInTheContract_MatchesTheSpawnerRegistry()
    {
        var contract = McpContractEnums.Of(typeof(CreateItem), nameof(CreateItem.type));
        var spawnable = ElementSpawners.SpawnableTypes;
        var contractOnly = contract.Except(spawnable).ToList();
        var registryOnly = spawnable.Except(contract).ToList();

        CollectionAssert.AreEquivalent(spawnable, contract,
            "список типов создания живёт в двух местах и обязан совпадать: тип, который умеет " +
            "ElementSpawners, но контракт не объявляет, клиенту недоступен; тип, объявленный в " +
            "контракте без фабрики, молча даёт обычную деталь вместо отказа. " +
            $"только в контракте: [{string.Join(", ", contractOnly)}]; " +
            $"только в реестре: [{string.Join(", ", registryOnly)}]");
    }

    [Test]
    public void CreateElements_DefaultType_IsABoard()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[] { new { name = "Plain", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } }
        }));

        var element = Find<KitchenElement>("Plain");
        Assert.NotNull(element, "тип не указан — деталь");
        Assert.AreEqual(typeof(KitchenElement), element!.GetType());
    }
}
