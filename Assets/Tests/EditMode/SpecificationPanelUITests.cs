using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class SpecificationPanelUITests
{
    private static SpecLine BoardLine(string name, Vector3Int dims, string material, string section,
        int count, float qtyTotal) => new SpecLine
    {
        name = name,
        dimensionsMM = dims,
        hasDims = true,
        material = material,
        section = section,
        unit = SpecUnit.AreaM2,
        count = count,
        qtyPerItem = qtyTotal / System.Math.Max(count, 1),
        qtyTotal = qtyTotal,
        areaPerBoardM2 = qtyTotal / System.Math.Max(count, 1),
        totalAreaM2 = qtyTotal,
    };

    private static SpecLine ItemLine(string name, string material, string section, SpecUnit unit,
        int sourceRowCount, float qtyTotal) => new SpecLine
    {
        name = name,
        hasDims = false,
        material = material,
        section = section,
        unit = unit,
        count = sourceRowCount,
        qtyTotal = qtyTotal,
    };

    private static SpecResult Result(params SpecLine[] lines)
    {
        var list = new List<SpecLine>(lines);
        var result = new SpecResult
        {
            lines = list,
            totalCount = list.Where(l => l.unit == SpecUnit.AreaM2).Sum(l => l.count),
        };
        result.totalsByUnit = SpecTotals.ByUnit(list.Select(l => (l.unit, l.qtyTotal)));
        return result;
    }

    private static string CellAt(string row, float fromPos, float toPos)
    {
        string startTag = $"<pos={fromPos}>";
        int start = row.IndexOf(startTag, System.StringComparison.Ordinal);
        Assert.Greater(start, -1, $"тег {startTag} не найден в строке: {row}");
        start += startTag.Length;
        string endTag = $"<pos={toPos}>";
        int end = row.IndexOf(endTag, start, System.StringComparison.Ordinal);
        Assert.Greater(end, -1, $"тег {endTag} не найден в строке: {row}");
        return row.Substring(start, end - start);
    }

    private static string[] Lines(string text) =>
        text.Replace("\r\n", "\n").Split('\n');

    private static string TailAt(string row, float fromPos)
    {
        string startTag = $"<pos={fromPos}>";
        int start = row.IndexOf(startTag, System.StringComparison.Ordinal);
        Assert.Greater(start, -1, $"тег {startTag} не найден в строке: {row}");
        return row.Substring(start + startTag.Length);
    }

    // ── Требование 2: hasDims решает, печатать габариты или нет ──

    [Test]
    public void BuildDisplayText_LineWithDims_PrintsWidthHeightDepth()
    {
        var result = Result(BoardLine("Полка", new Vector3Int(800, 400, 18), "ЛДСП", SpecSections.Furniture,
            1, 0.68f));

        var row = Lines(SpecificationPanelUI.BuildDisplayText(result))
            .Single(l => l.Contains("Полка"));

        Assert.AreEqual("800", CellAt(row, SpecificationPanelUI.ColW, SpecificationPanelUI.ColH));
        Assert.AreEqual("400", CellAt(row, SpecificationPanelUI.ColH, SpecificationPanelUI.ColD));
        Assert.AreEqual("18", CellAt(row, SpecificationPanelUI.ColD, SpecificationPanelUI.ColMaterial));
    }

    [Test]
    public void BuildDisplayText_LineWithoutDims_LeavesDimensionColumnsBlank_NotZero()
    {
        var result = Result(ItemLine("Кирпич", "Керамика", "Стены", SpecUnit.Pieces, 3, 3720f));

        var row = Lines(SpecificationPanelUI.BuildDisplayText(result))
            .Single(l => l.Contains("Кирпич"));

        Assert.AreEqual("", CellAt(row, SpecificationPanelUI.ColW, SpecificationPanelUI.ColH),
            "hasDims=false — колонка Ш обязана быть пустой, а не напечатанным нулём");
        Assert.AreEqual("", CellAt(row, SpecificationPanelUI.ColH, SpecificationPanelUI.ColD));
        Assert.AreEqual("", CellAt(row, SpecificationPanelUI.ColD, SpecificationPanelUI.ColMaterial));
    }

    // ── Требование 4: «Дет.» — число деталей у строки с габаритами, ничего — у строки без ──

    [Test]
    public void BuildDisplayText_BoardLine_ShowsPieceCountInPiecesColumn()
    {
        var result = Result(BoardLine("Дно", new Vector3Int(600, 500, 18), "Белый (GTV)", SpecSections.Furniture,
            2, 1.24f));

        var row = Lines(SpecificationPanelUI.BuildDisplayText(result)).Single(l => l.Contains("Дно"));

        Assert.AreEqual("2", CellAt(row, SpecificationPanelUI.ColPieces, SpecificationPanelUI.ColQty),
            "колонка «Дет.» обязана показать число физических деталей (2 доски), а не м²");
    }

    [Test]
    public void BuildDisplayText_LineWithoutDims_LeavesPiecesColumnBlank_NotSourceRowCount()
    {
        // Строка без габаритов не считается «деталями» — противоположный вход к тесту выше:
        // здесь source-row-count (3) не имеет права всплыть в колонке «Дет.».
        var result = Result(ItemLine("Кирпич", "Керамика", "Стены", SpecUnit.Pieces, 3, 3720f));

        var row = Lines(SpecificationPanelUI.BuildDisplayText(result)).Single(l => l.Contains("Кирпич"));

        Assert.AreEqual("", CellAt(row, SpecificationPanelUI.ColPieces, SpecificationPanelUI.ColQty),
            "hasDims=false — колонка «Дет.» обязана быть пустой, а не числом строк-источников");
    }

    // ── Требование 3: «Кол-во» — всегда общее количество в собственной единице строки ──

    [Test]
    public void BuildDisplayText_PiecesLine_ShowsTotalPieces_NotTheSourceRowCount()
    {
        // Ровно баг из отчёта приёмки: 3 строки-источника сложились в 3720 кирпичей.
        var result = Result(ItemLine("Кирпич", "Керамика", "Стены", SpecUnit.Pieces, 3, 3720f));

        var row = Lines(SpecificationPanelUI.BuildDisplayText(result))
            .Single(l => l.Contains("Кирпич"));

        Assert.AreEqual("3720", CellAt(row, SpecificationPanelUI.ColQty, SpecificationPanelUI.ColUnit),
            "в колонке «Кол-во» обязано быть 3720 шт, а не 3 строки-источника");
        StringAssert.Contains("шт", row);
    }

    [Test]
    public void BuildDisplayText_BoardLine_QtyColumn_MatchesTotalAreaFromCsv()
    {
        var result = Result(BoardLine("Дно", new Vector3Int(600, 500, 18), "Белый (GTV)", SpecSections.Furniture,
            2, 1.24f));

        var row = Lines(SpecificationPanelUI.BuildDisplayText(result)).Single(l => l.Contains("Дно"));

        Assert.AreEqual(1.24f.ToString("F2"),
            CellAt(row, SpecificationPanelUI.ColQty, SpecificationPanelUI.ColUnit),
            "то же totalAreaM2/qtyTotal, что уходит в CSV — числа окна и CSV не расходятся");
    }

    // ── Требование 1: группировка по разделу — данные строки, а не сортировка по материалу ──

    [Test]
    public void BuildDisplayText_GroupsBySection_NotOnlyByMaterial()
    {
        // "Бетон" (Конструкции) обязан не перемешаться со строками "Мебель", даже если
        // материалы соседних разделов совпали бы по алфавиту.
        var result = Result(
            BoardLine("Полка", new Vector3Int(500, 300, 18), "Дуб", SpecSections.Furniture, 1, 0.3f),
            ItemLine("Кирпич", "Дуб-Керамика", "Конструкции", SpecUnit.Pieces, 1, 500f));

        var lines = Lines(SpecificationPanelUI.BuildDisplayText(result));
        int furnitureHeader = System.Array.FindIndex(lines, l => l.Contains(SpecSections.Furniture));
        int constructionHeader = System.Array.FindIndex(lines, l => l.Contains("Конструкции"));
        int shelfRow = System.Array.FindIndex(lines, l => l.Contains("Полка"));
        int brickRow = System.Array.FindIndex(lines, l => l.Contains("Кирпич"));

        Assert.Greater(furnitureHeader, -1, "заголовок раздела «Мебель» обязан присутствовать");
        Assert.Greater(constructionHeader, -1, "заголовок раздела «Конструкции» обязан присутствовать");
        Assert.AreEqual(furnitureHeader + 1, shelfRow,
            "строка полки идёт СРАЗУ за заголовком своего раздела, а не после чужого");
        Assert.AreEqual(constructionHeader + 1, brickRow,
            "строка кирпича идёт СРАЗУ за заголовком своего раздела, а не после чужого");
    }

    [Test]
    public void BuildDisplayText_SectionHeader_IsNotTruncatedAt10Chars()
    {
        var result = Result(ItemLine("Утеплитель", "Минвата", "Вентиляционная система", SpecUnit.AreaM2, 1, 4f));

        var text = SpecificationPanelUI.BuildDisplayText(result);

        StringAssert.Contains("Вентиляционная система", text,
            "раздел длиннее 10 символов не имеет права обрезаться многоточием");
    }

    // ── Требование 5: итог по материалу ──

    [Test]
    public void BuildDisplayText_MaterialSubtotal_SumsAcrossItsOwnLines()
    {
        var result = Result(
            ItemLine("Фитинг угловой", "", SpecSections.Plumbing, SpecUnit.Pieces, 1, 6f),
            ItemLine("Фитинг тройник", "", SpecSections.Plumbing, SpecUnit.Pieces, 1, 4f));

        var text = SpecificationPanelUI.BuildDisplayText(result);

        StringAssert.Contains("10 шт", text.Replace("\r\n", "\n"),
            "итог по материалу обязан сложить количество всех его строк (6 + 4 = 10)");
    }

    // ── Общий итог по единицам: одна единица и несколько — противоположные входы ──

    [Test]
    public void BuildDisplayText_TotalsByUnit_OneUnitOnly_PrintsOneTotalLine()
    {
        var result = Result(BoardLine("Полка", new Vector3Int(500, 300, 18), "Дуб", SpecSections.Furniture, 1, 0.3f));

        var text = SpecificationPanelUI.BuildDisplayText(result);

        int totalLines = Lines(text).Count(l => l.Contains("Всего, ") && l.Contains("м²"));
        Assert.AreEqual(1, totalLines, "единственная единица — единственная итоговая строка по ней");
    }

    [Test]
    public void BuildDisplayText_TotalsByUnit_SeveralUnits_PrintsOneLinePerUnit()
    {
        var result = Result(
            BoardLine("Полка", new Vector3Int(500, 300, 18), "Дуб", SpecSections.Furniture, 1, 0.3f),
            ItemLine("Кирпич", "Керамика", "Стены", SpecUnit.Pieces, 1, 500f),
            ItemLine("Труба ДН20", "", SpecSections.Plumbing, SpecUnit.LinearMeters, 1, 12.5f));

        var text = SpecificationPanelUI.BuildDisplayText(result);
        string unitTag = $"<pos={SpecificationPanelUI.ColUnit}>";
        var totalRows = Lines(text).Where(l => l.Contains("Всего, ") && l.Contains(unitTag)).ToList();

        Assert.AreEqual(1, totalRows.Count(r => TailAt(r, SpecificationPanelUI.ColUnit) == "м²"),
            "ровно одна итоговая строка по м² (доски)");
        Assert.AreEqual(1, totalRows.Count(r => TailAt(r, SpecificationPanelUI.ColUnit) == "шт"),
            "ровно одна итоговая строка по шт (кирпич)");
        Assert.AreEqual(1, totalRows.Count(r => TailAt(r, SpecificationPanelUI.ColUnit) == "м"),
            "ровно одна итоговая строка по м (труба), не перепутанная с м²");
    }
}
