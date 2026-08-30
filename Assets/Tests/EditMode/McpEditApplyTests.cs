using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>Что edit_elements ДЕЛАЕТ с элементом помимо геометрии: пазы,
/// накладки, кромки, прикрепление, переименование. Причины, которые раньше
/// стояли комментариями внутри ApplyNonGeometryEdits, живут здесь.</summary>
public class McpEditApplyTests
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
        MaterialCatalog.Reset();
    }

    private McpRequest MakeReq(string method, object data) => new McpRequest
    {
        id = "test",
        method = method,
        Params = JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(data))
    };

    private static string ErrorMessage(McpResponse resp) =>
        JObject.FromObject(resp.data!)["message"]!.Value<string>()!;

    private void Edit(object op)
    {
        var resp = _handler!.Handle(MakeReq("edit_elements", new { ops = new[] { op } }));
        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
    }

    private T Make<T>(string name, Vector3Int dims, Vector3 pos = default) where T : KitchenElement
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        go.transform.position = pos;
        var e = go.AddComponent<T>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        return e;
    }

    [Test]
    public void EditElements_Grooves_ReplaceTheWholeSetInsteadOfAddingToIt()
    {
        var board = Make<KitchenElement>("Board", new Vector3Int(600, 400, 18));

        Edit(new { name = "Board", grooves = "through:top, blind:left" });
        Assert.AreEqual(2, board.Grooves.Count);

        Edit(new { name = "Board", grooves = "blind:right" });

        Assert.AreEqual(1, board.Grooves.Count,
            "поле grooves задаёт НАБОР целиком: второй вызов заменяет пазы, а не дописывает");
        Assert.AreEqual(new GrooveSpec(GrooveKind.Blind, GrooveSide.Right), board.Grooves[0]);
    }

    [Test]
    public void EditElements_EmptyGrooves_ClearsThemAll()
    {
        var board = Make<KitchenElement>("Board", new Vector3Int(600, 400, 18));
        Edit(new { name = "Board", grooves = "through:top" });

        Edit(new { name = "Board", grooves = "" });

        Assert.AreEqual(0, board.Grooves.Count, "пустая строка снимает все пазы");
    }

    [Test]
    public void EditElements_EdgeSkipValidation_MarksEveryOneOfTheFourSidesManual()
    {
        var shelf = Make<KitchenElement>("Shelf", new Vector3Int(800, 18, 400));

        Edit(new { name = "Shelf", edge_skip_validation = true });

        Assert.AreEqual(EdgeManual.AllMask, shelf.EdgeManualMask,
            "кромки давно правятся по сторонам, но поле контракта осталось на всю деталь: " +
            "true обязан означать «все четыре стороны ручные», иначе проверка кромок " +
            "продолжит ругаться на деталь, которую агент уже пометил как ручную");

        Edit(new { name = "Shelf", edge_skip_validation = false });

        Assert.AreEqual(0, shelf.EdgeManualMask, "false снимает ручной режим со всех сторон");
    }

    [Test]
    public void GetElements_EdgeSkipValidation_IsNullWhenOnlySomeSidesAreManual()
    {
        var shelf = Make<KitchenElement>("Shelf", new Vector3Int(800, 18, 400));
        shelf.EdgeManualMask = EdgeManual.Bit(EdgeSide.L1);

        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "Shelf" } }));
        var payload = JObject.FromObject(resp.data!);
        var element = payload["elements"]![0]!;

        Assert.IsTrue(element["edgeSkipValidation"] == null || element["edgeSkipValidation"]!.Type == JTokenType.Null,
            "частичный набор ручных сторон в контракте MCP не выражается — поле молчит, а не врёт true");
    }

    [Test]
    public void EditElements_RenamingAFacade_CarriesTheDrawersLinkWithIt()
    {
        var facade = Make<FacadeElement>("Front", new Vector3Int(450, 140, 18));
        var drawer = Make<DrawerElement>("Box", new Vector3Int(450, 140, 500));
        Edit(new { name = "Box", attached_facade_name = "Front" });
        Assert.AreEqual("Front", drawer.AttachedFacadeName);

        Edit(new { name = "Front", new_name = "FrontRenamed" });

        Assert.AreEqual("FrontRenamed", facade.PartName);
        Assert.AreEqual("FrontRenamed", drawer.AttachedFacadeName,
            "связи держатся на ИМЕНИ: переименование обязано увести их за собой, " +
            "иначе ящик остаётся с битой ссылкой на несуществующий фасад");
        Assert.AreEqual(facade.PartName, facade.gameObject.name,
            "имя GameObject идёт следом, иначе объект не найти в иерархии");
    }

    [Test]
    public void EditElements_RenamingADrawer_CarriesItsPairWithIt()
    {
        var lower = Make<DrawerElement>("Lower", new Vector3Int(450, 140, 500));
        var upper = Make<DrawerElement>("Upper", new Vector3Int(450, 140, 500));
        Edit(new { name = "Upper", paired_drawer_name = "Lower" });

        Edit(new { name = "Lower", new_name = "LowerRenamed" });

        Assert.AreEqual("LowerRenamed", lower.PartName);
        Assert.AreEqual("LowerRenamed", upper.PairedDrawerName,
            "пара сдвоенного ящика тоже держится на имени");
    }

    [Test]
    public void EditElements_AttachedToName_LinksAndThenDetachesOnAnEmptyString()
    {
        Make<KitchenElement>("Host", new Vector3Int(600, 400, 18));
        var child = Make<KitchenElement>("Child", new Vector3Int(100, 100, 18));

        Edit(new { name = "Child", attached_to_name = "Host" });
        Assert.AreEqual("Host", child.AttachedToName);

        Edit(new { name = "Child", attached_to_name = "" });

        Assert.AreEqual("", child.AttachedToName, "пустая строка — отцепить, а не «не менять»");
    }

    [Test]
    public void EditElements_GapsOnARadialShelf_AreApplied()
    {
        var shelf = Make<RadialShelfElement>("Shelf", new Vector3Int(600, 18, 400));
        Assert.IsTrue(shelf.SupportsGaps);

        Edit(new { name = "Shelf", gap_left = 3, gap_top = 2 });

        Assert.AreEqual(3, shelf.GapLeft,
            "зазоры есть не только у фасада — они правятся по SupportsGaps, а не по типу элемента");
        Assert.AreEqual(2, shelf.GapTop);
    }
}
