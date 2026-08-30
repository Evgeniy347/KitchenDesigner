using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>Какое поле edit_elements какой тип элемента принимает. Таблица
/// EditFieldRules заменила лестницу `if (el is X)`, и эти тесты держат её
/// частные случаи — те, что раньше объяснялись комментариями в исходнике.</summary>
public class McpEditFieldRulesTests
{
    private McpCommandHandler? _handler;
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
    }

    private McpRequest MakeReq(string method, object data) => new McpRequest
    {
        id = "test",
        method = method,
        Params = JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(data))
    };

    private static string ErrorMessage(McpResponse resp) =>
        JObject.FromObject(resp.data!)["message"]!.Value<string>()!;

    private KitchenElement MakeBoard(string name, Vector3Int dims)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        return e;
    }

    [Test]
    public void EditElements_EdgeBandingTogetherWithAResize_IsAcceptedOnABarThatBecomesASheet()
    {
        var bar = MakeBoard("Bar", new Vector3Int(18, 18, 800));
        Assert.IsFalse(bar.SupportsEdges,
            "брусок 18x18x800 листом не является — кромок у него сейчас нет");

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Bar", height = 400, edge_banding = true, edge_thickness_mm = 2f } }
        }));

        Assert.AreEqual("result", resp.type,
            "«лист ли деталь» проверять на входе рано: габарит меняется этой же операцией, " +
            "и отказ здесь запретил бы за один вызов сделать лист и включить ему кромку. " +
            (resp.type == "error" ? ErrorMessage(resp) : ""));
        Assert.IsTrue(bar.SupportsEdges, "после правки деталь стала листом 18x400x800");
        Assert.IsTrue(bar.EdgeBandingEnabled);
        Assert.AreEqual(2f, bar.EdgeThicknessMM, 1e-4f);
    }

    [Test]
    public void EditElements_EdgeThicknessOutOfRange_IsRejected()
    {
        MakeBoard("Shelf", new Vector3Int(800, 18, 400));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Shelf", edge_thickness_mm = AppConstants.EDGE_THICKNESS_MAX_MM + 1f } }
        }));

        Assert.AreEqual("error", resp.type);
        StringAssert.Contains("edge_thickness_mm: out of range", ErrorMessage(resp));
    }

    [Test]
    public void EditElements_EdgeFieldsOnAFacade_AreRejected()
    {
        var go = new GameObject("Door");
        _spawned.Add(go);
        var facade = go.AddComponent<FacadeElement>();
        facade.PartName = "Door";
        facade.DimensionsMM = new Vector3Int(450, 700, 18);
        PartRegistry.Register(facade);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Door", edge_banding = true } }
        }));

        Assert.AreEqual("error", resp.type,
            "кромкование — свойство базовой детали: у фасада своя процедурная геометрия");
        StringAssert.Contains("edge_banding (plain boards only)", ErrorMessage(resp));
    }

    [Test]
    public void EditElements_RejectedBatch_ReportsEveryBadFieldAtOnce()
    {
        MakeBoard("Shelf", new Vector3Int(800, 18, 400));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Shelf", fill = "glass", corner_radius = 50, mid_height_mm = 100 } }
        }));

        Assert.AreEqual("error", resp.type);
        string message = ErrorMessage(resp);
        StringAssert.Contains("fill", message);
        StringAssert.Contains("corner_radius", message);
        StringAssert.Contains("mid_height_mm", message);
        StringAssert.Contains("NOTHING was applied", message,
            "батч атомарен: одна негодная операция отклоняет весь батч");
    }
}
