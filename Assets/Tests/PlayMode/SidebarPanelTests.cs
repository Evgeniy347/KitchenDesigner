using System.Collections;
using System.Collections.Generic;
using System.IO;
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
using KitchenDesigner.Tests;

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
    public void Caption_ShowsTileTitleForMultiPreset_AndItemDisplayNameForSinglePreset()
    {
        var fittingCaption = Tile("Сантехника", "Фитинг").GetComponentInChildren<TMP_Text>();
        Assert.AreEqual("Фитинг", fittingCaption.text,
            "многопресетная плитка подписана заголовком плитки, а не именем пресета номер ноль");

        var shelfCaption = Tile("Детали", "Полка").GetComponentInChildren<TMP_Text>();
        Assert.AreEqual("Полка", shelfCaption.text,
            "однопресетная плитка подписана именем своей единственной позиции");
    }

    [UnityTest]
    public IEnumerator SwitchingPreset_RerendersTheThumbnail_ForTheNewlySelectedPreset()
    {
        Header("Ящики").onClick.Invoke();
        var tile = Tile("Ящики", "Ящик");
        var thumb = Child(tile, "Thumb").GetComponent<RawImage>();
        var stub = Child(tile, "Stub").GetComponent<Image>();

        for (int i = 0; i < 10 && thumb.texture == null; i++) yield return null;
        Assert.IsNotNull(thumb.texture, "картинка первого пресета обязана отрисоваться до переключения");

        var presets = Child(tile, "Presets");
        Child(presets, "PresetDot_1").GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(stub.gameObject.activeSelf,
            "после смены пресета заглушка обязана вернуться, пока не отрисована новая картинка — "
            + "иначе на плитке молча останется картинка старого пресета");

        for (int i = 0; i < 10 && stub.gameObject.activeSelf; i++) yield return null;

        Assert.IsFalse(stub.gameObject.activeSelf,
            "картинка нового пресета обязана дорисоваться за несколько кадров, как и при первом показе");
        Assert.IsNotNull(thumb.texture, "у плитки обязана остаться картинка после переключения пресета");
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
        // The dock starts expanded or collapsed depending on Screen.height
        // (SidebarDockBudget.AutoCollapsesAt), which the Test Runner's window size can flip
        // between an isolated run and a full-suite run — the test used to Assume the pin was
        // visible first and went Inconclusive whenever the ambient screen happened to be short
        // enough to auto-collapse. Force the expanded state so the premise always holds.
        _sidebar.SetExpandedForTests(true);
        var pin = Child(Panel, "SbPin");
        Assert.IsTrue(pin.gameObject.activeSelf,
            "распахнутый док обязан показывать булавку — иначе клик по «свернуть» ниже ничего не проверяет");

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

    [Test]
    public void Docked_StaysExpanded_AfterAnElementIsSpawned()
    {
        _sidebar.SetExpandedForTests(true);
        _sidebar.SetDockChoiceForTests(SidebarDockChoice.Docked);

        _sidebar.CollapseAfterSpawnForTests();

        Assert.IsTrue(Full.gameObject.activeSelf,
            "пользователь выбрал раскрытый док — после установки детали каталог обязан "
            + "остаться раскрытым, чтобы можно было поставить подряд десять полок");
    }

    [Test]
    public void Rail_CollapsesBackToTheIconStrip_AfterAnElementIsSpawned()
    {
        _sidebar.SetExpandedForTests(true);
        _sidebar.SetDockChoiceForTests(SidebarDockChoice.Rail);

        _sidebar.CollapseAfterSpawnForTests();

        Assert.IsFalse(Full.gameObject.activeSelf,
            "пользователь выбрал рейку иконок — после установки детали каталог обязан "
            + "свернуться обратно, как закрывается палитра по вызову");
        var mini = Child(Panel, "SbMini");
        Assert.IsTrue(mini.gameObject.activeSelf);
    }

    [Test]
    public void DockModeButton_TogglesTheChoice_AndPersistsItForTheNextLaunch()
    {
        bool hadPrevValue = PlayerPrefs.HasKey("KitchenSidebarDockChoice");
        int prevValue = PlayerPrefs.GetInt("KitchenSidebarDockChoice", 0);
        try
        {
            _sidebar.SetExpandedForTests(true);
            _sidebar.SetDockChoiceForTests(SidebarDockChoice.Rail);

            Child(Panel, "SbDockMode").GetComponent<Button>().onClick.Invoke();

            Assert.IsTrue(Full.gameObject.activeSelf,
                "переключатель обязан немедленно раскрыть каталог, выбрав «раскрытый док»");
            Assert.AreEqual(SidebarDockChoice.Docked, SidebarDockPreference.Load(),
                "выбор обязан лечь в PlayerPrefs сразу по клику — иначе следующий запуск "
                + "программы снова спросит высоту экрана");
        }
        finally
        {
            if (hadPrevValue) PlayerPrefs.SetInt("KitchenSidebarDockChoice", prevValue);
            else PlayerPrefs.DeleteKey("KitchenSidebarDockChoice");
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void UserChoice_SurvivesARestart_ThroughTheSamePlayerPrefsKey_NotInTheProjectFile()
    {
        bool hadPrevValue = PlayerPrefs.HasKey("KitchenSidebarDockChoice");
        int prevValue = PlayerPrefs.GetInt("KitchenSidebarDockChoice", 0);
        var restartedCanvasGo = new GameObject("CanvasAfterRestart");
        var restartedHost = new GameObject("SidebarHostAfterRestart");
        try
        {
            SidebarDockPreference.Save(SidebarDockChoice.Rail);

            restartedCanvasGo.AddComponent<Canvas>();
            var restarted = restartedHost.AddComponent<SidebarUI>();
            restarted.Build(restartedCanvasGo.transform);

            var restartedFull = Child(Child(restartedCanvasGo.transform, "Sidebar"), "SbFull");
            Assert.IsFalse(restartedFull.gameObject.activeSelf,
                "«перезапуск» — это просто новый экземпляр SidebarUI, читающий тот же ключ "
                + "PlayerPrefs, а не файл проекта: он обязан стартовать свёрнутым в рейку, "
                + "как и было выбрано ДО перезапуска, независимо от высоты экрана тестового "
                + "раннера");
        }
        finally
        {
            Object.DestroyImmediate(restartedHost);
            Object.DestroyImmediate(restartedCanvasGo);
            if (hadPrevValue) PlayerPrefs.SetInt("KitchenSidebarDockChoice", prevValue);
            else PlayerPrefs.DeleteKey("KitchenSidebarDockChoice");
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void SearchField_MatchesByTheTileTitle_NotOnlyByPresetNames()
    {
        var search = Child(Panel, "SbSearch").GetComponent<TMP_InputField>();
        search.text = "фитинг";

        var fittingTile = Tile("Сантехника", "Фитинг");
        Assert.IsTrue(fittingTile.gameObject.activeSelf,
            "заголовок плитки лежит в данных (SidebarCatalogTable) и обязан участвовать в "
            + "поиске: ни один из шести пресетов не называется «фитинг», только сама плитка");
    }

    [Test]
    public void PresetDot_CarriesAVisibleOrdinalLabel_NotJustAColor()
    {
        var tile = Tile("Сантехника", "Фитинг");
        var firstDot = Child(Child(tile, "Presets"), "PresetDot_0");

        var label = firstDot.GetComponentInChildren<TMP_Text>();
        Assert.IsNotNull(label,
            "точка пресета обязана нести различимую метку — на 16 px один цвет от другого "
            + "не отличить без наведения");
        Assert.AreEqual("1", label!.text,
            "метка — порядковый номер пресета, а не буква-псевдоиконка");
    }

    private static void Fire(GameObject go, EventTriggerType type)
    {
        var trigger = go.GetComponent<EventTrigger>();
        Assert.IsNotNull(trigger, $"на «{go.name}» нет EventTrigger — подсказку не повесили");
        foreach (var entry in trigger!.triggers)
            if (entry.eventID == type)
                entry.callback.Invoke(new PointerEventData(EventSystem.current));
    }

    [UnityTest]
    public IEnumerator TileTooltip_ReflectsTheCurrentlySelectedPreset_NotTheOneAtBuildTime()
    {
        // Раскрытая ГРУППА не значит раскрытый ДОК: SidebarDockBudget.CollapsesAfterSpawn
        // при малом Screen.height (batchMode) уже решил при Build(), что панель стартует
        // свёрнутой в мини-режим, и ApplyState() гасит SbFull целиком — раскрытые
        // group.open тогда сидят под неактивным предком, GetComponentInParent<Canvas>
        // его не видит, и TooltipUI.EnsureOnCanvasOf молча ничего не находит.
        // ExpandAllGroupsForTests раскрывает и сам док (SetExpanded(true)), не только
        // группы.
        _sidebar.ExpandAllGroupsForTests();

        var tile = Tile("Сантехника", "Фитинг");
        TestContext.WriteLine($"плитка активна в иерархии: {tile.gameObject.activeInHierarchy}");

        var presets = Child(tile, "Presets");
        Child(presets, "PresetDot_2").GetComponent<Button>().onClick.Invoke();

        Fire(tile.gameObject, EventTriggerType.PointerEnter);
        yield return new WaitForSecondsRealtime(1f);

        var canvas = tile.GetComponentInParent<Canvas>();
        TestContext.WriteLine($"канва найдена от плитки через GetComponentInParent: {canvas != null}");

        var tooltipNode = _canvasGo.transform.Find("Tooltip");
        TestContext.WriteLine($"узел «Tooltip» создан на канве: {tooltipNode != null}");
        Assert.IsNotNull(tooltipNode,
            "подсказка не навесилась вовсе: TooltipUI.EnsureOnCanvasOf не нашёл Canvas от "
            + "плитки (наведённая плитка должна быть активной в раскрытом доке)");

        var label = tooltipNode!.GetComponentInChildren<TMP_Text>();
        TestContext.WriteLine($"текстовый компонент на подсказке: {label != null}");
        Assert.IsNotNull(label, "у узла «Tooltip» нет текстового компонента");

        TestContext.WriteLine($"текст подсказки: '{label!.text}'");
        Assert.IsTrue(label.text.Contains("Тройник"),
            "подсказка плитки обязана показывать НЫНЕ выбранный пресет: она привязана один "
            + "раз при сборке, и если текст не пересчитывается на каждом наведении, здесь "
            + "останется имя пресета номер ноль вместо выбранного");
    }

    [Test]
    public void DockModeButton_HasNoLetterLabel_ShowsAnIconInstead()
    {
        var dockModeBtn = Child(Panel, "SbDockMode");

        Assert.IsNull(dockModeBtn.GetComponentInChildren<TMP_Text>(),
            "буквы «Д»/«Р» — псевдоиконки, запрещённые правилом 4: переключатель режима "
            + "дока обязан рисоваться спрайтом, как остальные иконки IconFactory");
        var icon = Child(dockModeBtn, "SbDockMode_Icon").GetComponent<Image>();
        Assert.IsNotNull(icon.sprite);
    }

    [Test]
    public void SelectingAPreset_PersistsAsTheLastUsedPreset_ForTheNextBuildOfTheSameTile()
    {
        const string key = "KitchenSidebarPreset_Ящик";
        bool hadPrevValue = PlayerPrefs.HasKey(key);
        string prevValue = PlayerPrefs.GetString(key, "");
        try
        {
            var drawerTile = Tile("Ящики", "Ящик");
            var presets = Child(drawerTile, "Presets");
            Child(presets, "PresetDot_1").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual("Ящик Movento", SidebarPresetPreference.Load("Ящик"),
                "клик по точке пресета обязан немедленно запомнить выбор — так же, как "
                + "запоминается режим дока");

            Object.DestroyImmediate(_sidebarHost);
            Object.DestroyImmediate(_canvasGo);
            _canvasGo = new GameObject("Canvas");
            _canvasGo.AddComponent<Canvas>();
            _sidebarHost = new GameObject("SidebarHost");
            _sidebar = _sidebarHost.AddComponent<SidebarUI>();
            _sidebar.Build(_canvasGo.transform);

            var rebuiltTile = Tile("Ящики", "Ящик");
            var rebuiltPresets = Child(rebuiltTile, "Presets");
            var secondDot = Child(rebuiltPresets, "PresetDot_1").GetComponent<Image>();
            AssertSameColor(UIStyle.SurfaceActive, secondDot.color,
                "плитку пересобрали заново («перезапуск»), и она обязана открыться на "
                + "последнем использованном пресете, а не на пресете номер ноль");
        }
        finally
        {
            if (hadPrevValue) PlayerPrefs.SetString(key, prevValue);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    [UnityTest]
    public IEnumerator SlashShortcut_DefersActivatingSearchByOneFrame_SoTheSlashCannotLeakIntoIt()
    {
        _sidebar.SetExpandedForTests(false);
        var search = Child(Panel, "SbSearch").GetComponent<TMP_InputField>();

        _sidebar.SimulateSlashShortcutForTests();

        Assert.IsFalse(search.isFocused,
            "поле поиска не должно получать фокус в ТОМ ЖЕ кадре, где сработал шорткат — "
            + "EventSystem мог ещё не разослать символ «/» этого нажатия, и активированное "
            + "прямо сейчас поле получило бы его в довесок");

        yield return null;
        yield return null;

        Assert.IsTrue(search.isFocused,
            "на следующем кадре поле поиска обязано получить фокус");
    }

    /// <summary>Сенсор на дефект, который прожил незамеченным при полном покрытии тестами:
    /// подпись плитки нигде не выставлялась (`Item.DisplayName`/`tile.title` не читались), и
    /// картинка всегда рендерилась для нулевого пресета, поэтому «Ящик Movento», «Варочная
    /// Bosch» и все пресеты фитингов кроме первого не получали её никогда. Ни `Tile_HasA
    /// ThumbnailSlotAndAPlaceholderStub`, ни `Tile_ThumbnailAppears_AfterAFewFramesOfLazy
    /// Generation` этого не ловили — оба проверяли ровно одну плитку с ровно одним пресетом.
    /// Здесь проверка идёт по ВСЕМ плиткам каталога и по факту содержимого (непустой текст,
    /// текстура с реальными размерами), а не по `!= null`.</summary>
    [UnityTest]
    public IEnumerator EveryTile_HasANonEmptyCaption_AndARenderedThumbnail()
    {
        _sidebar.ExpandAllGroupsForTests();

        var allTiles = SidebarCatalog.Build()
            .SelectMany(g => SidebarTileBuilder.BuildTiles(g.items).Select(t => (g.title, t)))
            .ToList();

        int framesNeeded = allTiles.Count / 2 + 4;
        for (int i = 0; i < framesNeeded; i++) yield return null;

        var emptyCaptions = new List<string>();
        var missingThumbnails = new List<string>();

        foreach (var (groupTitle, tile) in allTiles)
        {
            var node = Tile(groupTitle, tile.title);

            var caption = node.GetComponentInChildren<TMP_Text>();
            if (caption == null || string.IsNullOrWhiteSpace(caption.text))
                emptyCaptions.Add(groupTitle + "/" + tile.title);

            var thumb = Child(node, "Thumb").GetComponent<RawImage>();
            if (!(thumb.texture is RenderTexture rt) || rt.width <= 0 || rt.height <= 0)
                missingThumbnails.Add(groupTitle + "/" + tile.title);
        }

        Assert.IsEmpty(emptyCaptions,
            "плитки без подписи (" + emptyCaptions.Count + "): " + string.Join(", ", emptyCaptions));
        Assert.IsEmpty(missingThumbnails,
            "плитки без отрисованной картинки (" + missingThumbnails.Count + "): "
            + string.Join(", ", missingThumbnails));
    }

    [UnityTest]
    public IEnumerator ExpandedDock_WithTilesSearchHeadersAndPresetDots_MatchesUiSnapshot()
    {
        _sidebar.SetDockChoiceForTests(SidebarDockChoice.Docked);
        _sidebar.SetExpandedForTests(true);
        Header("Сантехника").onClick.Invoke();
        _sidebar.ShowAllPresetRowsForTests();

        int visibleTileCount = SidebarCatalog.Build()
            .Where(g => g.title == "Детали" || g.title == "Сантехника")
            .Sum(g => SidebarTileBuilder.BuildTiles(g.items).Count);
        int framesNeeded = visibleTileCount / 2 + 4;
        for (int i = 0; i < framesNeeded; i++) yield return null;

        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        UiSnapshotEngine.CaptureVerified(_canvasGo, Path.Combine(dir, "sidebar_dock_expanded.json"));
    }
}
