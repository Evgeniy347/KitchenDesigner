using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

public class SpecificationExportTests
{
    private static SpecResult MakeResult(string name)
    {
        var line = new SpecLine
        {
            name = name,
            dimensionsMM = new Vector3Int(800, 400, 18),
            count = 2,
            areaPerBoardM2 = 0.65f,
            totalAreaM2 = 1.30f,
            qtyPerItem = 0.65f,
            qtyTotal = 1.30f,
            section = "Мебель",
            unit = SpecUnit.AreaM2,
        };
        return new SpecResult
        {
            lines = new List<SpecLine> { line },
            totalCount = 2,
            totalAreaM2 = 1.30f
        };
    }

    [Test]
    public void ToCsv_HasHeaderAndTotalRow()
    {
        var csv = SpecificationExport.ToCsv(MakeResult("Board"));
        var lines = csv.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

        StringAssert.StartsWith("Name;Width_mm;Height_mm;Depth_mm;Count", lines[0]);
        StringAssert.Contains("Board;800;400;18;2", csv);
        StringAssert.StartsWith("Total;;;;2;;", lines[lines.Length - 1]);
    }

    [Test]
    public void ToCsv_HeaderHasSectionAndUnitColumns()
    {
        var header = SpecificationExport.ToCsv(MakeResult("Board"))
            .Replace("\r\n", "\n").Split('\n')[0].Split(';');

        Assert.Contains("Section", header, "раздел обязан стать колонкой, а не подразумеваться");
        Assert.Contains("Unit", header, "единица обязана стать колонкой, а не подразумеваться в заголовке S, м²");
    }

    [Test]
    public void ToCsv_DataRow_CarriesSectionAndUnitLabel()
    {
        var csv = SpecificationExport.ToCsv(MakeResult("Board"));
        var header = csv.Replace("\r\n", "\n").Split('\n')[0].Split(';');
        var dataRow = csv.Replace("\r\n", "\n").Split('\n')[1].Split(';');

        int sectionCol = System.Array.IndexOf(header, "Section");
        int unitCol = System.Array.IndexOf(header, "Unit");

        Assert.AreEqual("Мебель", dataRow[sectionCol]);
        Assert.AreEqual("м²", dataRow[unitCol], "единица строки — та, что объявил элемент, не 'S' из шапки");
    }

    [Test]
    public void ToCsv_EscapesNameContainingSemicolon()
    {
        var csv = SpecificationExport.ToCsv(MakeResult("Left;Right"));
        StringAssert.Contains("\"Left;Right\"", csv);
    }

    [Test]
    public void SaveToFile_WritesCsvFile()
    {
        var path = Path.Combine(Application.temporaryCachePath, "spec_export.csv");
        if (File.Exists(path)) File.Delete(path);

        Assert.IsTrue(SpecificationExport.SaveToFile(MakeResult("Board"), path));
        Assert.IsTrue(File.Exists(path));
        StringAssert.Contains("Board", File.ReadAllText(path));

        File.Delete(path);
    }

    [Test]
    public void SaveToFile_InvalidPath_ReturnsFalse()
    {
        LogAssert.Expect(LogType.Error, new Regex("\\[SpecExport\\] Failed"));
        bool ok = SpecificationExport.SaveToFile(MakeResult("Board"), "Z:\\<>:invalid\\x.csv");
        Assert.IsFalse(ok);
    }

    /// <summary>Дефект из приёмки: строки «Итого» шли в АЛФАВИТНОМ порядке названий единиц
    /// (`u.ToString()` Ordinal), а не в порядке их перечисления в `SpecUnit`. Кг («Kilograms»)
    /// и Шт («Pieces») намеренно выбраны так, чтобы алфавитный и объявленный порядок разошлись:
    /// алфавит даёт Kilograms → Pieces, объявление — Pieces (0) → Kilograms (4).</summary>
    [Test]
    public void ToCsv_TotalsByUnitRows_OrderedByEnumDeclarationNotAlphabet()
    {
        var result = new SpecResult
        {
            lines = new List<SpecLine>(),
            totalsByUnit = new Dictionary<SpecUnit, float>
            {
                { SpecUnit.Kilograms, 10f },
                { SpecUnit.Pieces, 5f },
            },
        };

        var csv = SpecificationExport.ToCsv(result);
        int piecesLine = csv.IndexOf("шт", System.StringComparison.Ordinal);
        int kgLine = csv.IndexOf("кг", System.StringComparison.Ordinal);

        Assert.Greater(piecesLine, -1);
        Assert.Greater(kgLine, -1);
        Assert.Less(piecesLine, kgLine,
            "шт (Pieces=0) обязан идти раньше кг (Kilograms=4) — порядок объявления, не алфавит");
    }
}
