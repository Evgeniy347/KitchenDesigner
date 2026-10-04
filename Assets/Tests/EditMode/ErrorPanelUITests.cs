using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// T7 — «Ошибки» по docs/ui-redesign/tables.md: чипы уровня со счётчиками вместо списка «Уровень»,
// списки кода и этажа без подписей над ними, колонка уровня — значок, счётчик в заголовке, кнопки
// «Обновить» нет, футер «подсказка | К первой ошибке», пустое состояние D9.
public class ErrorPanelUITests
{
    private GameObject? _canvas;
    private ErrorPanelUI? _panel;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        LevelRegistry.Reset();
        PartRegistry.Clear();
        _canvas = UiTestCanvas.Create("ErrorsCanvas");
        _panel = _canvas.AddComponent<ErrorPanelUI>();
        _panel.Build(_canvas.transform);
    }

    [TearDown]
    public void TearDown()
    {
        UiTestCanvas.Release(_canvas);
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        ProjectWindows.Clear();
        LevelRegistry.Reset();
        LogAssert.ignoreFailingMessages = false;
    }

    private Transform Panel => _canvas!.transform.Find("ErrorPanel")!;

    private Button Chip(string node) => Panel.Find(node)!.GetComponent<Button>();

    private static void SpawnOverlappingBoards()
    {
        ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Доска А", Vector3.zero);
        ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Доска Б", Vector3.zero);
    }

    private static int CountOf(IssueLevel level) => SceneAnalyzer.Analyze().Count(i => i.Level == level);

    [Test]
    public void Build_TitleLeft_NoRefreshButton_NoCaptionsAboveTheFilters()
    {
        Assert.AreEqual("Ошибки", _panel!.Chrome.Title.text);
        Assert.AreEqual(TextAlignmentOptions.Left, _panel.Chrome.Title.alignment, "заголовок слева (D5)");
        Assert.IsNull(Panel.Find("ErrRefresh"), "пересчёт живой — «Обновить» атавизм (todo_evolution 1.2)");
        foreach (var caption in new[] { "FltLevelLbl", "FltCodeLbl", "FltFloorLbl", "FltSearchLbl" })
            Assert.IsNull(Panel.Find(caption), caption + ": подпись над фильтром — первый пункт списка «Все …» её заменяет");
    }

    [Test]
    public void Build_FooterCarriesTheHintOnTheLeft_AndGoToFirstOnTheRight()
    {
        var footer = Panel.Find("ErrorPanelFooter")!;
        var hint = footer.Find(ErrorPanelUI.HelpNode)!.GetComponent<TMP_Text>();
        var first = (RectTransform)footer.Find(ErrorPanelUI.FirstErrorNode)!;

        Assert.AreEqual("Двойной клик — выделить деталь и показать её", hint.text);
        Assert.Less(hint.rectTransform.anchorMin.x, first.anchorMin.x, "подсказка слева, действие справа");
        Assert.AreEqual(1f, first.anchorMin.x, "кнопка прижата к правому краю футера");
        Assert.AreEqual("К первой ошибке", first.GetComponentInChildren<TMP_Text>().text);
    }

    [Test]
    public void Filters_AreChipsOfLevelsOnTheLeft_ThenTwoDropdownsAndAStretchingSearch()
    {
        var all = (RectTransform)Panel.Find(IssueFilterBar.ChipAllNode)!;
        var errors = (RectTransform)Panel.Find(IssueFilterBar.ChipErrorsNode)!;
        var warnings = (RectTransform)Panel.Find(IssueFilterBar.ChipWarningsNode)!;
        var code = (RectTransform)Panel.Find(IssueFilterBar.CodeNode)!;
        var floor = (RectTransform)Panel.Find(IssueFilterBar.FloorNode)!;
        var search = (RectTransform)Panel.Find(IssueFilterBar.SearchNode)!;

        var xs = new[] { all, errors, warnings, code, floor, search }.Select(r => r.anchoredPosition.x).ToList();
        CollectionAssert.AreEqual(xs.OrderBy(x => x).ToList(), xs, "порядок слева направо: чипы, код, этаж, поиск");
        Assert.AreEqual(UIStyle.ChipH, all.sizeDelta.y, "чип 28 высотой");
        Assert.AreEqual(UIStyle.ControlHCompact, code.sizeDelta.y, "списки кода и этажа — 28");
        float right = search.anchoredPosition.x + search.sizeDelta.x;
        Assert.AreEqual(_panel!.Chrome.BodyPad + _panel.Chrome.BodyWidth, right, 0.5f,
            "поиск растягивается до правого края тела окна");
        Assert.AreEqual("Все коды", code.GetComponentInChildren<TMP_Text>().text, "подпись — первый пункт, не отдельный текст");
        Assert.AreEqual("Все этажи", floor.GetComponentInChildren<TMP_Text>().text);
    }

    [Test]
    public void TheAllChip_IsSelectedFromTheStart_WithAccentSubtle()
    {
        var chips = _panel!.Filters;
        Assert.AreEqual(IssueLevelFilter.All, chips.Level);
        Assert.IsTrue(chips.ChipFor(IssueLevelFilter.All).Selected);
        Assert.AreEqual(UIStyle.AccentSubtle, chips.ChipFor(IssueLevelFilter.All).Button.GetComponent<Image>().color);
        Assert.AreEqual(UIStyle.Panel, chips.ChipFor(IssueLevelFilter.Errors).Button.GetComponent<Image>().color);
    }

    [Test]
    public void Chips_ShowTheCountsOfEachLevel_AndTheTitleShowsTheTotal()
    {
        SpawnOverlappingBoards();
        _panel!.SetVisible(true);

        int total = SceneAnalyzer.Analyze().Count;
        Assume.That(total, Is.GreaterThan(0), "предпосылка: наложенные доски дают находку");
        Assert.AreEqual(total, _panel.TotalIssueCount);

        string AllText() => _panel.Filters.ChipFor(IssueLevelFilter.All).Button.GetComponentInChildren<TMP_Text>().text;
        StringAssert.Contains(NumberFormat.Integer(total), AllText());
        Assert.AreEqual(NumberFormat.Integer(total),
            Panel.Find(WindowTitleCount.Node)!.GetComponent<TMP_Text>().text,
            "счётчик — в заголовке («Ошибки 2»), а не кеглем 14 в левом нижнем углу");

        string ErrorsText() => _panel.Filters.ChipFor(IssueLevelFilter.Errors).Button.GetComponentInChildren<TMP_Text>().text;
        StringAssert.Contains(NumberFormat.Integer(CountOf(IssueLevel.Error)), ErrorsText());
    }

    [Test]
    public void ClickingALevelChip_NarrowsTheRowsToThatLevel_AndAllRestoresThem()
    {
        SpawnOverlappingBoards();
        _panel!.SetVisible(true);
        int total = _panel.TotalIssueCount;
        int errors = CountOf(IssueLevel.Error);
        int warnings = CountOf(IssueLevel.Warning);
        Assume.That(total, Is.GreaterThan(0));

        Chip(IssueFilterBar.ChipErrorsNode).onClick.Invoke();
        Assert.AreEqual(errors, _panel.VisibleIssueCount, "чип «Ошибки» оставляет только ошибки");
        Assert.IsTrue(_panel.Filters.ChipFor(IssueLevelFilter.Errors).Selected);
        Assert.IsFalse(_panel.Filters.ChipFor(IssueLevelFilter.All).Selected, "выбран ровно один чип");

        Chip(IssueFilterBar.ChipWarningsNode).onClick.Invoke();
        Assert.AreEqual(warnings, _panel.VisibleIssueCount, "чип «Предупреждения» — только предупреждения");

        Chip(IssueFilterBar.ChipAllNode).onClick.Invoke();
        Assert.AreEqual(total, _panel.VisibleIssueCount);
    }

    [Test]
    public void LevelFilter_PassesOnlyItsOwnLevel_AndAnInfoIssueSurvivesOnlyUnderAll()
    {
        var bar = _panel!.Filters;
        var error = new AnalysisIssue(IssueLevel.Error, "E-1", "d", "m");
        var warning = new AnalysisIssue(IssueLevel.Warning, "W-1", "d", "m");
        var info = new AnalysisIssue(IssueLevel.Info, "I-1", "d", "m");

        Chip(IssueFilterBar.ChipErrorsNode).onClick.Invoke();
        Assert.IsTrue(bar.Passes(error, null));
        Assert.IsFalse(bar.Passes(warning, null));
        Assert.IsFalse(bar.Passes(info, null), "справка не входит ни в «Ошибки», ни в «Предупреждения»");

        Chip(IssueFilterBar.ChipAllNode).onClick.Invoke();
        Assert.IsTrue(bar.Passes(info, null));
    }

    [Test]
    public void TheSearchField_FiltersByCodePartAndText_IgnoringCase()
    {
        var bar = _panel!.Filters;
        var issue = new AnalysisIssue(IssueLevel.Warning, "GRD-01", "Verhniy_yaschik", "Край детали не на целом миллиметре");

        bar.Search.text = "grd";
        Assert.IsTrue(bar.Passes(issue, null), "по коду");
        bar.Search.text = "YASCHIK";
        Assert.IsTrue(bar.Passes(issue, null), "по детали");
        bar.Search.text = "миллиметре";
        Assert.IsTrue(bar.Passes(issue, null), "по тексту");
        bar.Search.text = "нет такого";
        Assert.IsFalse(bar.Passes(issue, null));
    }

    [Test]
    public void TheSearchHint_IsShownOnlyWhileTheFieldIsEmpty()
    {
        var hint = Panel.Find(IssueFilterBar.SearchNode + "/" + IssueFilterBar.SearchHintNode)!.gameObject;
        Assert.IsTrue(hint.activeSelf);
        Assert.AreEqual("Поиск: код, деталь, текст", hint.GetComponent<TMP_Text>().text);

        _panel!.SearchField.text = "x";
        Assert.IsFalse(hint.activeSelf, "подсказка под введённым текстом не нужна");
        _panel.SearchField.text = "";
        Assert.IsTrue(hint.activeSelf);
    }

    [Test]
    public void NoIssues_ShowsTheEmptyState_WithTheTitleAndTheAutoCheckHint()
    {
        _panel!.SetVisible(true);

        var empty = _panel.Table.Empty!;
        Assert.IsTrue(empty.Root.gameObject.activeSelf, "450 px пустого тела без слова — дефект (D9)");
        Assert.AreEqual("Ошибок нет", empty.Title.text);
        Assert.AreEqual("Проверка идёт сама после каждой правки.", empty.Hint.text);
        Assert.AreEqual("0", Panel.Find(WindowTitleCount.Node)!.GetComponent<TMP_Text>().text);
        Assert.IsFalse(Panel.Find("ErrorPanelFooter/" + ErrorPanelUI.FirstErrorNode)!.GetComponent<Button>().interactable,
            "идти не к чему — «К первой ошибке» выключена");
    }

    [Test]
    public void FiltersHidingEverything_SayNothingFound_NotNoErrors()
    {
        SpawnOverlappingBoards();
        _panel!.SetVisible(true);
        Assume.That(_panel.TotalIssueCount, Is.GreaterThan(0));

        _panel.SearchField.text = "ZZZ_no_such_issue_999";

        Assert.AreEqual(0, _panel.VisibleIssueCount);
        Assert.AreEqual("Ничего не найдено", _panel.Table.Empty!.Title.text,
            "ошибки есть, их скрыл фильтр: «Ошибок нет» тут было бы неправдой");
    }

    [Test]
    public void GoToFirst_SelectsTheFirstErrorOfTheList_OrTheFirstRowWhenThereIsNone()
    {
        SpawnOverlappingBoards();
        _panel!.SetVisible(true);
        var issues = SceneAnalyzer.Analyze().ToList();
        Assume.That(issues.Count, Is.GreaterThan(0));
        int expected = issues.FindIndex(i => i.Level == IssueLevel.Error);
        if (expected < 0) expected = 0;

        Panel.Find("ErrorPanelFooter/" + ErrorPanelUI.FirstErrorNode)!.GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(1, _panel.SelectedCount);
        Assert.AreEqual(expected, _panel.SelectedVisibleIdx);
        Assert.IsTrue(_panel.IsSelected(issues[expected]));
    }

    [Test]
    public void ARow_ShowsAGlyphInTheLevelColour_AndTheTextInTheOtherColumns()
    {
        var error = IssueRows.For(new AnalysisIssue(IssueLevel.Error, "COL-02", "Svobodnaya_polka", "Висит в воздухе"));
        var warning = IssueRows.For(new AnalysisIssue(IssueLevel.Warning, "GRD-01", "Verhniy_yaschik", "Сдвиг"));

        Assert.AreEqual(UIStyle.GlyphClose, error.Cell(IssueRows.GlyphColumn), "значок вместо слова «Ошибка» (колонка 14 px)");
        Assert.AreEqual(UIStyle.TextError, error.CellColor(IssueRows.GlyphColumn));
        Assert.AreEqual(UIStyle.GlyphWarning, warning.Cell(IssueRows.GlyphColumn));
        Assert.AreEqual(UIStyle.TextWarning, warning.CellColor(IssueRows.GlyphColumn));
        Assert.IsNull(error.CellColor(IssueRows.CodeColumn), "остальные ячейки — обычным цветом текста");
        Assert.AreEqual("COL-02", error.Cell(IssueRows.CodeColumn));
        Assert.AreEqual("Висит в воздухе", error.Cell(IssueRows.MessageColumn));
    }

    [Test]
    public void ASelectedRow_IsPaintedWithRowSelected_AndItsBar()
    {
        SpawnOverlappingBoards();
        _panel!.SetVisible(true);
        var issues = SceneAnalyzer.Analyze();
        Assume.That(issues.Count, Is.GreaterThan(0));

        _panel.HandleRowClick(issues[0], 0, ctrl: false, shift: false);

        var row = _panel.Table.RowRect(0);
        Assert.AreEqual(UIStyle.RowSelected, row.GetComponent<Image>().color);
        Assert.IsTrue(row.Find("SelectionBar")!.gameObject.activeSelf, "выбранная строка — заливка и полоса слева (D10)");
    }

    [Test]
    public void ClickingARowOfTheTable_SelectsIt_ThroughTheSelectionModel()
    {
        SpawnOverlappingBoards();
        _panel!.SetVisible(true);
        var issues = SceneAnalyzer.Analyze();
        Assume.That(issues.Count, Is.GreaterThan(0));

        _panel.Table.Select(_panel.Table.ShownRows[0]);

        Assert.AreEqual(1, _panel.SelectedCount, "клик по строке таблицы идёт тем же путём, что HandleRowClick");
        Assert.AreEqual(0, _panel.SelectedVisibleIdx);
    }

    [Test]
    public void TheTable_FillsTheWindowBetweenTheFilterRowAndTheFooter()
    {
        var table = (RectTransform)Panel.Find(ErrorPanelUI.TableNode)!;
        var panel = (RectTransform)Panel;

        float top = -table.anchoredPosition.y;
        float bottom = top + table.sizeDelta.y;
        Assert.AreEqual(panel.sizeDelta.y - UIStyle.FooterH, bottom, 0.5f, "таблица кончается там, где начинается футер");
        Assert.Greater(top, UIStyle.TitleBarH + UIStyle.ChipH, "таблица ниже строки фильтров");
        Assert.AreEqual(UIStyle.ErrorsSize, panel.sizeDelta, "размер окна — токен ErrorsSize (D5)");
    }
}
