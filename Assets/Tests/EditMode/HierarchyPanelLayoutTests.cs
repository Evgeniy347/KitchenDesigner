using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class HierarchyPanelLayoutTests
{
    private GameObject _canvasGo = null!;
    private GameObject _panelHost = null!;
    private GameObject _selectionGo = null!;
    private HierarchyPanelUI _panel = null!;
    private SelectionManager _selection = null!;
    private SelectionManager? _selectionBefore;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        GroupManager.Clear();
        ProjectWindows.Clear();
        LevelRegistry.Reset();

        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();

        _selectionBefore = SelectionManager.Instance;
        _selectionGo = new GameObject("Selection");
        _selection = _selectionGo.AddComponent<SelectionManager>();
        SelectionManager.Instance = _selection;

        _panelHost = new GameObject("HierarchyPanelHost");
        _panel = _panelHost.AddComponent<HierarchyPanelUI>();
        _panel.Build(_canvasGo.transform);
    }

    [TearDown]
    public void Teardown()
    {
        if (_panelHost != null) Object.DestroyImmediate(_panelHost);
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        if (_selectionGo != null) Object.DestroyImmediate(_selectionGo);
        SelectionManager.Instance = _selectionBefore;
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        GroupManager.Clear();
        ProjectWindows.Clear();
        LevelRegistry.Reset();
    }

    private KitchenElement MakeElement(string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = new Vector3Int(600, 400, 18);
        PartRegistry.Register(element);
        _spawned.Add(go);
        return element;
    }

    private Transform Panel => _canvasGo.transform.Find("HierarchyPanel");

    private Transform Content => Panel.Find("HierarchyPanelBody/HierarchyPanelBodyContent");

    private Transform Footer => Panel.Find("HierarchyPanelFooter");

    private Transform Row(string label)
    {
        foreach (Transform row in Content)
            if (row.Find("Main").GetComponentInChildren<TMP_Text>().text == label) return row;
        Assert.Fail("в дереве нет строки «" + label + "»");
        return null!;
    }

    private static float LabelLeftMargin(Transform row) =>
        row.Find("Main").GetComponentInChildren<TMP_Text>().margin.x;

    private void ShowWithAGroupedModule()
    {
        var a = MakeElement("Боковина");
        var b = MakeElement("Верхний ящик");
        MakeElement("Свободная полка");
        var group = GroupManager.Link(new List<KitchenElement> { a, b })!;
        GroupManager.Rename(group, "Модуль");
        _panel.SetVisible(true);
    }

    [Test]
    public void Rows_AreTwentyEightHigh_AndIdleRowsCarryNoFill()
    {
        ShowWithAGroupedModule();

        Assert.Greater(Content.childCount, 3, "предпосылка: дерево построено");
        foreach (Transform row in Content)
        {
            Assert.AreEqual(UIStyle.TreeRowH, ((RectTransform)row).sizeDelta.y, 0.01f, row.name + ": высота строки D8");
            Assert.AreEqual(0f, row.GetComponent<Image>().color.a, 0.001f,
                "строка в покое без фона: дерево читается отступами, а не стопкой кнопок (D8)");
        }
    }

    [Test]
    public void SelectedRow_IsPaintedRowSelected_AndShowsTheYellowBar()
    {
        var shelf = MakeElement("Полка");
        MakeElement("Столешница");
        _selection.Select(shelf);
        _panel.SetVisible(true);

        var row = Row("Полка");
        var other = Row("Столешница");

        Assert.AreEqual(UIStyle.RowSelected, row.GetComponent<Image>().color);
        Assert.IsTrue(row.Find("SelectionBar").gameObject.activeSelf,
            "выбранная строка — фон RowSelected И полоса SelectionBar: тон выделения в 3D и в списке одной семьи");
        Assert.IsFalse(other.Find("SelectionBar").gameObject.activeSelf);
    }

    [Test]
    public void GroupMembers_SitOneIndentDeeperThanTheirUngroupedNeighbour()
    {
        ShowWithAGroupedModule();

        float member = LabelLeftMargin(Row("Боковина"));
        float neighbour = LabelLeftMargin(Row("Свободная полка"));

        Assert.AreEqual(UIStyle.TreeIndent, member - neighbour, 0.01f,
            "отступ на уровень — токен TreeIndent (12), а не число по месту");
    }

    [Test]
    public void GroupRow_ShowsItsMemberCountApart_AndKeepsTheNameClean()
    {
        ShowWithAGroupedModule();

        var row = Row("Модуль");
        var count = row.Find("Count").GetComponent<TMP_Text>();

        Assert.AreEqual(NumberFormat.Integer(2), count.text);
        Assert.AreEqual(UIStyle.TextDisabled, count.color, "счётчик — TextDisabled, как в D8");
    }

    [Test]
    public void GroupRow_MenuButtonAppearsOnlyUnderThePointer()
    {
        ShowWithAGroupedModule();
        var row = Row("Модуль");
        var view = row.GetComponent<SceneTreeRowView>();
        var menu = row.Find("GroupMenu").gameObject;
        Assume.That(menu.activeSelf, Is.False, "в покое «⋯» спрятана");

        view.OnPointerEnter(null!);
        Assert.IsTrue(menu.activeSelf, "под курсором «⋯» показана");
        Assert.AreEqual(UIStyle.RowHover, row.GetComponent<Image>().color);

        view.OnPointerExit(null!);
        Assert.IsFalse(menu.activeSelf);
    }

    [Test]
    public void GroupRow_HasAFoldButton_AndLeafRowsDoNot()
    {
        ShowWithAGroupedModule();

        Assert.IsNotNull(Row("Модуль").Find("Fold"));
        Assert.IsNull(Row("Свободная полка").Find("Fold"));
        Assert.IsNotNull(Row("Модуль").Find("Icon"), "у группы и детали иконка типа 14");
        Assert.AreEqual(UIStyle.TreeIconSize, ((RectTransform)Row("Модуль").Find("Icon")).sizeDelta.x);
    }

    [Test]
    public void AddGroup_IsAnIconInTheTitleRow_LeftOfTheCrossWithATooltip()
    {
        var add = (RectTransform)Panel.Find("HierAddGroup");
        var close = (RectTransform)Panel.Find("CloseBtn");

        Assert.IsNotNull(add.GetComponentInChildren<Image>(), "иконка, не подписанная кнопка");
        Assert.AreEqual(0, add.GetComponentsInChildren<TMP_Text>(true).Length, "в шапке «＋» — спрайт без текста (D5)");
        Assert.AreEqual(close.anchoredPosition.y, add.anchoredPosition.y, 0.01f, "на одной линии с ×");
        Assert.Less(add.anchoredPosition.x, close.anchoredPosition.x, "левее ×");
    }

    [Test]
    public void SearchField_IsTheFirstThingUnderTheTitle_AndTheTreeStartsBelowIt()
    {
        var search = (RectTransform)Panel.Find("HierSearch");
        var viewport = (RectTransform)Panel.Find("HierarchyPanelBody");

        float searchTop = -search.offsetMax.y;
        float searchBottom = -search.offsetMin.y;
        float treeTop = -viewport.offsetMax.y;

        Assert.AreEqual(UIStyle.TitleBarH + UIStyle.Space2, searchTop, 0.01f, "поиск — первым под шапкой");
        Assert.GreaterOrEqual(treeTop, searchBottom + UIStyle.Space2 - 0.01f, "дерево начинается ниже поля");
    }

    [Test]
    public void Footer_SaysHowManyAreSelected_AndMoveToFollowsTheSelection()
    {
        var shelf = MakeElement("Полка");
        _panel.SetVisible(true);
        var label = Footer.Find("HierSelected").GetComponent<TMP_Text>();
        var moveTo = Footer.Find("HierMoveTo").GetComponent<TMP_Dropdown>();

        Assert.AreEqual(Loc.F("common.selectedCount", NumberFormat.Integer(0)), label.text);
        Assert.IsFalse(moveTo.interactable,
            "«Переместить в…» над пустым выделением ничего не сделает — выключена (UI-GUIDELINES §9)");

        _selection.Select(shelf);

        Assert.AreEqual(Loc.F("common.selectedCount", NumberFormat.Integer(1)), label.text);
        Assert.IsTrue(moveTo.interactable, "есть выделение — действие над ним доступно");

        _selection.DeselectAll();
        Assert.IsFalse(moveTo.interactable, "выделение снято — действие снова выключено");
    }

    [Test]
    public void EmptyScene_ShowsTheEmptyState_NotABareRoot()
    {
        _panel.SetVisible(true);

        Assert.IsTrue(Panel.Find("HierEmpty").gameObject.activeSelf, "пустая сцена — состояние D9");
        Assert.AreEqual(0, Content.childCount, "и ни одной строки под ним");

        MakeElement("Полка");
        _panel.SetVisible(true);

        Assert.IsFalse(Panel.Find("HierEmpty").gameObject.activeSelf, "появилась деталь — состояние уходит");
        Assert.Greater(Content.childCount, 0);
    }

    [Test]
    public void SearchWithoutMatches_ShowsItsOwnEmptyState()
    {
        MakeElement("Полка");
        _panel.SetVisible(true);

        Panel.Find("HierSearch").GetComponent<TMP_InputField>().text = "zzz";

        Assert.IsTrue(Panel.Find("HierNoMatches").gameObject.activeSelf);
        Assert.IsFalse(Panel.Find("HierEmpty").gameObject.activeSelf, "сцена не пуста — это пустой поиск");
    }

    private void TwoLevels()
    {
        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 2800),
        });
        MakeElement("Полка 1").LevelId = "1";
        MakeElement("Полка 2").LevelId = "2";
        MakeElement("Полка 3").LevelId = "1";
        _panel.SetVisible(true);
    }

    [Test]
    public void LevelRow_IsABoldSectionHeader_WithTheElementCount()
    {
        TwoLevels();

        var row = Row("1 этаж");
        var text = row.Find("Main").GetComponentInChildren<TMP_Text>();

        Assert.AreEqual(FontStyles.Bold, text.fontStyle & FontStyles.Bold, "этаж — строка-заголовок группы Bold (D8)");
        Assert.AreEqual(UIStyle.TextSecondary, text.color);
        Assert.AreEqual(NumberFormat.Integer(2), row.Find("Count").GetComponent<TMP_Text>().text);
        Assert.IsNull(row.Find("Icon"), "у этажа нет иконки типа");
    }

    [Test]
    public void LevelRow_FoldHidesItsRows_AndOnlyItsRows()
    {
        TwoLevels();
        Assume.That(Row("Полка 1"), Is.Not.Null);

        Row("1 этаж").Find("Fold").GetComponent<Button>().onClick.Invoke();

        var labels = new List<string>();
        foreach (Transform row in Content) labels.Add(row.Find("Main").GetComponentInChildren<TMP_Text>().text);
        Assert.That(labels, Has.No.Member("Полка 1"), "свёрнутый этаж прячет свои строки");
        Assert.That(labels, Has.No.Member("Полка 3"));
        Assert.Contains("Полка 2", labels, "чужой этаж остаётся раскрытым");
        Assert.AreEqual(UIStyle.GlyphCollapsed,
            Row("1 этаж").Find("Fold").GetComponentInChildren<TMP_Text>().text, "стрелка сменилась на ►");
    }

    [Test]
    public void ClickOnALevelRow_FoldsItToo_BecauseTheWholeRowIsTheHeader()
    {
        TwoLevels();

        Row("2 этаж").Find("Main").GetComponent<Button>().onClick.Invoke();

        Assert.IsNull(FindRow("Полка 2"), "клик по заголовку этажа сворачивает его");
        Row("2 этаж").Find("Main").GetComponent<Button>().onClick.Invoke();
        Assert.IsNotNull(FindRow("Полка 2"), "и раскрывает обратно");
    }

    private Transform? FindRow(string label)
    {
        foreach (Transform row in Content)
            if (row.Find("Main").GetComponentInChildren<TMP_Text>().text == label) return row;
        return null;
    }

    [Test]
    public void ResizeGrip_IsAFullWidthEightPixelZone_WithAShortBarInTheMiddle()
    {
        var handle = (RectTransform)Panel.Find("ResizeHandle");
        var grip = (RectTransform)handle.Find("Grip");

        Assert.AreEqual(UIStyle.ResizeZoneH, handle.sizeDelta.y, 0.01f);
        Assert.AreEqual(0f, handle.anchorMin.x);
        Assert.AreEqual(1f, handle.anchorMax.x, "зона тянется на всю ширину нижнего края");
        Assert.AreEqual(UIStyle.ResizeGripW, grip.sizeDelta.x, 0.01f);
        Assert.AreEqual(UIStyle.ResizeGripH, grip.sizeDelta.y, 0.01f);
    }

    [Test]
    public void HierarchyPanel_IsBuiltOnTheWindowChrome()
    {
        Assert.IsNotNull(Panel.Find("CloseBtn"), "× — тихая иконная кнопка шапки");
        Assert.IsNotNull(Panel.Find("HierarchyPanelFooter"), "футер с выбранным и «Переместить в…»");
        Assert.AreEqual(UIStyle.HierarchyW, ((RectTransform)Panel).sizeDelta.x);
        Assert.AreEqual(TextAlignmentOptions.Left,
            Panel.Find("HierarchyPanelTitle").GetComponent<TMP_Text>().alignment & TextAlignmentOptions.Left, "заголовок слева (D5)");
    }
}
