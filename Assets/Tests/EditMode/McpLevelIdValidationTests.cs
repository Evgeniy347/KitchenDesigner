using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>H7 (review-ui-mcp): MCP принимал ЛЮБУЮ строку в level_id — на create_elements и
/// на edit_elements. ElementInfoBuilder тут же резолвит её через LevelResolution и отдаёт
/// «1» (effectiveLevels[0]), пока седьмой уровень не появится и деталь молча не «переедет».
/// Конвенция MCP-поверхности в остальных местах — «неизвестное значение валит весь батч»
/// (см. Unknown appliance model / Unknown type в McpCreateElementsTests) — level_id обязан
/// вести себя так же.</summary>
public class McpLevelIdValidationTests : McpTestFixture
{
    private static string ErrorMessage(McpResponse resp) =>
        JObject.FromObject(resp.data!)["message"]!.Value<string>()!;

    [SetUp]
    public void LevelsSetUp() => LevelRegistry.Set(new[]
    {
        new Level("1", "1 этаж", 0, 3000),
        new Level("2", "2 этаж", 3000, 3000),
    });

    [TearDown]
    public void LevelsTearDown() => LevelRegistry.Reset();

    [Test]
    public void CreateElements_UnknownLevelId_IsRejected_NothingCreated()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[]
            {
                new { name = "OnUnknownLevel", type = "board", level_id = "7",
                      anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f },
            }
        }));

        Assert.AreEqual("error", resp.type,
            "неизвестный level_id раньше молча создавал деталь, которую ElementInfoBuilder " +
            "тут же резолвил обратно на первый этаж");
        StringAssert.Contains("level_id", ErrorMessage(resp));
        Assert.IsNull(PartRegistry.GetAll().Find(e => e.PartName == "OnUnknownLevel"),
            "батч атомарен — ничего не создано");
    }

    [Test]
    public void CreateElements_KnownLevelId_IsAccepted()
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[]
            {
                new { name = "OnLevel2", type = "board", level_id = "2",
                      anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f },
            }
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        var el = PartRegistry.GetAll().Find(e => e.PartName == "OnLevel2");
        Assert.NotNull(el);
        Assert.AreEqual("2", el!.LevelId);
    }

    [Test]
    public void EditElements_UnknownLevelId_IsRejected_NothingApplied()
    {
        var element = MakeElement("Board", new Vector3Int(600, 18, 500));
        element.LevelId = "1";

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Board", level_id = "does-not-exist" } }
        }));

        Assert.AreEqual("error", resp.type);
        StringAssert.Contains("level_id", ErrorMessage(resp));
        Assert.AreEqual("1", element.LevelId, "отклонённый батч не должен ничего менять");
    }
}
