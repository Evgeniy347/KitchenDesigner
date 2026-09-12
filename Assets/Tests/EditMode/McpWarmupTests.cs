using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>Прогрев MCP, заведённый по разбивке дымового прогона 12.09. Первый
/// <c>create_elements</c> сессии стоил 130,27 мс, второй — 1,96 мс, и разбивка назвала
/// разницу поимённо: этап <c>accept</c> 57,61 мс против 0,05 мс и <c>exec->resp</c>
/// 46,40 мс против 0,38 мс. Вместе 104 мс из 130 — это Newtonsoft, впервые строящий
/// контракты на <c>ParamsCreateElements</c> с его <c>CreateItem[]</c> (около двухсот
/// полей) и на <c>ElementInfo</c>.
///
/// Мерить это временем нельзя, и здесь оно не меряется: сенсор считает РАБОТУ — сколько
/// контрактов Newtonsoft построил за окно. Контракт строится ровно один раз на тип, а
/// резолвер у MCP теперь ОДИН и общий (<c>McpJson.Resolver</c>), поэтому «ноль
/// построенных контрактов» и есть «холод уже оплачен».
///
/// Проверяется на Unity-слое, а не в dotnet, потому что сам Newtonsoft живёт здесь:
/// быстрый набор компилирует только <c>Core/Pure</c>, где его нет. Правило отбора
/// типов вынесено в <c>McpWireTypes</c> и проверяется dotnet-ом отдельно.</summary>
public class McpWarmupTests
{
    private static JObject ACreateRequest() => new JObject
    {
        ["items"] = new JArray
        {
            new JObject
            {
                ["type"] = "board",
                ["name"] = "sensor",
                ["anchor_x_mm"] = 0,
                ["anchor_y_mm"] = 0,
                ["anchor_z_mm"] = 0
            }
        }
    };

    [SetUp]
    public void SetUp() => McpWarmup.Run();

    [Test]
    public void RunningTheWarmupTwice_BuildsNoContractsTheSecondTime()
    {
        int before = McpJson.Resolver.ContractsBuilt;

        McpWarmup.Run();

        Assert.AreEqual(0, McpJson.Resolver.ContractsBuilt - before,
            "прогрев обязан оставить резолвер полностью прогретым: если второй проход "
            + "строит контракты, значит первый до этих типов не дошёл — и их построит "
            + "первый настоящий вызов агента");
    }

    [Test]
    public void AfterTheWarmup_ParsingACreateRequest_BuildsNoContracts()
    {
        int before = McpJson.Resolver.ContractsBuilt;

        ACreateRequest().ToObjectStrict<ParamsCreateElements>();

        Assert.AreEqual(0, McpJson.Resolver.ContractsBuilt - before,
            "это и есть этап accept, стоивший 57,61 мс на холодном вызове: разбор "
            + "ParamsCreateElements вместе с CreateItem. После прогрева он не строит ничего");
    }

    [Test]
    public void AfterTheWarmup_ParsingAnEditRequest_BuildsNoContracts()
    {
        int before = McpJson.Resolver.ContractsBuilt;

        new JObject { ["ops"] = new JArray { new JObject { ["name"] = "sensor" } } }
            .ToObjectStrict<ParamsEditElements>();

        Assert.AreEqual(0, McpJson.Resolver.ContractsBuilt - before,
            "EditOp — самый широкий контракт поверхности; холод у него такой же, "
            + "и платить его первой правкой агента незачем");
    }

    [Test]
    public void AfterTheWarmup_WritingAnElementInfo_BuildsNoContracts()
    {
        int before = McpJson.Resolver.ContractsBuilt;

        JsonConvert.SerializeObject(new List<ElementInfo> { new ElementInfo() },
            Formatting.Indented, McpJson.Settings);

        Assert.AreEqual(0, McpJson.Resolver.ContractsBuilt - before,
            "ElementInfo — тело ответа любого вызова про элементы; на холоде он был "
            + "главной частью exec->resp = 46,40 мс");
    }

    [Test]
    public void AfterTheWarmup_AResponseCostsAtMostItsOwnWrapper()
    {
        int before = McpJson.Resolver.ContractsBuilt;

        JsonConvert.SerializeObject(
            new
            {
                ok = true,
                created = new List<string> { "sensor" },
                elements = new List<ElementInfo> { new ElementInfo() },
                sceneViolationCount = 0
            },
            Formatting.Indented, McpJson.Settings);

        Assert.LessOrEqual(McpJson.Resolver.ContractsBuilt - before, 1,
            "обёртка ответа — анонимный тип, свой у каждого места вызова, и прогреть "
            + "её заранее нельзя: это честный остаток холода. Но он ОДИН контракт на "
            + "четыре скалярных поля, а не весь ElementInfo");
    }

    [Test]
    public void TheWarmup_CoversEveryWireType_NotAHandful()
    {
        Assert.Greater(McpWarmup.TypesWarmed, 20,
            "прогрев обязан пройти по всей поверхности; если он вдруг греет пару типов, "
            + "правило отбора перестало находить контракты и прогрев стал пустышкой");
    }

    [Test]
    public void RunOnce_StaysDone_SoTheBridgeNeverWarmsTwice()
    {
        McpWarmup.RunOnce();

        Assert.IsTrue(McpWarmup.Done,
            "мост зовёт RunOnce до старта слушающего потока: к первому запросу прогрев "
            + "уже состоялся, и повторно платить за него никто не должен");
    }
}
