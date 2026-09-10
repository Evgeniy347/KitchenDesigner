using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Единый порядок владения Escape (EscapeOwnership, agents/TEST-DESIGN.md): ровно
/// один хозяин на нажатие. Каждый тест здесь встречным входом включает хозяина ВЫШЕ по
/// списку и проверяет РЕЗУЛЬТАТ — что владелец рангом ниже не срабатывает, хотя его условие
/// (открытая панель, взведённая кнопка, выбранная плитка) остаётся истинным всё это время.
///
/// Перетаскивание (ElementMover) и режимы измерения/пипетки/пикера света намеренно не
/// смешиваются здесь друг с другом: `EscapeOwnershipTests` (Pure) уже доказывает их взаимный
/// порядок исчерпывающе через чистую функцию, а трогать `ElementMover.IsDragging` можно только
/// реальным перетаскиванием мышью — заводить его здесь ради дублирования того же вывода не
/// стоит своей цены.</summary>
public class EscapeOwnershipWiringTests
{
    private GameObject _canvasGo = null!;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        UIFactory.EnsureEventSystem();
        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();
    }

    [TearDown]
    public void TearDown()
    {
        ConfirmDeleteButton.DisarmAll();
        CommandStack.Clear();
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        PartRegistry.Clear();
    }

    private KitchenElement Board(string name) =>
        Spawn(ElementFactory.CreatePart(new Vector3Int(600, 300, 18), name, Vector3.zero));

    private KitchenElement Spawn(GameObject go)
    {
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private ContextMenuUI BuildContextMenu()
    {
        var go = new GameObject("CtxMenu");
        _spawned.Add(go);
        var menu = go.AddComponent<ContextMenuUI>();
        menu.Build(_canvasGo.transform);
        return menu;
    }

    private GroupMenuUI BuildGroupMenu()
    {
        var go = new GameObject("GroupMenu");
        _spawned.Add(go);
        var menu = go.AddComponent<GroupMenuUI>();
        menu.Build(_canvasGo.transform);
        return menu;
    }

    private SelectionManager BuildSelection()
    {
        var go = new GameObject("Selection");
        _spawned.Add(go);
        return go.AddComponent<SelectionManager>();
    }

    [Test]
    public void ConfirmDelete_WinsOverContextMenu_MenuStaysOpen_UntilConfirmIsResolved()
    {
        var menu = BuildContextMenu();
        var board = Board("B1");
        menu.Open(board);
        Assert.IsTrue(menu.IsOpen, "меню обязано открыться перед проверкой — иначе тест ничего не доказывает");

        var deleteButton = _canvasGo.transform.Find("ContextMenu/CtxDel")!.GetComponent<Button>();
        deleteButton.onClick.Invoke();
        Assert.IsTrue(ConfirmDeleteButton.AnyArmed,
            "первый клик по «Удалить» обязан взвести кнопку подтверждения — иначе сценарий не воспроизведён");

        menu.TryCloseFromEscape();

        Assert.IsTrue(menu.IsOpen,
            "пока кнопка удаления взведена, Escape обязан разоружить её первой, а не закрыть " +
            "всё меню — иначе пользователь случайно закрывает окно свойств вместо отмены удаления");
        Assert.IsTrue(ConfirmDeleteButton.OwnsEscape(),
            "владение Escape в этот момент обязано принадлежать взведённой кнопке");

        ConfirmDeleteButton.DisarmAll();
        menu.TryCloseFromEscape();
        Assert.IsFalse(menu.IsOpen,
            "как только кнопка больше не взведена, следующий Escape обязан закрыть само меню");
    }

    [Test]
    public void ContextMenu_WinsOverGroupMenu_GroupMenuStaysOpen()
    {
        var ctxMenu = BuildContextMenu();
        var groupMenu = BuildGroupMenu();
        var sel = BuildSelection();

        var a = Board("A");
        var b = Board("B");
        sel.SelectOnly(new List<KitchenElement> { a, b });
        groupMenu.Open(a);
        Assert.IsTrue(groupMenu.IsOpen, "меню группы обязано открыться перед проверкой");

        ctxMenu.Open(a);
        Assert.IsTrue(ctxMenu.IsOpen, "контекстное меню обязано открыться перед проверкой");

        groupMenu.TryCloseFromEscape();
        Assert.IsTrue(groupMenu.IsOpen,
            "пока открыто контекстное меню, Escape не имеет права закрыть меню группы под ним");

        ctxMenu.TryCloseFromEscape();
        Assert.IsFalse(ctxMenu.IsOpen, "контекстное меню — его собственный Escape закрывает");

        groupMenu.TryCloseFromEscape();
        Assert.IsFalse(groupMenu.IsOpen,
            "как только контекстное меню закрыто, следующий Escape обязан закрыть меню группы");
    }

    [Test]
    public void CatalogTileSelection_WinsOverDayNightPanel_PanelStaysOpen()
    {
        EditModeManager.Reset();
        SidebarUI.ResetLastUsedGroupForTests();

        var sidebarGo = new GameObject("Sidebar");
        _spawned.Add(sidebarGo);
        var sidebar = sidebarGo.AddComponent<SidebarUI>();
        sidebar.Build(_canvasGo.transform);

        var dayNightGo = new GameObject("DayNight");
        _spawned.Add(dayNightGo);
        var dayNight = dayNightGo.AddComponent<DayNightPanelUI>();
        dayNight.Build(_canvasGo.transform);
        dayNight.SetVisible(true);

        try
        {
            var tiles = SidebarTileBuilder.BuildTiles(
                SidebarCatalog.Build().Find(g => g.title == "Детали").items);
            sidebar.SelectTileForTests("Детали", tiles[0].title);
            Assert.IsTrue(SidebarUI.CatalogClaimsEscape,
                "выбранная плитка обязана поднять общий признак притязания на Escape");

            dayNight.TryHideFromEscape();
            Assert.IsTrue(dayNight.IsVisible,
                "пока в каталоге выделена плитка, Escape не имеет права закрыть фоновую панель под ней");

            sidebar.SimulateKeyForTests(KeyCode.Escape);
            Assert.IsFalse(SidebarUI.CatalogClaimsEscape, "первый Escape обязан снять выделение плитки");

            dayNight.TryHideFromEscape();
            Assert.IsFalse(dayNight.IsVisible,
                "как только каталог больше ничего не выделяет, следующий Escape обязан закрыть панель");
        }
        finally
        {
            EditModeManager.Reset();
            SidebarUI.ResetLastUsedGroupForTests();
            CameraController.CatalogHasLiveKeyboardSelection = false;
        }
    }

    [Test]
    public void DayNightPanel_WinsOverMusicPanel_MusicStaysOpen()
    {
        var dayNightGo = new GameObject("DayNight");
        _spawned.Add(dayNightGo);
        var dayNight = dayNightGo.AddComponent<DayNightPanelUI>();
        dayNight.Build(_canvasGo.transform);
        dayNight.SetVisible(true);

        var musicGo = new GameObject("Music");
        _spawned.Add(musicGo);
        var music = musicGo.AddComponent<MusicPanelUI>();
        music.Build(_canvasGo.transform);
        music.SetVisible(true);

        music.TryHideFromEscape();
        Assert.IsTrue(music.IsVisible,
            "пока открыта панель дня/ночи, Escape не имеет права закрыть музыкальную панель под ней");

        dayNight.TryHideFromEscape();
        Assert.IsFalse(dayNight.IsVisible, "панель дня/ночи — её собственный Escape закрывает");

        music.TryHideFromEscape();
        Assert.IsFalse(music.IsVisible,
            "как только панель дня/ночи закрыта, следующий Escape обязан закрыть музыкальную панель");
    }
}
