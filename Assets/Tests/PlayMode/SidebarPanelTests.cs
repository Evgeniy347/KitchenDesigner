using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Левый док каталога после перехода на плитки (docs/todo_evolution.md §2.4,
/// вариант A): поиск первым контролом, свёрнутые по умолчанию все группы кроме
/// последней использованной, сетка 2×N плиток 96×96 с картинкой и подписью,
/// пресеты — ряд точек на плитке при наведении, рейка иконок групп в свёрнутом
/// состоянии (без изменений с прошлой версии).
///
/// PlayMode по той же причине, что и раньше: сайдбар подписывается на статическое
/// событие EditModeManager.Changed и отписывается в OnDestroy, которого EditMode
/// не зовёт.
///
/// Узлы ищет <see cref="Child"/>, а не Transform.Find — заголовки и плитки носят
/// человекочитаемые русские имена, и в них попадаются символы, которые Find
/// трактует как разделитель пути («ДВП/ХДФ»).</summary>
public class SidebarPanelTests
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
    }

    [TearDown]
    public void TearDown()
    {
        if (_sidebarHost != null) Object.DestroyImmediate(_sidebarHost);
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        EditModeManager.Reset();
        SidebarUI.ResetLastUsedGroupForTests();
        PartRegistry.Clear();
    }

    private static Transform Child(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return parent.GetChild(i);

        var present = new StringBuilder();
        for (int i = 0; i < parent.childCount; i++)
        {
            if (i > 0) present.Append(", ");
            present.Append(parent.GetChild(i).name);
        }

        Assert.Fail($"в «{parent.name}» нет дочернего узла «{name}». Есть: [{present}]");
        return null!;
    }

    private Transform Panel => Child(_canvasGo.transform, "Sidebar");

    private Transform Full => Child(Panel, "SbFull");

    private RectTransform FullContent => (RectTransform)Child(Full, "SbFullContent");

    private static string TileNode(string group, string title) => "SbTile_" + group + "_" + title;

    private Transform Tile(string group, string title) => Child(FullContent, TileNode(group, title));

    private Button Header(string group)
        => Child(FullContent, "SbGrp_" + group).GetComponent<Button>();

    private static void AssertSameColor(Color expected, Color actual, string message)
    {
        Assert.AreEqual(expected.r, actual.r, 0.01f, message);
        Assert.AreEqual(expected.g, actual.g, 0.01f, message);
        Assert.AreEqual(expected.b, actual.b, 0.01f, message);
    }

    private static float BottomY(RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return corners[0].y;
    }

    private static float TopY(RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return corners[1].y;
    }

    [Test]
    public void SearchField_IsTheFirstControlOfTheDock()
    {
        var search = Child(Panel, "SbSearch");
        Assert.IsNotNull(search.GetComponent<TMP_InputField>(),
            "поле поиска обязано существовать в доке — оно должно взять поиск у сцены и ошибок");

        var searchTop = TopY((RectTransform)search);
        var fullTop = TopY((RectTransform)Full);
        Assert.Greater(searchTop, fullTop,
            "поле поиска стоит НАД сеткой плиток — первый контрол дока");
    }

    [Test]
    public void GroupHeader_PutsTheGlyphBeforeTheTitle_AndFlipsItOnToggle()
    {
        var header = Header("Мебель");
        var label = header.GetComponentInChildren<TMP_Text>();
        bool startedOpen = label.text.StartsWith(UIStyle.GlyphExpanded);

        header.onClick.Invoke();

        string expectedGlyph = startedOpen ? UIStyle.GlyphCollapsed : UIStyle.GlyphExpanded;
        Assert.IsTrue(label.text.StartsWith(expectedGlyph),
            "глиф стоит слева от названия и переключается по клику");
    }

    [Test]
    public void OnlyOneGroup_IsOpenByDefault()
    {
        int openCount = 0;
        foreach (var g in SidebarCatalog.Build())
        {
            var label = Header(g.title).GetComponentInChildren<TMP_Text>();
            if (label.text.StartsWith(UIStyle.GlyphExpanded)) openCount++;
        }

        Assert.AreEqual(1, openCount,
            "по умолчанию раскрыта РОВНО одна группа — последняя использованная "
            + "(docs/todo_evolution.md §2.4)");
    }

    [Test]
    public void Tile_HasAThumbnailSlotAndAPlaceholderStub()
    {
        var tile = Tile("Детали", "Полка");

        var thumb = Child(tile, "Thumb").GetComponent<RawImage>();
        var stub = Child(tile, "Stub").GetComponent<Image>();

        Assert.IsNotNull(thumb, "у плитки обязан быть RawImage под картинку ThumbnailRenderer");
        Assert.IsTrue(stub.gameObject.activeSelf,
            "пока картинка не готова, плитка обязана показывать заглушку, а не пустоту");
        Assert.AreEqual(IconFactory.TileStub, stub.sprite);
    }

    [UnityTest]
    public IEnumerator Tile_ThumbnailAppears_AfterAFewFramesOfLazyGeneration()
    {
        var tile = Tile("Детали", "Полка");
        var stub = Child(tile, "Stub").GetComponent<Image>();
        var thumb = Child(tile, "Thumb").GetComponent<RawImage>();

        for (int i = 0; i < 10 && thumb.texture == null; i++)
            yield return null;

        Assert.IsNotNull(thumb.texture,
            "генератор миниатюр обязан лениво заполнить картинку за несколько кадров");
        Assert.IsFalse(stub.gameObject.activeSelf,
            "как только картинка готова, заглушку убирают");
    }

    [Test]
    public void MultiPresetTile_ShowsAllOriginalItemsAsPresetDots()
    {
        var tile = Tile("Сантехника", "Фитинг");
        var presets = Child(tile, "Presets");

        Assert.AreEqual(6, presets.childCount,
            "шесть фитингов одной трубы обязаны остаться доступны — все шесть, как пресеты");
    }

    [Test]
    public void SinglePresetTile_HasNoPresetDots()
    {
        var tile = Tile("Детали", "Полка");
        var presets = Child(tile, "Presets");

        Assert.AreEqual(0, presets.childCount);
    }

    [Test]
    public void EveryCatalogItem_HasItsTileInTheContent()
    {
        var present = new HashSet<string>();
        for (int i = 0; i < FullContent.childCount; i++)
            present.Add(FullContent.GetChild(i).name);

        foreach (var g in SidebarCatalog.Build())
            foreach (var tile in SidebarTileBuilder.BuildTiles(g.items))
                Assert.IsTrue(present.Contains(TileNode(g.title, tile.title)),
                    $"плитки «{tile.title}» из группы «{g.title}» нет среди узлов каталога");
    }

    [Test]
    public void MissingNode_IsReportedByName_NotAsANullReference()
    {
        var ex = Assert.Throws<AssertionException>(() => Child(Full, "SbNoSuchNode"));

        Assert.IsNotNull(ex);
        StringAssert.Contains("SbNoSuchNode", ex!.Message);
        StringAssert.Contains("SbFullContent", ex.Message);
    }

    [Test]
    public void Panel_TakesItsHeightFromTheScreen_AndLeavesTheStatusChipAlone()
    {
        var panel = (RectTransform)Panel;
        var canvas = (RectTransform)_canvasGo.transform;

        Assert.Greater(BottomY(panel), BottomY(canvas));
        Assert.GreaterOrEqual(BottomY(panel), BottomY(canvas) + 34f);
    }

    [Test]
    public void Panel_ExpandedWidth_Is260Pixels()
    {
        Assert.AreEqual(260f, SidebarUI.ExpandedW);
    }

    [Test]
    public void Collapsed_ShowsGroupIconsAndHidesThePinAndSearch()
    {
        var pin = Child(Panel, "SbPin");
        Assume.That(pin.gameObject.activeSelf, Is.True);

        Child(Panel, "SbCollapse").GetComponent<Button>().onClick.Invoke();

        Assert.IsFalse(Full.gameObject.activeSelf);
        Assert.IsFalse(Child(Panel, "SbSearch").gameObject.activeSelf,
            "поиск не нужен в рейке шириной 52 px");
        var mini = Child(Panel, "SbMini");
        Assert.IsTrue(mini.gameObject.activeSelf);
        var strip = Child(mini, "SbMiniContent");
        var techBtn = Child(strip, "SbMini_Техника");

        Assert.IsNull(techBtn.GetComponentInChildren<TMP_Text>(),
            "буквы-псевдоиконки запрещены — в узкой полосе группа рисуется иконкой");
        var icon = Child(techBtn, "SbMini_Техника_Icon").GetComponent<Image>();
        Assert.AreEqual(IconFactory.Appliance, icon.sprite);
        Assert.IsNotNull(techBtn.gameObject.GetComponent<EventTrigger>());
        Assert.IsFalse(pin.gameObject.activeSelf);
    }

    [Test]
    public void CollapsedStrip_AlsoScrolls_SoNewGroupsCannotHideBelowTheEdge()
    {
        var mini = Child(Panel, "SbMini");
        var content = (RectTransform)Child(mini, "SbMiniContent");
        var scroll = mini.GetComponent<ScrollRect>();

        Assert.IsNotNull(scroll);
        Assert.AreSame(content, scroll.content);
        Assert.AreEqual(SidebarLayout.MiniContentHeight(SidebarCatalog.Build().Count),
            content.sizeDelta.y);
    }

    [Test]
    public void ClickingAGroupIconInTheRail_ExpandsTheDockOnThatGroup()
    {
        Child(Panel, "SbCollapse").GetComponent<Button>().onClick.Invoke();
        var mini = Child(Panel, "SbMini");
        var strip = Child(mini, "SbMiniContent");
        Child(strip, "SbMini_Техника").GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(Full.gameObject.activeSelf, "клик по иконке категории раскрывает док");
        var label = Header("Техника").GetComponentInChildren<TMP_Text>();
        Assert.IsTrue(label.text.StartsWith(UIStyle.GlyphExpanded), "и открывает именно эту группу");
    }

    [Test]
    public void SearchField_HidesTilesThatDoNotMatchByName()
    {
        var search = Child(Panel, "SbSearch").GetComponent<TMP_InputField>();
        search.text = "Полка";

        var shelf = Tile("Детали", "Полка");
        Assert.IsTrue(shelf.gameObject.activeSelf, "совпавшая плитка остаётся видна при поиске");

        var table = Tile("Мебель", "Прямоугольный стол");
        Assert.IsFalse(table.gameObject.activeSelf,
            "плитка, ни один пресет которой не совпал с поиском, обязана скрыться");
    }

    [Test]
    public void SearchField_MatchesByAnyPresetName_NotOnlyTheTileTitle()
    {
        var search = Child(Panel, "SbSearch").GetComponent<TMP_InputField>();
        search.text = "Movento";

        var drawerTile = Tile("Ящики", "Ящик");
        Assert.IsTrue(drawerTile.gameObject.activeSelf,
            "плитка находится по имени ЛЮБОГО своего пресета, а не только по заголовку плитки");
    }

    [Test]
    public void RoomMode_GraysOutRegularTiles_ButKeepsWallsAndAlwaysItems()
    {
        var shelf = Tile("Детали", "Полка").GetComponent<Button>();
        var wall = Tile("Помещение", "Стена").GetComponent<Button>();
        var korob = Tile("Помещение", EditModeManager.KorobName).GetComponent<Button>();
        var shelfLabel = shelf.GetComponentInChildren<TMP_Text>();
        Color normalColor = shelfLabel.color;

        EditModeManager.SetMode(EditMode.Room);

        Assert.IsFalse(shelf.interactable,
            "в режиме помещения обычные детали не добавляются");
        AssertSameColor(UIStyle.TextDisabled, shelfLabel.color,
            "недоступный пункт обязан выглядеть недоступным — цвет TextDisabled");
        Assert.IsTrue(wall.interactable);
        Assert.IsTrue(korob.interactable, "короб доступен в любом режиме");

        EditModeManager.Reset();

        Assert.IsTrue(shelf.interactable);
        AssertSameColor(normalColor, shelfLabel.color,
            "вернувшись в обычный режим, доступная позиция снова окрашена обычным цветом");
    }
}
