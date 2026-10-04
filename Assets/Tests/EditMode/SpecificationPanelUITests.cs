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
        result.totalsBySection = SpecTotals.BySectionAndUnit(list.Select(l => (l.section, l.unit, l.qtyTotal)));
        return result;
    }

    private static SpecTableModel Model(SpecResult result) => SpecificationRows.Build(result);

    private static DataRow Item(SpecTableModel model, string name) =>
        model.Rows.Single(r => r.Kind == DataRowKind.Item && r.Cell(SpecificationRows.NameColumn) == name);

    private static string Cell(DataRow row, int column) => row.Cell(column);

    // ── Требование 2: hasDims решает, печатать габариты или нет ──

    [Test]
    public void Rows_LineWithDims_PrintsWidthHeightDepth()
    {
        var model = Model(Result(BoardLine("Полка", new Vector3Int(800, 400, 18), "ЛДСП", SpecSections.Furniture,
            1, 0.68f)));

        var row = Item(model, "Полка");

        Assert.AreEqual("800", Cell(row, SpecificationRows.WidthColumn));
        Assert.AreEqual("400", Cell(row, SpecificationRows.HeightColumn));
        Assert.AreEqual("18", Cell(row, SpecificationRows.DepthColumn));
    }

    [Test]
    public void Rows_LineWithoutDims_LeavesDimensionColumnsBlank_NotZero()
    {
        var model = Model(Result(ItemLine("Кирпич", "Керамика", "Стены", SpecUnit.Pieces, 3, 3720f)));

        var row = Item(model, "Кирпич");

        Assert.AreEqual("", Cell(row, SpecificationRows.WidthColumn),
            "hasDims=false — колонка Ш обязана быть пустой, а не напечатанным нулём");
        Assert.AreEqual("", Cell(row, SpecificationRows.HeightColumn));
        Assert.AreEqual("", Cell(row, SpecificationRows.DepthColumn));
    }

    // ── Требование 4: «Дет.» — число деталей у строки с габаритами, ничего — у строки без ──

    [Test]
    public void Rows_BoardLine_ShowsPieceCountInPiecesColumn()
    {
        var model = Model(Result(BoardLine("Дно", new Vector3Int(600, 500, 18), "Белый (GTV)",
            SpecSections.Furniture, 2, 1.24f)));

        Assert.AreEqual("2", Cell(Item(model, "Дно"), SpecificationRows.PiecesColumn),
            "колонка «Дет.» обязана показать число физических деталей (2 доски), а не м²");
    }

    [Test]
    public void Rows_LineWithoutDims_LeavesPiecesColumnBlank_NotSourceRowCount()
    {
        var model = Model(Result(ItemLine("Кирпич", "Керамика", "Стены", SpecUnit.Pieces, 3, 3720f)));

        Assert.AreEqual("", Cell(Item(model, "Кирпич"), SpecificationRows.PiecesColumn),
            "hasDims=false — колонка «Дет.» обязана быть пустой, а не числом строк-источников");
    }

    // ── Требование 3: «Кол-во» — всегда общее количество в собственной единице строки ──

    [Test]
    public void Rows_PiecesLine_ShowsTotalPieces_NotTheSourceRowCount()
    {
        var model = Model(Result(ItemLine("Кирпич", "Керамика", "Стены", SpecUnit.Pieces, 3, 3720f)));

        var row = Item(model, "Кирпич");

        Assert.AreEqual("3720", Cell(row, SpecificationRows.QtyColumn),
            "в колонке «Кол-во» обязано быть 3720 шт, а не 3 строки-источника");
        Assert.AreEqual("шт", Cell(row, SpecificationRows.UnitColumn), "единица — в своей колонке");
    }

    [Test]
    public void Rows_BoardLine_QtyColumn_MatchesTheNumberFormatOfTheUiLanguage()
    {
        var model = Model(Result(BoardLine("Дно", new Vector3Int(600, 500, 18), "Белый (GTV)",
            SpecSections.Furniture, 2, 1.24f)));

        Assert.AreEqual(NumberFormat.Fixed(1.24, 2), Cell(Item(model, "Дно"), SpecificationRows.QtyColumn),
            "дробное количество — через NumberFormat, как все числа окон (десятичный знак языка интерфейса)");
        Assert.AreEqual("м²", Cell(Item(model, "Дно"), SpecificationRows.UnitColumn));
    }

    // ── Требование 1: группировка по разделу — строка-группа, а не сортировка по материалу ──

    [Test]
    public void Rows_GroupBySection_EachSectionStartsWithItsGroupRow()
    {
        var model = Model(Result(
            BoardLine("Полка", new Vector3Int(500, 300, 18), "Дуб", SpecSections.Furniture, 1, 0.3f),
            ItemLine("Кирпич", "Дуб-Керамика", "Конструкции", SpecUnit.Pieces, 1, 500f)));

        var rows = model.Rows.ToList();
        int shelf = rows.IndexOf(Item(model, "Полка"));
        int brick = rows.IndexOf(Item(model, "Кирпич"));
        int furnitureGroup = rows.FindIndex(r => r.Kind == DataRowKind.Group && r.Cell(0) == SpecSections.Furniture);
        int constructionGroup = rows.FindIndex(r => r.Kind == DataRowKind.Group && r.Cell(0) == "Конструкции");

        Assert.AreEqual(2, model.Sections);
        Assert.Greater(furnitureGroup, -1, "строка-группа раздела «Мебель» обязана присутствовать");
        Assert.Greater(constructionGroup, -1, "строка-группа раздела «Конструкции» обязана присутствовать");
        Assert.AreEqual(furnitureGroup + 1, shelf,
            "строка полки идёт СРАЗУ за строкой-группой своего раздела, а не после чужого");
        Assert.AreEqual(constructionGroup + 1, brick,
            "строка кирпича идёт СРАЗУ за строкой-группой своего раздела, а не после чужого");
    }

    [Test]
    public void Rows_SectionHeader_IsNotTruncatedAt10Chars()
    {
        var model = Model(Result(ItemLine("Утеплитель", "Минвата", "Вентиляционная система", SpecUnit.AreaM2, 1, 4f)));

        Assert.IsTrue(model.Rows.Any(r => r.Kind == DataRowKind.Group && r.Cell(0) == "Вентиляционная система"),
            "раздел длиннее 10 символов не имеет права обрезаться: многоточие делает таблица");
    }

    [Test]
    public void Rows_NumberingContinuesAcrossSections_AndCountsOnlyItems()
    {
        var model = Model(Result(
            BoardLine("Полка", new Vector3Int(500, 300, 18), "Дуб", SpecSections.Furniture, 1, 0.3f),
            BoardLine("Дно", new Vector3Int(500, 300, 18), "Дуб", SpecSections.Furniture, 1, 0.3f),
            ItemLine("Кирпич", "Керамика", "Стены", SpecUnit.Pieces, 1, 500f)));

        var numbers = model.Rows.Where(r => r.Kind == DataRowKind.Item)
            .Select(r => Cell(r, SpecificationRows.NumberColumn)).ToList();

        CollectionAssert.AreEqual(new[] { "1", "2", "3" }, numbers, "сквозная нумерация позиций, как в макете");
        Assert.AreEqual(3, model.Positions, "подытоги и итоги не позиции");
    }

    // ── Требование 5: подытог по материалу — строка под группой материала, число в колонке «Кол-во» ──

    [Test]
    public void Rows_MaterialSubtotal_SumsItsOwnLines_AndSitsInTheQtyColumn()
    {
        var model = Model(Result(
            ItemLine("Фитинг угловой", "Латунь", SpecSections.Plumbing, SpecUnit.Pieces, 1, 6f),
            ItemLine("Фитинг тройник", "Латунь", SpecSections.Plumbing, SpecUnit.Pieces, 1, 4f)));

        var subtotal = model.Rows.Single(r => r.Kind == DataRowKind.Subtotal);

        Assert.AreEqual("10", Cell(subtotal, SpecificationRows.QtyColumn),
            "итог по материалу обязан сложить количество всех его строк (6 + 4 = 10)");
        Assert.AreEqual("шт", Cell(subtotal, SpecificationRows.UnitColumn));
        StringAssert.Contains("Латунь", Cell(subtotal, SpecificationRows.NameColumn));
        Assert.AreEqual("", Cell(subtotal, SpecificationRows.MaterialColumn),
            "подытог стоит в колонке «Наименование», а не «Материал» (аудит: «подытоги в колонке Материал»)");
    }

    [Test]
    public void Rows_LineWithoutAMaterial_ShowsADash_AndHasNoSubtotal()
    {
        var model = Model(Result(ItemLine("Фитинг", "", SpecSections.Plumbing, SpecUnit.Pieces, 1, 6f)));

        Assert.AreEqual(UIStyle.GlyphDash, Cell(Item(model, "Фитинг"), SpecificationRows.MaterialColumn));
        Assert.IsFalse(model.Rows.Any(r => r.Kind == DataRowKind.Subtotal),
            "итог «без материала» ничего не сообщает — подытога у безматериальных строк нет");
    }

    [Test]
    public void Rows_MaterialWithSeveralUnits_GetsOneSubtotalPerUnit()
    {
        var model = Model(Result(
            BoardLine("Полка", new Vector3Int(500, 300, 18), "Дуб", SpecSections.Furniture, 1, 0.3f),
            ItemLine("Кромка", "Дуб", SpecSections.Furniture, SpecUnit.LinearMeters, 1, 4.5f)));

        var units = model.Rows.Where(r => r.Kind == DataRowKind.Subtotal)
            .Select(r => Cell(r, SpecificationRows.UnitColumn)).ToList();

        CollectionAssert.AreEquivalent(new[] { "м²", "м" }, units,
            "у одного материала две единицы — две строки подытога, а не «0,3 м², 4,5 м» в одной ячейке");
    }

    // ── Общий итог по единицам: одна единица и несколько — противоположные входы ──

    [Test]
    public void Rows_TotalsByUnit_OneUnitOnly_PrintsOneSectionTotalRow()
    {
        var model = Model(Result(BoardLine("Полка", new Vector3Int(500, 300, 18), "Дуб", SpecSections.Furniture, 1, 0.3f)));

        var totals = model.Rows.Where(r => r.Kind == DataRowKind.Total
            && Cell(r, SpecificationRows.UnitColumn) == "м²").ToList();

        Assert.AreEqual(1, totals.Count, "единственная единица — единственная итоговая строка по ней");
        Assert.AreEqual(NumberFormat.Fixed(0.3, 2), Cell(totals[0], SpecificationRows.QtyColumn));
    }

    [Test]
    public void Rows_TotalsByUnit_SeveralUnits_PrintsOneRowPerUnit()
    {
        var model = Model(Result(
            BoardLine("Полка", new Vector3Int(500, 300, 18), "Дуб", SpecSections.Furniture, 1, 0.3f),
            ItemLine("Кирпич", "Керамика", "Стены", SpecUnit.Pieces, 1, 500f),
            ItemLine("Труба ДН20", "", SpecSections.Plumbing, SpecUnit.LinearMeters, 1, 12.5f)));

        var units = model.Rows.Where(r => r.Kind == DataRowKind.Total)
            .Select(r => Cell(r, SpecificationRows.UnitColumn)).ToList();

        Assert.AreEqual(1, units.Count(u => u == "м²"), "ровно одна итоговая строка по м² (доски)");
        Assert.AreEqual(2, units.Count(u => u == "шт"),
            "по шт: «досок» и кирпич — две итоговые строки, одна общая и одна по разделу");
        Assert.AreEqual(1, units.Count(u => u == "м"), "ровно одна итоговая строка по м (труба), не перепутанная с м²");
    }

    [Test]
    public void Rows_EmptyResult_HasNoRowsAtAll()
    {
        var model = Model(Result());

        Assert.AreEqual(0, model.Rows.Count, "нет деталей — нет и строк «Всего»: окно покажет пустое состояние");
        Assert.AreEqual(0, model.Positions);
        Assert.AreEqual(0, model.Sections);
    }

    // ── Сама таблица: колонки и числа вправо ──

    [Test]
    public void Columns_NineOfThem_NumbersRight_NameTakesTheRest()
    {
        var columns = SpecificationRows.Columns();

        Assert.AreEqual(SpecificationRows.ColumnCount, columns.Count);
        Assert.IsTrue(columns[SpecificationRows.NameColumn].IsFlexible,
            "наименование тянется на остаток — числа держат ширину");
        foreach (int numeric in new[] { SpecificationRows.NumberColumn, SpecificationRows.WidthColumn,
                     SpecificationRows.HeightColumn, SpecificationRows.DepthColumn,
                     SpecificationRows.PiecesColumn, SpecificationRows.QtyColumn })
            Assert.AreEqual(CellAlign.Right, columns[numeric].Align, columns[numeric].Key + ": числа вправо (D8)");
        Assert.AreEqual("Ш, мм", columns[SpecificationRows.WidthColumn].Header,
            "единица — в шапке, а не в каждой ячейке");
    }
}
