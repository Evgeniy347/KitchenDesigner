using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// T4b — таблица D8: шапка прибита и выровнена как колонка, числа вправо, длинный текст —
// многоточие и tooltip, строка-группа и подытог, сортировка по шапке, пустое состояние D9.
// Сегодня «Спецификация» — один TMP-текст с <pos>, числа влево и имя наезжает на «400»;
// окна T7 переезжают на этот компонент.
public class DataTableTests
{
    private GameObject? _canvas;

    private static readonly DataColumn[] Columns =
    {
        new DataColumn("n", "№", 40f, CellAlign.Right),
        new DataColumn("name", "Наименование", 0f, CellAlign.Left, sortable: true),
        new DataColumn("w", "Ш, мм", 70f, CellAlign.Right, sortable: true),
    };

    [SetUp]
    public void SetUp() => _canvas = UiTestCanvas.Create("TableCanvas");

    [TearDown]
    public void TearDown() => UiTestCanvas.Release(_canvas);

    private DataTable Table(bool selectable = false) =>
        DataTable.Create(_canvas!.transform, "Table", new Vector2(400f, 300f), Columns, selectable);

    private static List<DataRow> Spec() => new()
    {
        DataRow.Group("Мебель"),
        DataRow.Item("1", "Полка", "1325"),
        DataRow.Item("2", "Дно", "564"),
        new DataRow(DataRowKind.Subtotal, "", "Итого, Серый", "0,68"),
        DataRow.Group("Сантехника"),
        DataRow.Item("3", "Смеситель для кухни с очень длинным названием модели", "50"),
    };

    [Test]
    public void Numbers_AlignRight_Text_AlignsLeft_AndTheHeaderAlignsLikeItsColumn()
    {
        var table = Table();
        table.SetRows(Spec());
        var name = table.CellLabel(1, 1)!;
        var width = table.CellLabel(1, 2)!;
        Assert.AreEqual(TextAlignmentOptions.Left, name.alignment);
        Assert.AreEqual(TextAlignmentOptions.Right, width.alignment, "числа вправо — разряды под разрядами (D8)");
        Assert.AreEqual(TextAlignmentOptions.Right, table.HeaderLabel(2).alignment,
            "шапка выровнена как её колонка: «Ш, мм» над правым краем чисел, а не над их началом");

        float cellRight = width.rectTransform.anchoredPosition.x + width.rectTransform.sizeDelta.x;
        var head = table.HeaderLabel(2).rectTransform;
        Assert.AreEqual(cellRight, head.anchoredPosition.x + head.sizeDelta.x, 0.5f);
    }

    [Test]
    public void Header_IsPinned_OutsideTheScrollingBody()
    {
        var table = Table();
        table.SetRows(Spec());
        Assert.IsFalse(table.Header.IsChildOf(table.Body.Content), "шапка прибита — прокручивается только тело (D8)");
        Assert.AreEqual(UIStyle.TableHeaderH, table.Header.sizeDelta.y);
        Assert.AreEqual(UIStyle.FontSmall, (int)table.HeaderLabel(0).fontSize);
        Assert.AreEqual(UIStyle.TextSecondary, table.HeaderLabel(0).color);
    }

    [Test]
    public void LongText_EndsInAnEllipsis_AndCarriesTheFullTextInATooltip()
    {
        var table = Table();
        table.SetRows(Spec());
        var longName = table.CellLabel(5, 1)!;
        Assert.AreEqual(TextOverflowModes.Ellipsis, longName.overflowMode);
        Assert.IsNotNull(longName.GetComponent<UnityEngine.EventSystems.EventTrigger>(),
            "обрезанное имя показывает полный текст наведением — «Комплект GTV AXIS PRO» не наезжает на «400»");
        Assert.IsNull(table.CellLabel(1, 1)!.GetComponent<UnityEngine.EventSystems.EventTrigger>(), "короткому — tooltip не нужен");
    }

    [Test]
    public void GroupRow_IsBoldSecondary_SubtotalIsSmallSecondary_NumberInTheSameColumn()
    {
        var table = Table();
        table.SetRows(Spec());
        var group = table.CellLabel(0, 0)!;
        Assert.AreEqual(FontStyles.Bold, group.fontStyle);
        Assert.AreEqual(UIStyle.TextSecondary, group.color);
        Assert.AreEqual(UIStyle.TableGroupRowH, table.RowRect(0).sizeDelta.y);

        var subtotal = table.CellLabel(3, 2)!;
        Assert.AreEqual(UIStyle.FontSmall, (int)subtotal.fontSize);
        Assert.AreEqual(UIStyle.TextSecondary, subtotal.color);
        Assert.AreEqual(table.CellLabel(1, 2)!.rectTransform.anchoredPosition.x, subtotal.rectTransform.anchoredPosition.x,
            "подытог стоит в той же колонке, что слагаемые, а не в колонке «Материал»");
    }

    [Test]
    public void ReadOnlyRows_Are28_InteractiveRows_32()
    {
        var plain = Table();
        plain.SetRows(Spec());
        Assert.AreEqual(UIStyle.TableRowH, plain.RowRect(1).sizeDelta.y);

        var interactive = DataTable.Create(_canvas!.transform, "T2", new Vector2(400f, 300f), Columns, selectable: true);
        interactive.SetRows(Spec());
        Assert.AreEqual(UIStyle.TableRowInteractiveH, interactive.RowRect(1).sizeDelta.y);
        Assert.AreEqual(UIStyle.TableGroupRowH + UIStyle.TableRowInteractiveH * 3 + UIStyle.TableRowH + UIStyle.TableGroupRowH,
            interactive.ContentHeight, 0.5f, "высота тела — сумма строк, область прокрутки знает её сама");
    }

    [Test]
    public void ClickingASortableHeader_SortsWithinGroups_AndShowsTheDirection()
    {
        var table = Table();
        table.SetRows(Spec());
        table.ToggleSort(2);
        Assert.AreEqual(new[] { "Мебель", "Дно", "Полка", "Итого, Серый", "Сантехника", "Смеситель для кухни с очень длинным названием модели" },
            table.ShownRows.Select(r => r.Kind == DataRowKind.Group ? r.Cell(0) : r.Cell(1)).ToArray(),
            "564 < 1325 по числу, а не по тексту («1» < «5»); подытог остаётся в конце своей группы");
        StringAssert.Contains(DataTable.SortGlyphAscending, table.HeaderLabel(2).text);

        table.ToggleSort(2);
        Assert.IsTrue(table.SortDescending, "второй клик по той же шапке — обратный порядок");
        Assert.AreEqual("Полка", table.ShownRows[1].Cell(1));
        StringAssert.Contains(DataTable.SortGlyphDescending, table.HeaderLabel(2).text);

        table.ToggleSort(0);
        Assert.AreEqual(2, table.SortColumn, "колонка без сортировки на клик не отвечает");
    }

    [Test]
    public void Selection_PaintsTheRowAndTheBar_OnlyInASelectableTable()
    {
        var table = Table(selectable: true);
        var rows = Spec();
        table.SetRows(rows);
        DataRow? seen = null;
        table.SelectionChanged += r => seen = r;
        table.Select(rows[1]);

        Assert.AreSame(rows[1], seen);
        Assert.AreEqual(UIStyle.RowSelected, table.RowRect(1).GetComponent<Image>().color);
        Assert.IsTrue(table.RowRect(1).Find("SelectionBar").gameObject.activeSelf, "выбор — фон RowSelected и полоса SelectionBar (D4)");
        table.Select(rows[0]);
        Assert.AreSame(rows[1], table.Selected, "строку-группу не выбирают");

        var readOnly = Table();
        readOnly.SetRows(Spec());
        readOnly.Select(Spec()[1]);
        Assert.IsNull(readOnly.Selected);
    }

    [Test]
    public void NoRows_ShowsTheEmptyState_InWords()
    {
        var table = Table();
        table.SetEmptyState("Ошибок нет", "Проверка идёт сама после каждой правки.");
        table.SetRows(new List<DataRow>());
        Assert.IsTrue(table.Empty!.Root.gameObject.activeSelf, "пустой список — не пустое поле (D9)");
        Assert.AreEqual(UIStyle.Text, table.Empty.Title.color);
        Assert.AreEqual(UIStyle.TextSecondary, table.Empty.Hint.color);

        table.SetRows(Spec());
        Assert.IsFalse(table.Empty.Root.gameObject.activeSelf);
    }

    [Test]
    public void FlexibleColumn_TakesTheRest_OfTheWidthLessTheScrollbar()
    {
        var table = Table();
        Assert.AreEqual(400f - WindowBody.BarW - 40f - 70f, table.ColumnWidth(1), 0.01f);
        Assert.AreEqual(40f, table.ColumnLeft(1), 0.01f);
    }

    [Test]
    public void ADisabledRow_IsDimmed_CannotBeSelectedOrActivated_AndKeepsAnExplicitCellColour()
    {
        var table = Table(selectable: true);
        var live = DataRow.Item("1", "Рабочий", "10");
        var gone = new DataRow(DataRowKind.Item, "2", "Пропавший", "20")
        {
            Enabled = false,
            CellColors = new Color?[] { null, null, UIStyle.TextError },
        };
        table.SetRows(new List<DataRow> { live, gone });
        DataRow? activated = null;
        table.RowActivated += r => activated = r;

        table.Select(gone);
        table.Activate(gone);

        Assert.IsNull(table.Selected, "выключенную строку не выбирают: она ничего не открывает");
        Assert.AreEqual(UIStyle.TextDisabled, table.CellLabel(1, 1)!.color, "выключенная строка — TextDisabled (D10)");
        Assert.AreEqual(UIStyle.TextError, table.CellLabel(1, 2)!.color, "явный цвет ячейки сильнее выключенности");
        Assert.AreEqual(UIStyle.Text, table.CellLabel(0, 1)!.color);
        Assert.IsFalse(table.RowRect(1).GetComponent<Image>().raycastTarget, "и мышь её не ловит: наведения нет");
        Assert.IsNull(activated);
    }

    [Test]
    public void SelectionSource_DecidesWhichRowsArePainted_AndRepaintSelectionAppliesIt()
    {
        var table = Table(selectable: true);
        var rows = Spec();
        table.SetRows(rows);
        var picked = new HashSet<DataRow> { rows[1], rows[2] };
        table.SelectionSource = picked.Contains;

        table.RepaintSelection();

        Assert.AreEqual(UIStyle.RowSelected, table.RowRect(1).GetComponent<Image>().color);
        Assert.AreEqual(UIStyle.RowSelected, table.RowRect(2).GetComponent<Image>().color,
            "внешняя модель выбора (Ctrl/Shift в «Ошибках») красит несколько строк разом");
        Assert.AreNotEqual(UIStyle.RowSelected, table.RowRect(0).GetComponent<Image>().color);

        picked.Clear();
        table.RepaintSelection();
        Assert.AreNotEqual(UIStyle.RowSelected, table.RowRect(1).GetComponent<Image>().color);
    }

    [Test]
    public void ARowSetAgain_IsPaintedFromTheSelectionSource_NotFromTheLastClick()
    {
        var table = Table(selectable: true);
        DataRow? target = null;
        table.SelectionSource = row => row == target;
        var rows = Spec();
        target = rows[2];

        table.SetRows(rows);

        Assert.AreEqual(UIStyle.RowSelected, table.RowRect(2).GetComponent<Image>().color,
            "перестроенные строки сразу красятся по модели выбора: выделение переживает пересборку (фильтр, поиск)");
    }

    [Test]
    public void RowDecorator_RunsOncePerRow_WithItsRect_SoAWindowCanAddABadgeOrAButton()
    {
        var table = Table();
        var seen = new List<string>();
        table.RowDecorator = (row, rect) => seen.Add(row.Kind + ":" + rect.name);

        table.SetRows(Spec());

        Assert.AreEqual(6, seen.Count, "по разу на каждую показанную строку, включая группы и подытоги");
        Assert.AreEqual("Group:Row_0", seen[0]);
        Assert.AreEqual("Item:Row_1", seen[1]);
    }

    [Test]
    public void ScrollIntoView_MovesTheBodyJustEnoughToRevealTheRow()
    {
        var table = Table();
        var rows = new List<DataRow>();
        for (int i = 0; i < 40; i++) rows.Add(DataRow.Item(i.ToString(), "Строка " + i, "1"));
        table.SetRows(rows);
        var viewport = table.Body.Viewport;
        viewport.anchorMin = viewport.anchorMax = new Vector2(0f, 1f);
        viewport.pivot = new Vector2(0f, 1f);
        viewport.sizeDelta = new Vector2(300f, 140f);
        viewport.anchoredPosition = Vector2.zero;
        float row = UIStyle.TableRowH;

        table.ScrollIntoView(30);
        Assert.AreEqual(31 * row - 140f, table.Body.Content.anchoredPosition.y, 0.5f,
            "строка внизу — тело сдвинуто так, чтобы она встала у нижней кромки");

        table.ScrollIntoView(2);
        Assert.AreEqual(2 * row, table.Body.Content.anchoredPosition.y, 0.5f,
            "строка выше окна — встаёт у верхней кромки, страница целиком не катится");

        table.ScrollIntoView(3);
        Assert.AreEqual(2 * row, table.Body.Content.anchoredPosition.y, 0.5f, "строка уже видна — тело не трогаем");

        table.ScrollIntoView(-1);
        table.ScrollIntoView(400);
        Assert.AreEqual(2 * row, table.Body.Content.anchoredPosition.y, 0.5f, "неверный индекс — ничего не двигает");
    }

    [Test]
    public void RowsPerPage_IsTheWholeRowsThatFitTheBody()
    {
        var table = Table();
        var viewport = table.Body.Viewport;
        viewport.anchorMin = viewport.anchorMax = new Vector2(0f, 1f);
        viewport.pivot = new Vector2(0f, 1f);
        viewport.sizeDelta = new Vector2(300f, 140f);

        Assert.AreEqual(5, table.RowsPerPage(), "140 / 28 = 5 строк на страницу — шаг PageUp/PageDown");
    }
}
