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
        CameraController.CatalogHasLiveKeyboardSelection = false;
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

    private static List<SidebarTileBuilder.Tile> TilesOf(string groupTitle) =>
        SidebarTileBuilder.BuildTiles(SidebarCatalog.Build().Find(g => g.title == groupTitle).items);

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
        _sidebar.SetExpandedForTests(true);
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
        _sidebar.SetExpandedForTests(true);
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

    /// <summary>Дефект приёмки: поиск «Movento» находил и показывал плитку «Ящик» по имени
    /// пресета, но клик всё равно ставил ЗАПОМНЕННЫЙ пресет (GTV), потому что тест выше
    /// проверял только видимость плитки, а не то, что реально уходит в
    /// <see cref="SidebarSpawnRouter.Route"/>. Здесь проверяется РЕЗУЛЬТАТ поиска — какой
    /// именно пресет плитка отдаст на спавн — через
    /// <see cref="SidebarUI.ItemToSpawnForTests"/>, без поднятия полного UIManager.</summary>
    [Test]
    public void SearchField_MatchesByAnyPresetName_AndSpawnsTheMatchedPresetNotTheRememberedOne()
    {
        // Запомненный пресет читается из PlayerPrefs — общего с реальным Editor-сеансом
        // хранилища, а не сброшенного между тестами состояния. Тест фиксирует «запомнен
        // GTV» ЯВНО (как соседний тест ниже фиксирует «запомнен Movento»), а не полагается
        // на то, что ключ окажется пустым: иначе ручная проверка Movento-пресета в этом же
        // Editor-проекте (у него теперь настоящие, не дефолтные значения) молча подменяет
        // «запомненный по умолчанию» на Movento, и тест перестаёт ловить исходный дефект.
        const string key = "KitchenSidebarPreset_Ящик";
        bool hadPrevValue = PlayerPrefs.HasKey(key);
        string prevValue = PlayerPrefs.GetString(key, "");
        try
        {
            SidebarPresetPreference.Save("Ящик", "Ящик GTV");
            Object.DestroyImmediate(_sidebarHost);
            Object.DestroyImmediate(_canvasGo);
            _canvasGo = new GameObject("Canvas");
            _canvasGo.AddComponent<Canvas>();
            _sidebarHost = new GameObject("SidebarHost");
            _sidebar = _sidebarHost.AddComponent<SidebarUI>();
            _sidebar.Build(_canvasGo.transform);

            var search = Child(Panel, "SbSearch").GetComponent<TMP_InputField>();

            var spawnedWithoutSearch = _sidebar.ItemToSpawnForTests("Ящики", "Ящик");
            Assert.AreEqual("Ящик GTV", spawnedWithoutSearch.name,
                "без поиска плитка отдаёт запомненный по умолчанию пресет (GTV)");

            search.text = "Movento";
            var spawnedWithSearch = _sidebar.ItemToSpawnForTests("Ящики", "Ящик");

            Assert.AreEqual("Ящик Movento", spawnedWithSearch.name,
                "совпадение по ИМЕНИ ВАРИАНТА обязано переключить выбор на него — иначе клик "
                + "по найденной плиткой ставит не то, что было найдено");
        }
        finally
        {
            if (hadPrevValue) PlayerPrefs.SetString(key, prevValue);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void SearchField_MatchingOnlyTheTileTitle_DoesNotDisturbTheRememberedPreset()
    {
        const string key = "KitchenSidebarPreset_Ящик";
        bool hadPrevValue = PlayerPrefs.HasKey(key);
        string prevValue = PlayerPrefs.GetString(key, "");
        try
        {
            SidebarPresetPreference.Save("Ящик", "Ящик Movento");
            Object.DestroyImmediate(_sidebarHost);
            Object.DestroyImmediate(_canvasGo);
            _canvasGo = new GameObject("Canvas");
            _canvasGo.AddComponent<Canvas>();
            _sidebarHost = new GameObject("SidebarHost");
            _sidebar = _sidebarHost.AddComponent<SidebarUI>();
            _sidebar.Build(_canvasGo.transform);

            var search = Child(Panel, "SbSearch").GetComponent<TMP_InputField>();
            search.text = "Ящик";

            var spawned = _sidebar.ItemToSpawnForTests("Ящики", "Ящик");

            Assert.AreEqual("Ящик Movento", spawned.name,
                "«Ящик» совпадает с ЗАГОЛОВКОМ плитки (её найдёт любой пресет), а не с "
                + "конкретным вариантом — общее слово не обязано переключать запомненный выбор");
        }
        finally
        {
            if (hadPrevValue) PlayerPrefs.SetString(key, prevValue);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
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
    public void SelectingAGroup_PersistsAsTheLastUsedGroup_ForTheNextBuild()
    {
        const string key = "KitchenSidebarLastGroup";
        bool hadPrevValue = PlayerPrefs.HasKey(key);
        string prevValue = PlayerPrefs.GetString(key, "");
        try
        {
            Header("Ящики").onClick.Invoke();

            Assert.AreEqual("Ящики", PlayerPrefs.GetString(key),
                "раскрытие группы обязано немедленно запомнить её как последнюю "
                + "использованную — так же, как запоминается пресет и режим дока");

            Object.DestroyImmediate(_sidebarHost);
            Object.DestroyImmediate(_canvasGo);
            _canvasGo = new GameObject("Canvas");
            _canvasGo.AddComponent<Canvas>();
            _sidebarHost = new GameObject("SidebarHost");
            _sidebar = _sidebarHost.AddComponent<SidebarUI>();
            _sidebar.Build(_canvasGo.transform);

            var label = Header("Ящики").GetComponentInChildren<TMP_Text>();
            Assert.IsTrue(label.text.StartsWith(UIStyle.GlyphExpanded),
                "плитку пересобрали заново, и открытой обязана оказаться последняя "
                + "использованная группа");
        }
        finally
        {
            if (hadPrevValue) PlayerPrefs.SetString(key, prevValue);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Как <see cref="UserChoice_SurvivesARestart_ThroughTheSamePlayerPrefsKey_NotInTheProjectFile"/>,
    /// но для последней использованной группы: было статическое поле <c>_lastUsedGroupTitle</c>,
    /// которое переживало только текущий запуск программы, а не сам проект — то же, от чего
    /// уже избавили выбор дока и выбор пресета.</summary>
    [Test]
    public void LastUsedGroup_SurvivesARestart_ThroughTheSamePlayerPrefsKey_NotInTheProjectFile()
    {
        const string key = "KitchenSidebarLastGroup";
        bool hadPrevValue = PlayerPrefs.HasKey(key);
        string prevValue = PlayerPrefs.GetString(key, "");
        var restartedCanvasGo = new GameObject("CanvasAfterRestart");
        var restartedHost = new GameObject("SidebarHostAfterRestart");
        try
        {
            PlayerPrefs.SetString(key, "Ящики");
            PlayerPrefs.Save();

            restartedCanvasGo.AddComponent<Canvas>();
            var restarted = restartedHost.AddComponent<SidebarUI>();
            restarted.Build(restartedCanvasGo.transform);

            var restartedContent = (RectTransform)Child(
                Child(Child(restartedCanvasGo.transform, "Sidebar"), "SbFull"), "SbFullContent");
            var label = Child(restartedContent, "SbGrp_Ящики").GetComponentInChildren<TMP_Text>();

            Assert.IsTrue(label.text.StartsWith(UIStyle.GlyphExpanded),
                "«перезапуск» — новый экземпляр SidebarUI, читающий тот же ключ PlayerPrefs, "
                + "а не файл проекта: он обязан открыться на группе, выбранной ДО перезапуска");
        }
        finally
        {
            Object.DestroyImmediate(restartedHost);
            Object.DestroyImmediate(restartedCanvasGo);
            if (hadPrevValue) PlayerPrefs.SetString(key, prevValue);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    [UnityTest]
    public IEnumerator SwitchingPreset_ReleasesThePreviousThumbnailRenderTexture()
    {
        _sidebar.SetExpandedForTests(true);
        Header("Ящики").onClick.Invoke();
        var tile = Tile("Ящики", "Ящик");
        var thumb = Child(tile, "Thumb").GetComponent<RawImage>();

        for (int i = 0; i < 10 && thumb.texture == null; i++) yield return null;
        var firstTexture = thumb.texture as RenderTexture;
        Assert.IsNotNull(firstTexture, "картинка первого пресета обязана быть RenderTexture");
        Assert.IsTrue(firstTexture!.IsCreated());

        var presets = Child(tile, "Presets");
        Child(presets, "PresetDot_1").GetComponent<Button>().onClick.Invoke();
        for (int i = 0; i < 10 && thumb.texture == firstTexture; i++) yield return null;

        Assert.IsFalse(firstTexture.IsCreated(),
            "переключение пресета обязано ОСВОБОДИТЬ прежнюю RenderTexture, а не держать её "
            + "до закрытия панели — иначе каждый клик по пресету течёт по 64 КБ видеопамяти");
        Assert.AreNotSame(firstTexture, thumb.texture,
            "у плитки обязана остаться картинка НОВОГО пресета");
    }

    [Test]
    public void CollapsedIntoTheRail_DoesNotQueueThumbnailsForTheHiddenOpenGroup()
    {
        bool hadPrevValue = PlayerPrefs.HasKey("KitchenSidebarDockChoice");
        int prevValue = PlayerPrefs.GetInt("KitchenSidebarDockChoice", 0);
        var railCanvasGo = new GameObject("CanvasRail");
        var railHost = new GameObject("SidebarHostRail");
        try
        {
            SidebarDockPreference.Save(SidebarDockChoice.Rail);

            railCanvasGo.AddComponent<Canvas>();
            var rail = railHost.AddComponent<SidebarUI>();
            rail.Build(railCanvasGo.transform);

            Assert.AreEqual(0, rail.PendingThumbnailCountForTests,
                "рейка иконок сворачивает панель при построении — раскрытая группа не видна "
                + "никому, и заказывать для неё миниатюры сейчас незачем "
                + "(docs/UI-GUIDELINES.md, «лениво, размазано по кадрам»)");

            rail.SetExpandedForTests(true);

            Assert.Greater(rail.PendingThumbnailCountForTests, 0,
                "как только панель РЕАЛЬНО показана впервые, миниатюры открытой группы "
                + "обязаны встать в очередь");
        }
        finally
        {
            Object.DestroyImmediate(railHost);
            Object.DestroyImmediate(railCanvasGo);
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

    /// <summary>Клавиатурный путь к постановке элемента (docs/todo_evolution.md §2.4, пункт 5
    /// порядка работ): «/» уже раскрывал каталог и переходил в поиск — здесь стрелки, Enter,
    /// Escape и переключение варианта. Каждая проверка «что заспавнилось» сверяется с
    /// <see cref="SidebarUI.ItemToSpawnForTests"/> — тем же результатом, что и клик мышью,
    /// а не с тем, какая плитка подсветилась (этот класс дефекта уже ловили: поиск находил
    /// один вариант, а ставил другой).</summary>
    [UnityTest]
    public IEnumerator DownArrow_FromTheSearchField_MovesFocusIntoTheGrid_KeepingTheTypedFilter()
    {
        var search = Child(Panel, "SbSearch").GetComponent<TMP_InputField>();
        _sidebar.SimulateSlashShortcutForTests();
        yield return null;
        yield return null;
        Assert.IsTrue(search.isFocused,
            "поиск обязан быть в фокусе перед проверкой — иначе тест ничего не доказывает");
        search.text = "Полка";

        _sidebar.SimulateKeyForTests(KeyCode.DownArrow);

        Assert.IsTrue(_sidebar.HasKeyboardSelectionForTests,
            "стрелка вниз из поля поиска обязана перевести клавиатурный фокус в сетку плиток");
        Assert.AreEqual("Полка", search.text,
            "набранный текст фильтра не имеет права потеряться при переходе в сетку");
    }

    [Test]
    public void ArrowRight_SelectsTheNeighborTileInTheSameRow()
    {
        var tiles = TilesOf("Детали");
        Assert.GreaterOrEqual(tiles.Count, 2,
            "нужно хотя бы две плитки в «Детали», чтобы проверить соседку по строке");
        _sidebar.SelectTileForTests("Детали", tiles[0].title);

        _sidebar.SimulateKeyForTests(KeyCode.RightArrow);

        Assert.AreEqual(tiles[1].title, _sidebar.KeyboardSelectedTileTitleForTests,
            "стрелка вправо от левой плитки строки обязана выбрать правую плитку той же строки");
    }

    [Test]
    public void ArrowDown_SelectsTheNeighborTileInTheRowBelow_SameColumn()
    {
        var tiles = TilesOf("Детали");
        Assert.GreaterOrEqual(tiles.Count, 3,
            "нужно хотя бы три плитки в «Детали» (две строки сетки 2×N), чтобы проверить "
            + "соседку снизу");
        _sidebar.SelectTileForTests("Детали", tiles[0].title);

        _sidebar.SimulateKeyForTests(KeyCode.DownArrow);

        Assert.AreEqual(tiles[2].title, _sidebar.KeyboardSelectedTileTitleForTests,
            "стрелка вниз от плитки в столбце 0 обязана выбрать плитку следующей строки того "
            + "же столбца");
    }

    [Test]
    public void ArrowUp_AtTheTopOfTheFirstOpenGroup_StaysPut_DoesNotFlyIntoEmptiness()
    {
        var tiles = TilesOf("Детали");
        _sidebar.SelectTileForTests("Детали", tiles[0].title);

        _sidebar.SimulateKeyForTests(KeyCode.UpArrow);

        Assert.AreEqual(tiles[0].title, _sidebar.KeyboardSelectedTileTitleForTests,
            "выше первой строки первой открытой группы ничего нет — стрелка вверх обязана "
            + "оставить выделение на месте, а не улететь в пустоту");
    }

    [Test]
    public void ArrowLeft_AtTheLeftmostColumn_StaysPut_DoesNotFlyIntoEmptiness()
    {
        var tiles = TilesOf("Детали");
        _sidebar.SelectTileForTests("Детали", tiles[0].title);

        _sidebar.SimulateKeyForTests(KeyCode.LeftArrow);

        Assert.AreEqual(tiles[0].title, _sidebar.KeyboardSelectedTileTitleForTests,
            "левее первого столбца ничего нет — стрелка влево обязана оставить выделение на месте");
    }

    [Test]
    public void ArrowDown_FromTheLastTileOfAnOpenGroup_CrossesIntoTheNextOpenGroup_SkippingClosedOnes()
    {
        // «Детали» открыта по умолчанию (SetUp сбрасывает последнюю использованную группу);
        // раскрываем ещё «Сантехника» — между ними остаются свёрнутыми «Фасады», «Ящики»,
        // «Мебель», «Техника», и стрелка обязана пропустить их все, будто их нет.
        Header("Сантехника").onClick.Invoke();
        var detaliTiles = TilesOf("Детали");
        _sidebar.SelectTileForTests("Детали", detaliTiles[detaliTiles.Count - 1].title);

        _sidebar.SimulateKeyForTests(KeyCode.DownArrow);

        Assert.AreEqual("Сантехника", _sidebar.KeyboardSelectedGroupTitleForTests,
            "стрелка вниз с последней плитки открытой группы обязана перейти в следующую "
            + "ОТКРЫТУЮ группу, пропустив свёрнутые между ними");
    }

    [Test]
    public void Enter_InTheGrid_TargetsTheSameItemAClickWould()
    {
        var tiles = TilesOf("Детали");
        _sidebar.SelectTileForTests("Детали", tiles[0].title);
        var expected = _sidebar.ItemToSpawnForTests("Детали", tiles[0].title);

        _sidebar.SimulateKeyForTests(KeyCode.Return);

        Assert.IsNotNull(_sidebar.LastSpawnAttemptForTests,
            "Enter в сетке обязан нацелиться на постановку выбранной плитки");
        Assert.AreEqual(expected.name, _sidebar.LastSpawnAttemptForTests!.Value.name,
            "Enter в сетке обязан поставить именно ту позицию, что была бы поставлена кликом "
            + "по плитке — сенсор проверяет РЕЗУЛЬТАТ (что заспавнилось), а не то, что "
            + "подсветилось");
    }

    /// <summary>Противоположный вход к <see cref="Enter_InTheGrid_TargetsTheSameItemAClickWould"/>:
    /// то же нажатие Enter, но фокус остаётся в поле поиска — и обязано НЕ ставить ничего.</summary>
    [UnityTest]
    public IEnumerator Enter_WhileTypingInSearch_DoesNotSpawnAnything()
    {
        var search = Child(Panel, "SbSearch").GetComponent<TMP_InputField>();
        _sidebar.SimulateSlashShortcutForTests();
        yield return null;
        yield return null;
        Assert.IsTrue(search.isFocused,
            "поиск обязан быть в фокусе перед проверкой — иначе тест ничего не доказывает");
        search.text = "Полка";

        _sidebar.SimulateKeyForTests(KeyCode.Return);

        Assert.IsNull(_sidebar.LastSpawnAttemptForTests,
            "Enter, нажатый пока фокус в поле поиска, не имеет права ничего ставить — иначе "
            + "набор текста фильтра сам собой спавнил бы деталь");
    }

    /// <summary>Приёмка нашла ровно этот сценарий: плитка выбрана клавиатурой (док раскрыт
    /// постоянно на 1080p), пользователь кликнул в постороннее поле — например поле свойств
    /// элемента — и печатает там. Enter обязан уйти в это поле, а не одновременно поставить
    /// деталь: набор нажатий проверяется РЕЗУЛЬТАТОМ (что заспавнилось), а не тем, что
    /// осталось подсвеченным в сетке.</summary>
    [UnityTest]
    public IEnumerator Enter_WhileTypingInAnUnrelatedField_DoesNotSpawnAnything_EvenWithATileSelected()
    {
        var esGo = new GameObject("EventSystem", typeof(EventSystem));
        var otherField = UIFactory.CreateInputField("OtherField", _canvasGo.transform, "",
            Vector2.zero, new Vector2(120f, 24f));
        try
        {
            var tiles = TilesOf("Детали");
            _sidebar.SelectTileForTests("Детали", tiles[0].title);

            otherField.ActivateInputField();
            yield return null;
            Assert.IsTrue(otherField.isFocused,
                "постороннее поле обязано быть в фокусе перед проверкой — иначе тест ничего "
                + "не доказывает");

            otherField.text = "600";
            _sidebar.SimulateKeyForTests(KeyCode.Return);

            Assert.IsNull(_sidebar.LastSpawnAttemptForTests,
                "Enter, нажатый пока фокус в постороннем поле (не в поиске каталога), не "
                + "имеет права поставить плитку, выбранную клавиатурой раньше — иначе "
                + "подтверждение значения в чужом поле спавнило бы деталь заодно");
            Assert.IsTrue(_sidebar.HasKeyboardSelectionForTests,
                "выделение плитки не обязано сниматься от чужого Enter — оно просто не "
                + "имеет права действовать, пока печатает кто-то другой");
        }
        finally
        {
            Object.DestroyImmediate(otherField.gameObject);
            Object.DestroyImmediate(esGo);
        }
    }

    [Test]
    public void Escape_ClearsTheKeyboardSelection_BeforeAnythingElse()
    {
        var tiles = TilesOf("Детали");
        _sidebar.SelectTileForTests("Детали", tiles[0].title);

        _sidebar.SimulateKeyForTests(KeyCode.Escape);

        Assert.IsFalse(_sidebar.HasKeyboardSelectionForTests,
            "Escape обязан снять клавиатурное выделение плитки первым делом");
    }

    [Test]
    public void Escape_WithNoSelection_CollapsesAnUnpinnedExpandedDock()
    {
        _sidebar.SetExpandedForTests(true);
        Child(Panel, "SbPin").GetComponent<Button>().onClick.Invoke(); // снимаем булавку

        _sidebar.SimulateKeyForTests(KeyCode.Escape);

        Assert.IsFalse(Full.gameObject.activeSelf,
            "без клавиатурного выделения и без булавки Escape обязан свернуть раскрытый док");
    }

    [Test]
    public void Escape_WithNoSelection_LeavesAPinnedDockAlone()
    {
        _sidebar.SetExpandedForTests(true); // булавка по умолчанию включена

        _sidebar.SimulateKeyForTests(KeyCode.Escape);

        Assert.IsTrue(Full.gameObject.activeSelf,
            "закреплённый док не имеет права закрыться от Escape — иначе пользователь "
            + "потеряет открытый каталог посреди работы");
    }

    [Test]
    public void KeyboardSelection_IsMarkedByBothColorAndAnOutline_NotColorAlone()
    {
        var tiles = TilesOf("Детали");
        var tileNode = Tile("Детали", tiles[0].title);
        var background = tileNode.GetComponent<Image>();
        var outline = tileNode.GetComponent<Outline>();
        Assert.IsNotNull(outline,
            "у плитки обязана быть рамка-Outline — иначе выделение несёт только цвет "
            + "(LEAD-AGENT.md §2: цвет как единственный носитель смысла теряется примерно "
            + "у 8% мужчин)");
        Assert.IsFalse(outline!.enabled, "до выделения рамка обязана быть выключена");
        Color unselectedColor = background.color;

        _sidebar.SelectTileForTests("Детали", tiles[0].title);

        Assert.IsTrue(outline.enabled, "выбранная плитка обязана включить рамку");
        Assert.AreNotEqual(unselectedColor, background.color,
            "выбранная плитка обязана сменить и цвет фона — цвет и форма вместе, не порознь");
    }

    /// <summary>Сайдбар — единственный источник этого флага, а камера (CameraController.
    /// ApplyArrowOrbitIfOwned) только читает его: направление зависимости обязано идти от UI
    /// вниз к ядру, а не наоборот (LayerDependencyDirectionTests). Контракт проверяется здесь,
    /// на реальном выборе плитки, а не на подставном значении.</summary>
    [Test]
    public void SelectingATile_ClaimsTheArrowKeysFromTheCamera_AndReleasesThemOnDeselect()
    {
        Assert.IsFalse(CameraController.CatalogHasLiveKeyboardSelection,
            "без выбранной плитки признак, который читает камера, обязан быть снят");

        var tiles = TilesOf("Детали");
        _sidebar.SelectTileForTests("Детали", tiles[0].title);
        Assert.IsTrue(CameraController.CatalogHasLiveKeyboardSelection,
            "выбор плитки клавиатурой обязан поднять признак — иначе камера не узнает, что "
            + "сетка забрала стрелки себе");

        _sidebar.SimulateKeyForTests(KeyCode.Escape);
        Assert.IsFalse(CameraController.CatalogHasLiveKeyboardSelection,
            "снятие выделения обязано опустить признак обратно — иначе камера остаётся "
            + "заблокированной навсегда после одного выбора плитки");
    }

    [Test]
    public void RightBracket_CyclesToTheNextPreset_OfTheKeyboardSelectedTile()
    {
        const string key = "KitchenSidebarPreset_Ящик";
        bool hadPrevValue = PlayerPrefs.HasKey(key);
        string prevValue = PlayerPrefs.GetString(key, "");
        try
        {
            _sidebar.SelectTileForTests("Ящики", "Ящик");
            string? before = SidebarPresetPreference.Load("Ящик");

            _sidebar.SimulateKeyForTests(KeyCode.RightBracket);

            string? after = SidebarPresetPreference.Load("Ящик");
            Assert.AreNotEqual(before, after,
                "] обязан переключить вариант плитки, выбранной клавиатурой, — так же, как клик "
                + "по точке пресета");
        }
        finally
        {
            if (hadPrevValue) PlayerPrefs.SetString(key, prevValue);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
