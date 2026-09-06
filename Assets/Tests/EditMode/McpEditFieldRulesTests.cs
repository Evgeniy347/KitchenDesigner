using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>Какое поле edit_elements какой тип элемента принимает. Таблица
/// EditFieldRules заменила лестницу `if (el is X)`, и эти тесты держат её
/// частные случаи — те, что раньше объяснялись комментариями в исходнике.</summary>
public class McpEditFieldRulesTests : McpTestFixture
{
    private static string ErrorMessage(McpResponse resp) =>
        JObject.FromObject(resp.data!)["message"]!.Value<string>()!;

    [Test]
    public void EditElements_EdgeBandingTogetherWithAResize_IsAcceptedOnABarThatBecomesASheet()
    {
        var bar = MakeElement("Bar", new Vector3Int(18, 18, 800));
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
        MakeElement("Shelf", new Vector3Int(800, 18, 400));

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
    public void EditElements_DrawerSystemOnAPlainBoard_IsRejectedInItsContractPosition()
    {
        MakeElement("Shelf", new Vector3Int(800, 18, 400));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[]
            {
                new { name = "Shelf", corner_radius = 50, drawer_system = "movento", drawer_type = "B", leg_inset_mm = 30 }
            }
        }));

        Assert.AreEqual("error", resp.type,
            "смена системы выдвижения у элемента, который её не поддерживает, раньше молча " +
            "проглатывалась: клиент получал ok и считал правку применённой");
        Assert.AreEqual(
            "edit_elements rejected, NOTHING was applied: "
            + "Invalid field for 'Shelf': corner_radius | "
            + "Invalid field for 'Shelf': drawer_system | "
            + "Invalid field for 'Shelf': drawer_type | "
            + "Invalid field for 'Shelf': leg_inset_mm",
            ErrorMessage(resp),
            "порядок склеенных сообщений — часть контракта: drawer_system открывает блок ящика, " +
            "ровно как и в EditOp, и стоит между corner_radius и drawer_type");
    }

    [Test]
    public void EditElements_DrawerSystemOnADrawer_SwitchesTheRunnerSystem()
    {
        var created = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[] { new { name = "Box", type = "drawer", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } }
        }));
        Assert.AreEqual("result", created.type, created.type == "error" ? ErrorMessage(created) : "");
        var drawer = PartRegistry.GetAll().Find(e => e.PartName == "Box") as DrawerElement;
        Assert.NotNull(drawer);
        Assert.AreEqual(DrawerSystem.Gtv, drawer!.System, "type=drawer создаёт ящик GTV");

        var resp = _handler.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Box", drawer_system = "movento" } }
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        Assert.AreEqual(DrawerSystem.Movento, drawer.System,
            "у ящика поле принимается — отказ выше касается только элементов без системы выдвижения");
    }

    [Test]
    public void EditElements_RejectedBatch_ReportsEveryBadFieldAtOnce()
    {
        MakeElement("Shelf", new Vector3Int(800, 18, 400));

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

    /// <summary>Заход в корпус перестал быть вводимым в панели — значит перестаёт
    /// быть вводимым и у агента, тем же изменением: поле, закрытое с одной
    /// стороны, разводит две поверхности над одним свойством
    /// (McpUiPropertyParityTests). Так же поступили с attached_to_name у опоры.
    /// Отказ приходит на ЛЮБОМ элементе, включая саму опору, — величина
    /// измеряется, а не выбирается.</summary>
    [Test]
    public void EditElements_ScrewInsertion_IsRejectedEvenOnAScrewLeg()
    {
        var go = ElementFactory.CreateScrewLeg("Opora", Vector3.zero);
        _spawned.Add(go);
        var leg = go.GetComponent<ScrewLegElement>();
        int before = leg.InsertionDepthMM;

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = leg.PartName, screw_insertion_mm = 40 } }
        }));

        Assert.AreEqual("error", resp.type, "поле выведенное — принимать его нечем");
        StringAssert.Contains("screw_insertion_mm", ErrorMessage(resp),
            "отказ обязан назвать поле: клиент читает список полей, а не догадывается");
        Assert.AreEqual(before, leg.InsertionDepthMM,
            "и ничего не применилось — иначе отказ был бы только на словах");
    }

    [Test]
    public void EditElements_ScrewThreadLength_IsStillAccepted()
    {
        var go = ElementFactory.CreateScrewLeg("Opora", Vector3.zero);
        _spawned.Add(go);
        var leg = go.GetComponent<ScrewLegElement>();

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = leg.PartName, screw_thread_length_mm = 70 } }
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        Assert.AreEqual(70, leg.ThreadLengthMM,
            "контроль: у опоры отклонено ОДНО поле, а не весь её набор");
    }

    [Test]
    public void EditElements_PillarDiameter_OnAnythingElse_IsRejected()
    {
        MakeElement("Shelf", new Vector3Int(800, 18, 400));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Shelf", diameter_mm = 120 } }
        }));

        Assert.AreEqual("error", resp.type);
        StringAssert.Contains("diameter_mm", ErrorMessage(resp),
            "диаметр есть только у опоры: у доски ширина и глубина независимы");
    }
}
