using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>Что edit_elements ДЕЛАЕТ с элементом помимо геометрии: пазы,
/// накладки, кромки, прикрепление, переименование. Причины, которые раньше
/// стояли комментариями внутри ApplyNonGeometryEdits, живут здесь.</summary>
public class McpEditApplyTests : McpTestFixture
{
    [TearDown]
    public void Teardown()
    {
        MaterialCatalog.Reset();
    }

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

    /// <summary>Три состояния присутствия кромки по сторонам булевым полем не
    /// выражаются, поэтому контракт носит строку в стиле grooves и texture_overlays.
    /// Стороны, которых в строке нет, остаются как были.</summary>
    [Test]
    public void EditElements_EdgeSides_SetsForcedAndSuppressedPerSide()
    {
        var shelf = Make<KitchenElement>("Shelf", new Vector3Int(800, 18, 400));

        Edit(new { name = "Shelf", edge_sides = "L1:on; W1:off" });

        Assert.AreEqual(EdgeSideState.Forced, shelf.EdgeStateOf(EdgeSide.L1),
            "on — кромка на этой стороне ЕСТЬ, даже если сцена торец закрывает");
        Assert.AreEqual(EdgeSideState.Suppressed, shelf.EdgeStateOf(EdgeSide.W1),
            "off — кромки НЕТ, даже если торец открыт: спецификация обязана увидеть "
            + "на одну кромку меньше");
        Assert.AreEqual(EdgeSideState.Auto, shelf.EdgeStateOf(EdgeSide.L2),
            "сторону, которую строка не называет, правка не задевает");
    }

    [Test]
    public void EditElements_EdgeSides_EmptyString_PutsEveryEndBackToAuto()
    {
        var shelf = Make<KitchenElement>("Shelf", new Vector3Int(800, 18, 400));
        Edit(new { name = "Shelf", edge_sides = "L1:on; W1:off" });

        Edit(new { name = "Shelf", edge_sides = "" });

        foreach (EdgeSide side in EdgeStates.All)
            Assert.AreEqual(EdgeSideState.Auto, shelf.EdgeStateOf(side),
                $"{side}: пустая строка снимает все явные решения — как 'grooves = \"\"' снимает пазы");
    }

    [Test]
    public void GetElements_EdgeSides_MirrorsWhatEditAccepts()
    {
        var shelf = Make<KitchenElement>("Shelf", new Vector3Int(800, 18, 400));
        shelf.SetEdgeState(EdgeSide.L1, EdgeSideState.Forced);
        shelf.SetEdgeState(EdgeSide.W1, EdgeSideState.Suppressed);

        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "Shelf" } }));
        var payload = JObject.FromObject(resp.data!);
        var element = payload["elements"]![0]!;

        Assert.AreEqual("L1:on; W1:off", element["edgeSides"]!.ToString(),
            "ответ обязан читаться тем же кодеком, что и запрос — иначе агент не может "
            + "вернуть прочитанное обратно");
    }

    [Test]
    public void GetElements_EdgeSides_IsNullWhenEveryEndIsAuto()
    {
        Make<KitchenElement>("Shelf", new Vector3Int(800, 18, 400));

        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "Shelf" } }));
        var payload = JObject.FromObject(resp.data!);
        var element = payload["elements"]![0]!;

        Assert.IsTrue(element["edgeSides"] == null || element["edgeSides"]!.Type == JTokenType.Null,
            "деталь без единого решения человека поле не несёт — умолчание не шумит в ответе");
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

    [Test]
    public void EditElements_PillarDiameter_SetsBothWidthAndDepth()
    {
        var pillar = Make<PillarElement>("Опора", new Vector3Int(50, 105, 50));

        Edit(new { name = "Опора", diameter_mm = 140 });

        Assert.AreEqual(140, pillar.DiameterMM);
        Assert.AreEqual(140, pillar.DimensionsMM.x, "ширина = диаметр");
        Assert.AreEqual(140, pillar.DimensionsMM.z, "глубина = диаметр");
    }
}
