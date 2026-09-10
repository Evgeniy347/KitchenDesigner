using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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
            hasDims = true,
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
    /// алфавит даёт Kilograms → Pieces, объявление — Pieces (0) → Kilograms (4).
    ///
    /// «Итого» теперь строится по totalsBySection (пара раздел+единица, дефект приёмки №1) —
    /// обе строки нарочно в ОДНОМ разделе, чтобы порядок внутри раздела остался именно
    /// проверкой сортировки по единице, а не побочным эффектом сортировки по разделу.</summary>
    [Test]
    public void ToCsv_TotalsBySectionRows_OrderedByEnumDeclarationNotAlphabet()
    {
        var result = new SpecResult
        {
            lines = new List<SpecLine>(),
            totalsBySection = new Dictionary<(string, SpecUnit), float>
            {
                { ("Мебель", SpecUnit.Kilograms), 10f },
                { ("Мебель", SpecUnit.Pieces), 5f },
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

    /// <summary>Дефект приёмки №1: «Итого, м = 136,60» в эталоне складывало кромку (Мебель) и
    /// трубу (Сантехника) в одну кучу. totalsBySection обязан развести одинаковую единицу по
    /// разным разделам — противоположный вход к тесту выше, где раздел был один и тот же.</summary>
    [Test]
    public void ToCsv_TotalsBySection_DifferentSectionsSameUnit_AreNotMerged()
    {
        var result = new SpecResult
        {
            lines = new List<SpecLine>(),
            totalsBySection = new Dictionary<(string, SpecUnit), float>
            {
                { ("Мебель", SpecUnit.LinearMeters), 136.23f },
                { ("Сантехника", SpecUnit.LinearMeters), 0.37f },
            },
        };

        var csv = SpecificationExport.ToCsv(result);
        var rows = csv.Replace("\r\n", "\n").Trim('\n').Split('\n');
        var totalRows = rows.Where(r => r.StartsWith("Итого;")).ToArray();

        Assert.AreEqual(2, totalRows.Length, "раздел «Мебель» и раздел «Сантехника» — две отдельные строки «Итого»");
        Assert.IsTrue(csv.Contains("136,2300"), "метраж кромки не должен слиться с метражом трубы");
        Assert.IsTrue(csv.Contains("0,3700"), "метраж трубы остаётся собственным числом");
        Assert.IsFalse(csv.Contains("136,6000"), "сумма 136,23+0,37, которая никому не нужна, не должна появиться");
    }

    /// <summary>Формат зафиксирован на ru-RU (запятая), а не на культуре машины прогона.
    /// Тест намеренно выставляет культуру потока в en-US (точка) ПЕРЕД вызовом — если
    /// `ToCsv` вернётся к неявному `IFormatProvider` (берущему культуру потока), дробная
    /// часть выйдет через точку и тест покраснеет независимо от локали машины, на которой
    /// он реально запущен.</summary>
    [Test]
    public void ToCsv_FractionalNumbers_UseCommaRegardlessOfThreadCulture()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            var csv = SpecificationExport.ToCsv(MakeResult("Board"));
            var dataRow = csv.Replace("\r\n", "\n").Split('\n')[1].Split(';');

            StringAssert.Contains(",", dataRow[5], "AreaPerBoard_m2 обязан выйти с запятой (ru-RU), не точкой");
            StringAssert.Contains(",", dataRow[6], "TotalArea_m2 обязан выйти с запятой (ru-RU), не точкой");
            StringAssert.DoesNotContain(".", dataRow[5]);
            StringAssert.DoesNotContain(".", dataRow[6]);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    /// <summary>Обратный вход: целые количества (Count) не должны обрасти разделителем
    /// разрядов ru-RU (пробел/неразрывный пробел) — только дробные поля идут через
    /// `NumberCulture`, целые остаются как есть.</summary>
    [Test]
    public void ToCsv_IntegerCount_HasNoThousandsSeparator()
    {
        var result = MakeResult("Board");
        var line = result.lines[0];
        line.count = 12345;
        result.lines[0] = line;

        var csv = SpecificationExport.ToCsv(result);
        var dataRow = csv.Replace("\r\n", "\n").Split('\n')[1].Split(';');

        Assert.AreEqual("12345", dataRow[4]);
    }
}
