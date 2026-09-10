using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.Plumbing;

/// <summary>get_specification отстало от export_specification_csv: CSV уже умеет
/// раздел/единицу/qty на любую единицу измерения (SpecificationManager.cs,
/// SpecificationExport.ToCsv), а MCP отдавал только name/dims/count/areaPerBoardM2/
/// totalAreaM2 - для позиции без квадратных метров (труба, а завтра бетон в м3 или
/// арматура в кг) это молча превращалось в "0x0x0, area 0". Два теста ниже стерегут
/// это в двух направлениях: числа CSV и MCP обязаны совпасть на одной сцене, и
/// SpecLineInfo обязана нести каждое поле SpecLine - иначе где-нибудь опять забудут
/// колонку.</summary>
public class McpSpecificationParityTests : McpTestFixture
{
    [SetUp]
    public void SuppressPipeMeshRebuild() => KitchenElement.SuppressVisualRebuild = true;

    [TearDown]
    public void RestoreMeshRebuild() => KitchenElement.SuppressVisualRebuild = false;

    private PipeElement MakePipe(string name, Vector3 pos, string sizeId, int lengthMm)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<PipeElement>();
        e.PartName = name;
        e.SizeId = sizeId;
        e.LengthMM = lengthMm;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    private static JArray Lines(McpResponse resp) => (JArray)JObject.FromObject(resp.data!)["lines"]!;

    private static string[][] ParseCsvLines(string csv, int expectedRowCount)
    {
        var rows = csv.Replace("\r\n", "\n").Trim('\n').Split('\n');
        // header, N data rows, blank separator, "Total" row, one row per unit.
        return rows.Skip(1).Take(expectedRowCount).Select(r => r.Split(';')).ToArray();
    }

    // SpecificationExport.ToCsv formats fractional numbers with the fixed
    // SpecificationExport.NumberCulture (ru-RU, comma decimal separator), independent of
    // whatever culture happens to be active on the machine running the test. Parse with
    // that same constant - not CultureInfo.CurrentCulture - so the test is independent of
    // the runner's locale, and so it still fails if the export's number format ever changes.
    private static float ParseCsvFloat(string cell) => float.Parse(cell, NumberStyles.Float, SpecificationExport.NumberCulture);

    [Test]
    public void GetSpecification_MatchesCsvExport_SameSceneSameNumbers()
    {
        MakeElement("BoardA", new Vector3Int(600, 400, 18), Vector3.zero);
        MakeElement("BoardB", new Vector3Int(300, 250, 16), new Vector3(2f, 0f, 0f));
        MakePipe("PipeA", new Vector3(4f, 0f, 0f), PipeSpec.Dn25, 1234);

        var built = SpecificationManager.Build(PartRegistry.GetAll());
        string csv = SpecificationExport.ToCsv(built);

        var resp = _handler!.Handle(MakeReq("get_specification", new { }));
        var mcpLines = Lines(resp);
        Assert.AreEqual(4, mcpLines.Count,
            "две доски + труба - но строк четыре, не три: обе доски по умолчанию несут "
            + "кромку (PartData._edgeBanding = true), и на изолированной детали без соседей "
            + "открыты все 4 стороны, так что EdgeColumns.For находит кромку на каждой из "
            + "них. AddEdgeBandingItem группирует эти стороны по толщине кромки в ОДНУ "
            + "строку ведомости (EdgeBandingSpecItems.For), общую на обе доски, - в погонных "
            + "метрах, section \"Кромка\". Итого: BoardA, BoardB, PipeA и эта общая строка "
            + "кромки.");

        var csvRows = ParseCsvLines(csv, mcpLines.Count);
        Assert.AreEqual(mcpLines.Count, csvRows.Length, "CSV и MCP должны видеть одинаковое число строк");

        for (int i = 0; i < mcpLines.Count; i++)
        {
            var line = mcpLines[i]!;
            var row = csvRows[i];
            bool hasDims = line["hasDims"]!.Value<bool>();

            Assert.AreEqual(row[0], line["name"]!.Value<string>(), $"name разошлось на строке {i}");
            // Без габаритов CSV печатает пустые ячейки, а не нули (дефект приёмки №3) - сравнивать
            // как числа тут нечего, само отсутствие значения и есть проверяемое поведение.
            if (hasDims)
            {
                Assert.AreEqual(int.Parse(row[1], CultureInfo.InvariantCulture), line["dimXMm"]!.Value<int>(), $"dimXMm на строке {i}");
                Assert.AreEqual(int.Parse(row[2], CultureInfo.InvariantCulture), line["dimYMm"]!.Value<int>(), $"dimYMm на строке {i}");
                Assert.AreEqual(int.Parse(row[3], CultureInfo.InvariantCulture), line["dimZMm"]!.Value<int>(), $"dimZMm на строке {i}");
                Assert.AreEqual(int.Parse(row[4], CultureInfo.InvariantCulture), line["count"]!.Value<int>(), $"count на строке {i}");
                Assert.AreEqual(ParseCsvFloat(row[5]), line["areaPerBoardM2"]!.Value<float>(), 1e-3f, $"areaPerBoardM2 на строке {i}");
                Assert.AreEqual(ParseCsvFloat(row[6]), line["totalAreaM2"]!.Value<float>(), 1e-3f, $"totalAreaM2 на строке {i}");
                Assert.AreEqual(ParseCsvFloat(row[15]), line["qtyPerItemInUnit"]!.Value<float>(), 1e-3f, $"qtyPerItemInUnit на строке {i}");
            }
            else
            {
                Assert.AreEqual("", row[1], $"dimXMm пуст без габаритов на строке {i}");
                Assert.AreEqual("", row[2], $"dimYMm пуст без габаритов на строке {i}");
                Assert.AreEqual("", row[3], $"dimZMm пуст без габаритов на строке {i}");
                Assert.AreEqual("", row[4], $"count пуст без габаритов на строке {i}");
                Assert.AreEqual("", row[5], $"areaPerBoardM2 пуст без габаритов на строке {i}");
                Assert.AreEqual("", row[6], $"totalAreaM2 пуст без габаритов на строке {i}");
                Assert.AreEqual("", row[15], $"qtyPerItemInUnit пуст без габаритов на строке {i}");
            }
            Assert.AreEqual(row[7], line["material"]!.Value<string>(), $"material на строке {i}");
            Assert.AreEqual(row[8], line["grooves"]!.Value<string>(), $"grooves на строке {i}");
            Assert.AreEqual(row[9], line["edgeL1"]!.Value<string>(), $"edgeL1 на строке {i}");
            Assert.AreEqual(row[10], line["edgeL2"]!.Value<string>(), $"edgeL2 на строке {i}");
            Assert.AreEqual(row[11], line["edgeW1"]!.Value<string>(), $"edgeW1 на строке {i}");
            Assert.AreEqual(row[12], line["edgeW2"]!.Value<string>(), $"edgeW2 на строке {i}");
            Assert.AreEqual(row[13], line["section"]!.Value<string>(), $"section на строке {i}");
            Assert.AreEqual(row[14], line["unit"]!.Value<string>(), $"unit на строке {i}");
            Assert.AreEqual(ParseCsvFloat(row[16]), line["qtyTotalInUnit"]!.Value<float>(), 1e-3f, $"qtyTotalInUnit на строке {i}");
        }

        var pipeLine = mcpLines.Single(l => l!["name"]!.Value<string>()!.StartsWith("Труба"));
        Assert.IsFalse(pipeLine!["hasDims"]!.Value<bool>(), "у трубы нет физических габаритов - dims не значат 0x0x0");
        Assert.AreEqual(SpecSections.Plumbing, pipeLine["section"]!.Value<string>());
        Assert.AreEqual("м", pipeLine["unit"]!.Value<string>());
        Assert.AreEqual(1.234f, pipeLine["qtyTotalInUnit"]!.Value<float>(), 1e-3f);

        var boardLine = mcpLines.First(l => l!["name"]!.Value<string>() == "BoardA");
        Assert.IsTrue(boardLine!["hasDims"]!.Value<bool>());

        var data = JObject.FromObject(resp.data!);
        var totalsByUnit = (JObject)data["totalsByUnit"]!;
        Assert.AreEqual(built.totalsByUnit![SpecUnit.AreaM2], totalsByUnit["м²"]!.Value<float>(), 1e-3f,
            "итог по м² должен совпасть с тем, что CSV печатает строкой \"Итого\"");
        Assert.AreEqual(built.totalsByUnit![SpecUnit.LinearMeters], totalsByUnit["м"]!.Value<float>(), 1e-3f,
            "итог по погонным метрам должен совпасть с CSV, а не потеряться");
    }

    [Test]
    public void SpecLineInfo_CarriesEveryFieldOfSpecLine_NoneForgotten()
    {
        var sourceFields = typeof(SpecLine).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(f => f.Name)
            .Where(n => n != nameof(SpecLine.dimensionsMM)) // раскладывается в dimXMm/dimYMm/dimZMm ниже
            .Where(n => n != nameof(SpecLine.qtyPerItem) && n != nameof(SpecLine.qtyTotal))
            // переименованы в qtyPerItemInUnit/qtyTotalInUnit ниже: единица переменная
            // (шт/м/м²/м³/кг) и живёт в соседнем поле unit, суффикс InUnit честно на это указывает
            .Where(n => n != nameof(SpecLine.isBoardArea))
            // internal accumulation flag (только для totalCount/totalAreaM2 внутри Build) — не
            // потребительское поле ведомости, агенту MCP оно не нужно
            .ToArray();
        var mcpFields = typeof(SpecLineInfo).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(f => f.Name)
            .ToHashSet();

        var missing = sourceFields.Where(n => !mcpFields.Contains(n)).ToArray();
        Assert.IsEmpty(missing,
            "SpecLineInfo (ответ get_specification) не несёт поле(я) SpecLine: " +
            string.Join(", ", missing) +
            " - агент на другой стороне MCP увидит эту позицию неполной и молча.");

        Assert.IsTrue(mcpFields.Contains(nameof(SpecLineInfo.dimXMm))
            && mcpFields.Contains(nameof(SpecLineInfo.dimYMm))
            && mcpFields.Contains(nameof(SpecLineInfo.dimZMm)),
            "dimensionsMM должен быть разложен в dimXMm/dimYMm/dimZMm");

        Assert.IsTrue(mcpFields.Contains(nameof(SpecLineInfo.qtyPerItemInUnit))
            && mcpFields.Contains(nameof(SpecLineInfo.qtyTotalInUnit)),
            "SpecLine.qtyPerItem/qtyTotal должны дойти как qtyPerItemInUnit/qtyTotalInUnit");
    }
}
