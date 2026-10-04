using System.Linq;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

/// <summary>Каталог по макету shell.png (docs/ui-redesign/shell.md, п. 4): поле поиска с
/// подсказкой-хоткеем, категории — строки 32 со счётчиком, плитки 2×N на всю ширину
/// (2 × 122 + 8), тихие иконки режима дока вместо зелёных, рейка с иконками категорий
/// 20 в кнопке 40. Поведение дока (клавиши, пресеты, режимы) держит SidebarPanelTests;
/// здесь только то, что нарисовано и сколько оно занимает.</summary>
public class SidebarShellTests
{
    private GameObject _canvasGo = null!;
    private GameObject _sidebarHost = null!;
    private SidebarUI _sidebar = null!;

    [SetUp]
    public void SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();
        EditModeManager.Reset();
        SidebarUI.ResetLastUsedGroupForTests();
        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();
        _sidebarHost = new GameObject("SidebarHost");
        _sidebar = _sidebarHost.AddComponent<SidebarUI>();
        _sidebar.Build(_canvasGo.transform);
        _sidebar.SetExpandedForTests(true);
    }

    [TearDown]
    public void TearDown()
    {
        if (_sidebarHost != null) Object.DestroyImmediate(_sidebarHost);
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        EditModeManager.Reset();
        SidebarUI.ResetLastUsedGroupForTests();
        PartRegistry.Clear();
        CameraController.CatalogHasLiveKeyboardSelection = false;
    }

    private static Transform Child(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return parent.GetChild(i);

        var present = new StringBuilder();
        for (int i = 0; i < parent.childCount; i++) present.Append(i > 0 ? ", " : "").Append(parent.GetChild(i).name);
        Assert.Fail($"в «{parent.name}» нет дочернего узла «{name}». Есть: [{present}]");
        return null!;
    }

    private Transform Panel => Child(_canvasGo.transform, "Sidebar");

    private RectTransform FullContent => (RectTransform)Child(Child(Panel, "SbFull"), "SbFullContent");

    private RectTransform Header(string group) => (RectTransform)Child(FullContent, "SbGrp_" + group);

    private RectTransform Tile(string group, string title) => (RectTransform)Child(FullContent, "SbTile_" + group + "_" + title);

    private static string FirstGroup => SidebarCatalog.Build()[0].title;

    [Test]
    public void ThePanel_StartsUnderTheToolbarAndAlignsWithItsHeight()
    {
        var panel = (RectTransform)Panel;

        Assert.AreEqual(-UIStyle.ToolbarH, panel.offsetMax.y, 0.01f, "док начинается под тулбаром 48, а не 52");
        Assert.AreEqual(SidebarLayout.DockW, panel.offsetMax.x, 0.01f);
        Assert.AreEqual(268f, SidebarUI.ExpandedW);
        Assert.AreEqual(UIStyle.ToolbarH, SidebarUI.TopOffsetUnderToolbar);
    }

    [Test]
    public void ThePanel_HasAQuietRightRule_NotAFrame()
    {
        var rule = (RectTransform)Child(Panel, "RightRule");

        Assert.AreEqual(UIStyle.Divider, rule.GetComponent<Image>().color);
        Assert.AreEqual(UIStyle.DividerPx, rule.sizeDelta.x, 0.01f);
        Assert.AreEqual(1f, rule.anchorMin.x, "линия прижата к правому краю");
    }

    [Test]
    public void TheSearchField_IsOneCompactRowHigh_WithAHintAndTheShortcutKey()
    {
        var search = (RectTransform)Child(Panel, "SbSearch");
        var hint = Child(search, SidebarSearchHint.NodeName).GetComponent<TMP_Text>();
        var shortcut = Child(search, SidebarSearchHint.ShortcutNodeName).GetComponent<TMP_Text>();

        Assert.AreEqual(UIStyle.ControlHCompact, search.sizeDelta.y, "поле поиска — компактный ряд 28");
        Assert.AreEqual(Loc.T("sidebar.searchHint"), hint.text);
        Assert.AreEqual("/", shortcut.text, "подсказка называет клавишу открытия поиска — по умолчанию «/»");
        Assert.AreEqual(UIStyle.TextSecondary, hint.color);
        Assert.IsTrue(hint.gameObject.activeSelf && shortcut.gameObject.activeSelf);

        search.GetComponent<TMP_InputField>().text = "пол";

        Assert.IsFalse(hint.gameObject.activeSelf, "при вводе подсказка уходит");
        Assert.IsFalse(shortcut.gameObject.activeSelf, "и вместе с ней хоткей");
    }

    [Test]
    public void TheTopStrip_HoldsSearchPinDockAndCollapse_InOneRow_AllQuiet()
    {
        string[] names = { "SbSearch", "SbPin", "SbDockMode", "SbCollapse" };
        var rects = names.Select(n => (RectTransform)Child(Panel, n)).ToList();

        for (int i = 1; i < rects.Count; i++)
        {
            Assert.AreEqual(rects[0].anchoredPosition.y, rects[i].anchoredPosition.y, 0.01f, names[i] + " — в одном ряду с поиском");
            Assert.Greater(rects[i].anchoredPosition.x, rects[i - 1].anchoredPosition.x + rects[i - 1].sizeDelta.x - 0.01f,
                names[i] + " — правее предыдущего без наложения");
        }
        foreach (var n in names.Skip(1))
        {
            var rect = (RectTransform)Child(Panel, n);
            Assert.AreEqual(UIStyle.ControlHCompact, rect.sizeDelta.x, n);
            Assert.AreEqual(UIStyle.ControlHCompact, rect.sizeDelta.y, n);
        }
        Assert.AreEqual(SidebarUI.ExpandedW - SidebarLayout.Pad,
            rects[3].anchoredPosition.x + rects[3].sizeDelta.x, 0.01f, "«свернуть» — у правого края с полем 8");
    }

    [Test]
    public void TheModeToggles_AreQuietIcons_NotGreenFills()
    {
        var pin = Child(Panel, "SbPin").GetComponent<Button>();
        var dock = Child(Panel, "SbDockMode").GetComponent<Button>();

        Assert.AreEqual(UIStyle.SurfaceActive, ((Image)pin.targetGraphic).color,
            "закреплён по умолчанию — «нажата» тем же SurfaceActive, что и кнопки тулбара; зелёный занят смыслом «что будет» (§9)");
        Assert.AreEqual(UIStyle.SurfaceHover, ((Image)dock.targetGraphic).color, "режим дока не выбран — тихая");
        Assert.AreEqual(UIStyle.TintHidden, dock.colors.normalColor);
        Assert.AreEqual(UIStyle.TextSecondary, ToolbarButtonsIcon(dock).color, "иконки режима дока — TextSecondary (shell.md, п. 4)");

        pin.onClick.Invoke();
        Assert.AreEqual(UIStyle.TintHidden, pin.colors.normalColor, "открепили — кнопка снова тихая");
    }

    private static Image ToolbarButtonsIcon(Button button) =>
        button.GetComponentsInChildren<Image>(true).First(i => i != button.targetGraphic);

    [Test]
    public void TheToggleButtons_CarryTooltips()
    {
        foreach (var n in new[] { "SbPin", "SbDockMode", "SbCollapse" })
            Assert.IsNotNull(Child(Panel, n).GetComponent<UnityEngine.EventSystems.EventTrigger>(), n + ": иконка без подсказки не читается");
    }

    [Test]
    public void ACategoryRow_IsThirtyTwoHigh_WithGlyphTitleAndCount()
    {
        var header = Header(FirstGroup);
        var groupTiles = SidebarTileBuilder.BuildTiles(SidebarCatalog.Build()[0].items);

        Assert.AreEqual(SidebarLayout.HeaderH, header.sizeDelta.y);
        Assert.AreEqual(32f, header.sizeDelta.y, "строка категории 32");
        Assert.AreEqual(SidebarUI.ExpandedW - 2f * SidebarLayout.Pad, header.sizeDelta.x, 0.01f, "строка на всю ширину дока за вычетом полей");
        var glyph = header.GetComponentInChildren<TMP_Text>();
        Assert.IsTrue(glyph.text.StartsWith(UIStyle.GlyphExpanded), "первая группа раскрыта: ▼");
        Assert.AreEqual(FirstGroup, Child(header, SidebarChrome.HeaderTitleNode).GetComponent<TMP_Text>().text);
        var count = Child(header, SidebarChrome.HeaderCountNode).GetComponent<TMP_Text>();
        Assert.AreEqual(groupTiles.Count.ToString(), count.text, "счётчик — число плиток группы");
        Assert.AreEqual(UIStyle.FontCaption, count.fontSize);
        Assert.AreEqual(HorizontalAlignmentOptions.Right, count.horizontalAlignment, "счётчик прижат вправо");
    }

    [Test]
    public void ClosedCategoryRows_StandFlushOnThirtyTwoPixelSteps()
    {
        var groups = SidebarCatalog.Build();
        var a = Header(groups[2].title);
        var b = Header(groups[3].title);

        Assert.AreEqual(SidebarLayout.HeaderH, a.anchoredPosition.y - b.anchoredPosition.y, 0.01f,
            "две свёрнутые категории подряд идут встык — строки 32, как в макете (было 38)");
    }

    [Test]
    public void TheCategoryRow_IsAQuietRowButton_HoverIsRowHover()
    {
        var button = Header(FirstGroup).GetComponent<Button>();

        Assert.AreEqual(UIStyle.RowHover, ((Image)button.targetGraphic).color);
        Assert.AreEqual(UIStyle.TintHidden, button.colors.normalColor, "в покое категория без плашки, как в макете");
    }

    [Test]
    public void TwoTiles_FillTheDockWidth_EdgeToEdge()
    {
        var tiles = SidebarTileBuilder.BuildTiles(SidebarCatalog.Build()[0].items);
        Assume.That(tiles.Count, Is.GreaterThanOrEqualTo(2));
        var first = Tile(FirstGroup, tiles[0].title);
        var second = Tile(FirstGroup, tiles[1].title);

        Assert.AreEqual(122f, first.sizeDelta.x, "плитка 122 по макету");
        Assert.AreEqual(120f, first.sizeDelta.y, "высотой 120");
        Assert.AreEqual(SidebarLayout.Pad, first.anchoredPosition.x, 0.01f);
        Assert.AreEqual(SidebarLayout.Pad + 122f + SidebarLayout.TileGap, second.anchoredPosition.x, 0.01f, "зазор 8");
        Assert.AreEqual(SidebarUI.ExpandedW - SidebarLayout.Pad, second.anchoredPosition.x + second.sizeDelta.x, 0.01f,
            "правая плитка кончается в 8 px от края дока — справа не остаётся 30 px пустоты (дефект №6 аудита)");
    }

    [Test]
    public void TheTile_IsARoundedRowHoverFill_WithAHoverRim()
    {
        var tiles = SidebarTileBuilder.BuildTiles(SidebarCatalog.Build()[0].items);
        var tile = Tile(FirstGroup, tiles[0].title);
        var rim = Child(tile, SidebarChrome.TileRimNode);

        Assert.AreEqual(UIStyle.RowHover, tile.GetComponent<Image>().color);
        Assert.IsFalse(rim.gameObject.activeSelf, "рамка видна только под курсором");
        Assert.AreEqual(UIStyle.FieldStroke, rim.GetComponent<Image>().color);
        Assert.IsFalse(rim.GetComponent<Image>().raycastTarget, "рамка не ловит клики");
    }

    [Test]
    public void TheKeyboardSelection_IsTheFocusRing_NotTheYellowSelectionColour()
    {
        var tiles = SidebarTileBuilder.BuildTiles(SidebarCatalog.Build()[0].items);
        var tile = Tile(FirstGroup, tiles[0].title);

        _sidebar.SelectTileForTests(FirstGroup, tiles[0].title);

        var outline = tile.GetComponent<Outline>();
        Assert.IsTrue(outline.enabled);
        Assert.AreEqual(UIStyle.FocusRing, outline.effectColor, "фокус клавиатуры — FocusRing (D10); жёлтый занят выделением в сцене");
    }

    [Test]
    public void TheRail_ShowsTwentyPixelCategoryIconsInFortyPixelButtons()
    {
        Child(Panel, "SbCollapse").GetComponent<Button>().onClick.Invoke();
        var strip = Child(Child(Panel, "SbMini"), "SbMiniContent");
        var groups = SidebarCatalog.Build();

        Assert.AreEqual(56f, SidebarUI.CollapsedW, "рейка 56: кнопка 40 и по 8 с боков");
        foreach (var g in groups)
        {
            var button = (RectTransform)Child(strip, "SbMini_" + g.title);
            var icon = (RectTransform)Child(button, "SbMini_" + g.title + "_Icon");
            Assert.AreEqual(new Vector2(40f, 40f), button.sizeDelta, g.title);
            Assert.AreEqual(new Vector2(20f, 20f), icon.sizeDelta, g.title + ": иконка категории 20 (shell.md, п. 4)");
            Assert.IsTrue(g.icon.texture.name.StartsWith("OutlineIcon_"), g.title + ": иконка из общего контурного набора");
        }
    }

    [Test]
    public void TheCollapseButton_MovesToTheMiddleOfTheRail_AndBack()
    {
        var collapse = (RectTransform)Child(Panel, "SbCollapse");
        float expandedX = collapse.anchoredPosition.x;

        collapse.GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual((SidebarUI.CollapsedW - collapse.sizeDelta.x) * 0.5f, collapse.anchoredPosition.x, 0.01f,
            "в рейке шириной 56 кнопка стоит по центру");

        collapse.GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(expandedX, collapse.anchoredPosition.x, 0.01f, "и возвращается к правому краю");
    }

    [Test]
    public void TheCaptionOfATile_IsTheSmallFont_AndNeverBelowTheFloor()
    {
        var tiles = SidebarTileBuilder.BuildTiles(SidebarCatalog.Build()[0].items);
        var caption = Tile(FirstGroup, tiles[0].title).GetComponentsInChildren<TMP_Text>(true).First();

        Assert.GreaterOrEqual(caption.fontSize, UIStyle.FontMin, "меньше 13 в интерфейсе не бывает (D3)");
    }
}
