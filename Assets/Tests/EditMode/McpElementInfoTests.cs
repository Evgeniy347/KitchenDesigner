using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>ElementInfoBuilder и разбор нарушений: то, что MCP ОТДАЁТ про
/// элемент. Здесь живут причины, стоявшие комментариями в
/// McpCommandHandler.Helpers.</summary>
public class McpElementInfoTests : McpTestFixture
{
    private static JObject Data(McpResponse resp) => JObject.FromObject(resp.data!);

    private string SeverityOfOverlapBetweenTwoBoardsPenetratingBy(float penetrationMm)
    {
        float centreDistanceM = (500f - penetrationMm) * AppConstants.MM_TO_UNITS;
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(centreDistanceM, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "A", locked = false } }
        }));
        var violations = (JArray)Data(resp)["results"]![0]!["violations"]!;
        Assert.AreEqual(1, violations.Count, "ровно одно пересечение — с B");
        Assert.AreEqual("overlap", violations[0]!["kind"]!.Value<string>());
        return violations[0]!["severity"]!.Value<string>()!;
    }

    [Test]
    public void Violations_OneMillimetreOfPenetration_IsMinorOverlap()
    {
        Assert.AreEqual("minor_overlap", SeverityOfOverlapBetweenTwoBoardsPenetratingBy(1f),
            "меньше 2 мм — это перекрытие «на волосок»: агенту его чинить не срочно");
    }

    [Test]
    public void Violations_FiveMillimetresOfPenetration_IsPlainOverlap()
    {
        Assert.AreEqual("overlap", SeverityOfOverlapBetweenTwoBoardsPenetratingBy(5f));
    }

    [Test]
    public void Violations_TwelveMillimetresOfPenetration_IsDeepPenetration()
    {
        Assert.AreEqual("deep_penetration", SeverityOfOverlapBetweenTwoBoardsPenetratingBy(12f),
            "от 10 мм деталь сидит в соседе всерьёз — это не дребезг координат, а ошибка расстановки");
    }

    [Test]
    public void Violations_SubMillimetreContact_IsNotReportedAtAll()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18),
            new Vector3((500f - 0.2f) * AppConstants.MM_TO_UNITS, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "A", locked = false } }
        }));

        var violations = (JArray)Data(resp)["results"]![0]!["violations"]!;
        Assert.AreEqual(0, violations.Count,
            "0,2 мм — шум координат, а не пересечение: иначе каждая стыкованная пара кричала бы об ошибке");
    }

    /// <summary>Правку захода агенту закрыли вместе с панелью, но ЧИТАТЬ его он
    /// обязан — и читать то же число, что показывает панель. Пара с тестом ниже:
    /// без хозяина мерить нечего, и ноль там читается по пустому hostName.</summary>
    [Test]
    public void GetElements_ScrewLeg_ReportsTheMeasuredInsertion_NotTheStoredField()
    {
        MakeElement("Tsarga", new Vector3Int(482, 80, 16), new Vector3(0f, 0.060f, 0f));
        var go = ElementFactory.CreateScrewLeg("Opora", new Vector3(0f, 0.029f, 0f));
        _spawned.Add(go);
        var leg = go.GetComponent<ScrewLegElement>();
        ScrewLegHostLink.Apply(leg, PartRegistry.GetAll());

        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { leg.PartName } }));
        var info = Data(resp)["elements"]![0]!["screwLeg"]!;

        Assert.AreEqual("Tsarga", info["hostName"]!.Value<string>(), "предусловие: хозяин вывелся");
        Assert.AreEqual(38, info["insertionMM"]!.Value<int>(),
            "резьба кончается на 58 мм, дно царги — на 20: внутри 38, а не заводские 25");
    }

    [Test]
    public void GetElements_ScrewLegWithNoHost_ReportsZeroInsertionBesideAnEmptyHost()
    {
        var go = ElementFactory.CreateScrewLeg("Odna", new Vector3(0f, 0.029f, 0f));
        _spawned.Add(go);
        var leg = go.GetComponent<ScrewLegElement>();
        ScrewLegHostLink.Apply(leg, PartRegistry.GetAll());

        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { leg.PartName } }));
        var info = Data(resp)["elements"]![0]!["screwLeg"]!;

        Assert.AreEqual("", info["hostName"]!.Value<string>(), "хозяина нет");
        Assert.AreEqual(0, info["insertionMM"]!.Value<int>(),
            "ноль здесь читается ТОЛЬКО в паре с пустым hostName — сам по себе он "
            + "неотличим от «вошла на 0 мм», и в панели вместо него стоит прочерк");
    }

    [Test]
    public void GetElements_DishwasherWithoutAFacade_ReportsZeroPlinth()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[] { new { name = "DW-bare", type = "dishwasher", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f } }
        }));

        var resp = _handler.Handle(MakeReq("get_elements", new { names = new[] { "DW-bare" } }));
        var info = Data(resp)["elements"]![0]!["dishwasher"]!;

        Assert.AreEqual(0, info["plinthMM"]!.Value<int>(),
            "высота цоколя считается по ПРИСТЁГНУТОМУ фасаду; без фасада она ещё не определена, " +
            "и выдавать номинал было бы враньём");
    }
}
