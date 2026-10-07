using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>Отказ слабой модели не должен быть просто «rejected»: он называет, что не так, и как исправить,
/// а атомарный отказ батча напоминает прислать ВЕСЬ батч заново (частичный повтор давал дубль имени).</summary>
public class McpRefusalHintsTests : McpTestFixture
{
    [SetUp]
    public void Setup() => CommandStack.Clear();

    [TearDown]
    public void Teardown() => CommandStack.Clear();

    private static string Message(McpResponse resp) => JObject.FromObject(resp.data!)["message"]!.Value<string>()!;

    private McpResponse Create(string name) => _handler!.Handle(MakeReq("create_elements", new
    {
        items = new[] { new { name, width = 600, height = 400, depth = 18, anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } }
    }));

    [Test]
    public void CreatingANameThatExists_SuggestsEditPlaceOrAFreeName()
    {
        MakeElement("Shelf", new Vector3Int(600, 18, 400));

        var message = Message(Create("Shelf"));

        StringAssert.Contains("'Shelf' already exists", message);
        StringAssert.Contains("edit_elements", message);
        StringAssert.Contains("'Shelf_2'", message, "предложено свободное имя");
        StringAssert.Contains("resend the WHOLE batch", message);
    }

    [Test]
    public void ATypoInAName_GetsTheClosestRealOnes_OnEveryBatchTool()
    {
        MakeElement("Side_L", new Vector3Int(18, 720, 560));
        MakeElement("Side_R", new Vector3Int(18, 720, 560), new Vector3(1f, 0f, 0f));

        var edit = Message(_handler!.Handle(MakeReq("edit_elements", new { ops = new[] { new { name = "Side_X", locked = false } } })));
        var clone = Message(_handler.Handle(MakeReq("clone_elements", new { ops = new[] { new { name = "Sidel", count = 1 } } })));
        var delete = Message(_handler.Handle(MakeReq("delete_elements", new { names = new[] { "side_l" } })));

        StringAssert.Contains("closest names: Side_L", edit);
        StringAssert.Contains("closest names: Side_L", clone);
        StringAssert.Contains("closest names: Side_L", delete);
        StringAssert.Contains("Element not found: Side_X", edit, "прежний префикс сохранён: клиенты ищут по нему");
    }

    [Test]
    public void AnEditOfALockedPart_NamesTheRealUnlockCall()
    {
        MakeElement("Fixed", new Vector3Int(600, 18, 400)).Movable = false;

        var message = Message(_handler!.Handle(MakeReq("edit_elements", new { ops = new[] { new { name = "Fixed", anchor_x_mm = 100f } } })));

        StringAssert.Contains("LOCKED", message);
        StringAssert.Contains("edit_elements {ops:[{name:'Fixed', locked:false}]}", message);
        StringAssert.DoesNotContain("set_element_lock", message);
    }

    [Test]
    public void AlignWithFacesOnDifferentAxes_SaysWhichTargetFaceFits()
    {
        MakeElement("A", new Vector3Int(500, 400, 18));
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var message = Message(_handler!.Handle(MakeReq("align_elements", new
        {
            ops = new[] { new { name = "A", face = "left", target = "B", target_face = "top" } }
        })));

        StringAssert.Contains("target_face 'right'", message);
    }

    [Test]
    public void AlignWithAnUnknownFace_ListsTheValidWords()
    {
        MakeElement("A", new Vector3Int(500, 400, 18));
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var message = Message(_handler!.Handle(MakeReq("align_elements", new
        {
            ops = new[] { new { name = "A", face = "inside", target = "B", target_face = "right" } }
        })));

        StringAssert.Contains("Valid faces: left|right (X), bottom|top (Y), back|front (Z)", message);
    }

    [Test]
    public void EveryBatchRefusal_EndsWithTheResendAllReminder()
    {
        var refusals = new[]
        {
            _handler!.Handle(MakeReq("edit_elements", new { ops = new[] { new { name = "Nope", locked = false } } })),
            _handler.Handle(MakeReq("delete_elements", new { names = new[] { "Nope" } })),
            _handler.Handle(MakeReq("clone_elements", new { ops = new[] { new { name = "Nope", count = 1 } } })),
            _handler.Handle(MakeReq("convert_elements", new { ops = new[] { new { name = "Nope", target = "facade" } } })),
            _handler.Handle(MakeReq("distribute_evenly", new { names = new[] { "N1", "N2", "N3" }, axis = "x" })),
        };

        foreach (var refusal in refusals)
        {
            Assert.AreEqual("error", refusal.type);
            StringAssert.Contains("resend the WHOLE batch", Message(refusal));
        }
    }
}
