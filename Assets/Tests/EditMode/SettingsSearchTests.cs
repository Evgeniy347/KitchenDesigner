using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Поиск в навигации «Настроек» (D6): ввод фильтрует СТРОКИ по подписи, тексту «i» и названию секции,
/// а навигацию — до разделов, где что-то нашлось; пустой результат — пустое состояние D9, а не пустое
/// окно. Проверяется на настоящей панели: фильтр живёт на стыке трёх классов (поле, страницы, навигация),
/// и каждый по отдельности может быть исправен.
/// </summary>
public class SettingsSearchTests
{
    private ProjectLoadStateGuard? _globals;
    private GameObject? _canvasGo;
    private SettingsPanelUI? _ui;

    [SetUp]
    public void SetUp()
    {
        _globals = ProjectLoadStateGuard.Capture();
        Loc.SetLanguage("ru");
        _canvasGo = UiTestCanvas.Create("SearchCanvas");
        _ui = _canvasGo.AddComponent<SettingsPanelUI>();
        _ui.Build(_canvasGo.transform);
        _ui.SetVisible(true);
    }

    [TearDown]
    public void TearDown()
    {
        EditModeManager.Reset();
        UiTestCanvas.Release(_canvasGo);
        _globals?.Restore();
    }

    private TMP_InputField Search => _ui!.Nav!.Search;

    private void Type(string text) => Search.text = text;

    private Transform Page(string id) => _canvasGo!.transform.Find(SettingsWindowPaths.Page(id));

    private bool RowShown(string pageId, string key) => Page(pageId).Find("Row_" + key).gameObject.activeSelf;

    private List<string> ShownNavIds() =>
        _ui!.Nav!.Buttons.Where(b => b.gameObject.activeSelf)
            .Select(b => b.name.Substring(SettingsNav.ItemPrefix.Length)).ToList();

    [Test]
    public void TypingAStepWord_KeepsTheProjectPage_AndOnlyTheMatchingRowsOnIt()
    {
        string step = Loc.T("settings.project.gridStep").Split(' ')[0].ToLowerInvariant();

        Type(step);

        CollectionAssert.Contains(ShownNavIds(), SettingsPanelUI.ProjectId, "«Проект» остаётся в навигации");
        Assert.AreEqual(SettingsPanelUI.PageOrder.ToList().IndexOf(SettingsPanelUI.ProjectId), _ui!.CurrentTab,
            "первая страница, где нашлось, открывается сама (если открытая ничего не дала)");
        Assert.IsTrue(RowShown("project", Loc.T("settings.project.gridStep")), "на странице — строка «Шаг сетки»");
        Assert.IsFalse(RowShown("project", Loc.T("settings.project.autoSave")),
            "строка, где слова нет ни в подписи, ни в «i», ни в названии секции, скрыта");
    }

    [Test]
    public void TheSearch_MatchesTheHintText_NotOnlyTheCaption()
    {
        string hint = HintText.Of("settings.project.edgePartialThreshold").ToLowerInvariant();
        string word = hint.Split(' ', '.', ',', ';', '—', '(', ')').Where(w => w.Length >= 6)
            .OrderByDescending(w => w.Length).First();
        Assume.That(Loc.T("settings.project.edgePartialThreshold").ToLowerInvariant(), Does.Not.Contain(word),
            "слово взято из текста «i», а не из подписи");

        Type(word);

        Assert.IsTrue(RowShown("project", Loc.T("settings.project.edgePartialThreshold")),
            "строка найдена по слову из своей подсказки «i»");
    }

    [Test]
    public void TheSearch_MatchesTheSectionTitle_AndShowsEveryRowOfThatSection()
    {
        Type(Loc.T("settings.project.section.checks"));

        Assert.IsTrue(RowShown("project", Loc.T("settings.project.blockOnViolation")), "строка секции «Проверки»");
        Assert.IsTrue(RowShown("project", Loc.T("settings.project.edgePartialThreshold")), "и вторая строка секции");
        Assert.IsFalse(RowShown("project", Loc.T("settings.project.gridStep")), "чужая секция скрыта");
        var header = Page("project").Find(SettingsPage.SectionPrefix + Loc.T("settings.project.section.grid"));
        Assert.IsFalse(header.gameObject.activeSelf, "заголовок секции без найденных строк скрыт вместе с ней");
    }

    [Test]
    public void WhenTheOpenPageHasNoMatch_TheFirstPageWithAMatchOpens()
    {
        _ui!.OpenTab(SettingsPanelUI.PageOrder.ToList().IndexOf(SettingsPanelUI.AboutId));

        Type(Loc.T("settings.project.gridStep"));

        Assert.AreEqual(SettingsPanelUI.PageOrder.ToList().IndexOf(SettingsPanelUI.ProjectId), _ui.CurrentTab);
    }

    [Test]
    public void NavItems_WithoutAMatch_AreHidden_AndTheirGroupCaptionsWithThem()
    {
        Type(Loc.T("settings.project.gridStep"));

        var shown = ShownNavIds();
        Assert.Less(shown.Count, SettingsPanelUI.PageOrder.Length, "навигация сузилась до разделов с совпадением");
        var groups = _canvasGo!.transform.Find(SettingsWindowPaths.Nav).Cast<Transform>()
            .Where(t => t.name.StartsWith(SettingsNav.GroupPrefix)).ToList();
        foreach (var page in new[] { SettingsPanelUI.LightId, SettingsPanelUI.AboutId })
            if (!shown.Contains(page))
                Assert.IsTrue(groups.Any(g => !g.gameObject.activeSelf), "подпись группы без пунктов скрыта");
    }

    [Test]
    public void AnEmptyResult_ShowsTheEmptyState_AndNoPage()
    {
        Type("qwzxj");

        var empty = _canvasGo!.transform.Find(SettingsWindowPaths.Body + SettingsPanelUI.EmptyNode);
        Assert.IsNotNull(empty, "пустое состояние собрано");
        Assert.IsTrue(empty.gameObject.activeSelf, "пустой результат — слово словами (D9), а не пустое окно");
        StringAssert.Contains(Loc.T("settings.empty.title"),
            string.Join("|", empty.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text)));
        Assert.IsEmpty(ShownNavIds(), "навигация пуста");
        foreach (var page in _canvasGo.transform.Find(SettingsWindowPaths.Body.TrimEnd('/')).Cast<Transform>()
                     .Where(c => c.name.StartsWith("Page_")))
            Assert.IsFalse(page.gameObject.activeSelf, page.name + " не должна быть видна");
    }

    [Test]
    public void ClearingTheSearch_BringsEverythingBack()
    {
        Type("qwzxj");
        Type("");

        Assert.AreEqual(SettingsPanelUI.PageOrder.Length, ShownNavIds().Count, "все разделы снова в навигации");
        var empty = _canvasGo!.transform.Find(SettingsWindowPaths.Body + SettingsPanelUI.EmptyNode);
        Assert.IsFalse(empty.gameObject.activeSelf);
        Assert.IsTrue(RowShown("project", Loc.T("settings.project.autoSave")), "и все строки страницы");
        Assert.IsTrue(Search.transform.Find(SettingsNav.SearchHintNode).gameObject.activeSelf,
            "подсказка в пустом поле видна снова");
    }

    [Test]
    public void TheSearchHint_IsShownOnlyWhileTheFieldIsEmpty()
    {
        var hint = Search.transform.Find(SettingsNav.SearchHintNode).gameObject;
        Assert.IsTrue(hint.activeSelf);
        Assert.AreEqual(Loc.T("settings.nav.search"), hint.GetComponent<TMP_Text>().text.Replace("​", ""));

        Type("x");

        Assert.IsFalse(hint.activeSelf, "подсказка не лежит под набранным текстом");
    }

    [Test]
    public void OpeningAPageFromOutside_ClearsTheSearch_SoTheTargetIsNotHidden()
    {
        Type("qwzxj");

        _ui!.OpenControlsTab();

        Assert.AreEqual(string.Empty, Search.text);
        Assert.AreEqual(SettingsPanelUI.PageOrder.ToList().IndexOf(SettingsPanelUI.ControlId), _ui.CurrentTab);
        Assert.IsTrue(Page("control").gameObject.activeSelf);
    }

    [Test]
    public void EveryPage_HasSearchableRows_SoTheSearchCanNeverSilentlySkipAPage()
    {
        foreach (var id in SettingsPanelUI.PageOrder)
        {
            var page = Page(id);
            int rows = page.Cast<Transform>().Count(c => c.name.StartsWith("Row_"));
            Assert.Greater(rows, 0, $"на странице {id} нет ни одной строки — поиск её не увидит");
        }
    }
}
