using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.UI;
using NUnit.Framework;

/// <summary>
/// Модель выделения в таблице окна «Ошибки» без единого GameObject: выделение,
/// якорь Shift, toggle по Ctrl, копирование в порядке отрисовки и переживание
/// смены фильтров. Раньше это жило вперемешку с постройкой строк, и проверить
/// его можно было только через PlayMode со сценой.
/// </summary>
public class IssueTableSelectionTests
{
    private static AnalysisIssue Issue(string code) =>
        new AnalysisIssue(IssueLevel.Error, code, "Деталь " + code, "Текст " + code);

    private static IssueTableSelection WithVisible(params AnalysisIssue[] issues)
    {
        var selection = new IssueTableSelection();
        Rebuild(selection, issues);
        return selection;
    }

    private static void Rebuild(IssueTableSelection selection, params AnalysisIssue[] issues)
    {
        selection.BeginRebuild();
        foreach (var iss in issues) selection.AddVisible(iss);
        selection.EndRebuild();
    }

    [Test]
    public void PlainClick_SelectsOneRow_AndAsksForTheSceneSelection()
    {
        var a = Issue("COL-01");
        var selection = WithVisible(a, Issue("GAP-01"));

        bool selectsInScene = selection.Click(a, 0, ctrl: false, shift: false);

        Assert.IsTrue(selectsInScene,
            "обычный клик по строке выделяет и связанную деталь в сцене — Ctrl и Shift этого не делают");
        Assert.AreEqual(1, selection.SelectedCount);
        Assert.AreEqual(0, selection.FocusVisibleIdx);
    }

    [Test]
    public void CtrlClick_TogglesOneRow_WithoutTouchingTheScene()
    {
        var a = Issue("COL-01");
        var selection = WithVisible(a, Issue("GAP-01"));
        selection.Click(a, 0, ctrl: false, shift: false);

        bool selectsInScene = selection.Click(a, 0, ctrl: true, shift: false);

        Assert.IsFalse(selectsInScene, "Ctrl+клик правит только таблицу");
        Assert.AreEqual(0, selection.SelectedCount, "Ctrl+клик по выделенной строке снимает выделение");
    }

    [Test]
    public void ShiftClick_ExtendsFromTheAnchor_AndLeavesTheAnchorWhereItWas()
    {
        var a = Issue("A");
        var b = Issue("B");
        var c = Issue("C");
        var d = Issue("D");
        var selection = WithVisible(a, b, c, d);

        selection.Click(a, 0, ctrl: false, shift: false);
        selection.Click(c, 2, ctrl: false, shift: true);
        Assert.AreEqual(3, selection.SelectedCount, "Shift берёт диапазон от якоря до строки под курсором");

        selection.Click(d, 3, ctrl: false, shift: true);
        Assert.AreEqual(4, selection.SelectedCount,
            "якорь после Shift не двигается — второй Shift считает диапазон от той же строки A");
    }

    [Test]
    public void CtrlClick_DoesNotMoveTheAnchor_SoAFollowingShiftStillCountsFromIt()
    {
        var a = Issue("A");
        var b = Issue("B");
        var c = Issue("C");
        var d = Issue("D");
        var selection = WithVisible(a, b, c, d);

        selection.Click(a, 0, ctrl: false, shift: false);
        selection.Click(d, 3, ctrl: true, shift: false);
        selection.Click(c, 2, ctrl: false, shift: true);

        Assert.AreEqual(4, selection.SelectedCount,
            "якорь остался на A: Shift добирает A..C и вместе с D даёт четыре. "
            + "Если бы Ctrl+клик сдвинул якорь на D, вышло бы три (C..D плюс A)");
    }

    [Test]
    public void Copy_FollowsTheOrderOfTheRows_NotTheOrderOfClicks()
    {
        var a = Issue("A");
        var b = Issue("B");
        var c = Issue("C");
        var selection = WithVisible(a, b, c);

        selection.Click(c, 2, ctrl: true, shift: false);
        selection.Click(a, 0, ctrl: true, shift: false);

        var lines = selection.SelectedAsClipboardText().Split('\n');
        Assert.AreEqual(2, lines.Length);
        StringAssert.Contains("Деталь A", lines[0], "копирование идёт сверху вниз по видимым строкам");
        StringAssert.Contains("Деталь C", lines[1], "копирование идёт сверху вниз по видимым строкам");
    }

    [Test]
    public void CopiedRow_IsTabSeparated_ForPastingIntoASpreadsheet()
    {
        var a = Issue("COL-01");
        var selection = WithVisible(a);
        selection.Click(a, 0, ctrl: false, shift: false);

        var cols = selection.SelectedAsClipboardText().Split('\t');

        Assert.AreEqual(4, cols.Length, "уровень, код, деталь, текст — четыре колонки через табуляцию");
        Assert.AreEqual(IssueDisplay.LevelName(IssueLevel.Error), cols[0]);
        Assert.AreEqual("COL-01", cols[1]);
    }

    [Test]
    public void AFilterThatHidesTheSelection_KeepsItSelected_ButDropsTheFocusIndex()
    {
        var a = Issue("A");
        var b = Issue("B");
        var selection = WithVisible(a, b);
        selection.Click(a, 0, ctrl: false, shift: false);

        Rebuild(selection);

        Assert.IsTrue(selection.IsSelected(a),
            "смена фильтра — не переанализ: выделение сохраняется, чтобы вернуться вместе со строкой");
        Assert.AreEqual(-1, selection.FocusVisibleIdx,
            "индекс фокуса указывал бы за пределы списка, и стрелки прыгали бы в пустоту");
    }

    [Test]
    public void WhenTheFilterShowsTheAnchorAgain_ShiftKeepsWorkingFromIt()
    {
        var a = Issue("A");
        var b = Issue("B");
        var c = Issue("C");
        var selection = WithVisible(a, b, c);
        selection.Click(b, 1, ctrl: false, shift: false);

        Rebuild(selection, c);
        Rebuild(selection, a, b, c);

        selection.Click(c, 2, ctrl: false, shift: true);

        Assert.AreEqual(2, selection.SelectedCount,
            "фильтр выбросил якорь из списка и обнулил его индекс; при возврате строки индекс "
            + "восстанавливается поиском самой issue. Без этого Shift сработал бы как обычный "
            + "клик и молча выделил одну строку");
        Assert.IsTrue(selection.IsSelected(b), "диапазон считается от вернувшегося якоря B");
    }

    [Test]
    public void Analyze_ClearsEverything_BecauseTheIssuesThemselvesMayBeGone()
    {
        var a = Issue("A");
        var selection = WithVisible(a, Issue("B"));
        selection.Click(a, 0, ctrl: false, shift: false);

        selection.Clear();

        Assert.AreEqual(0, selection.SelectedCount);
        Assert.AreEqual(-1, selection.FocusVisibleIdx);
    }

    [Test]
    public void MoveFocusTo_OutsideTheList_ChangesNothing()
    {
        var a = Issue("A");
        var selection = WithVisible(a);
        selection.Click(a, 0, ctrl: false, shift: false);

        Assert.IsFalse(selection.MoveFocusTo(5), "выход за список — не навигация");
        Assert.AreEqual(0, selection.FocusVisibleIdx, "фокус остался на прежней строке");
    }
}
