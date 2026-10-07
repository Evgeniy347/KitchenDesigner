using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Сторож ЦЕНЫ ОПРОСА окна «Сцена». Считает работу, а не миллисекунды:
/// сколько раз за N опросов покоя панель скопировала реестр
/// (<c>PartRegistry.GetAll()</c> — новый <c>List</c> на весь проект, на сцене в 411
/// деталей это сотни килобайт мусора) и сколько раз перестроила дерево.
///
/// <para>Болезнь, ради которой написан: проверка «надо ли обновляться» стоила столько
/// же, сколько само обновление. <c>ComputeFingerprint</c> копировал реестр, а
/// <c>Refresh</c> копировал его ещё дважды — считая отпечаток заново поверх уже
/// посчитанного. Одна загрузка проекта в ОДНУ деталь стоила четырёх копий реестра.</para>
///
/// <para>Почему отпечаток остался, а не заменён сравнением <c>SceneRevision</c>:
/// переименование детали не поднимает ревизию вообще. <c>DrawerLinks.Rename</c> не
/// трогает ни трансформ, ни состав реестра, а именно через него переименовывают и
/// панель свойств, и MCP. Калитка на одной ревизии оставила бы дерево со старым именем
/// до следующего чужого изменения — молчаливая ложь, которая хуже лишнего обхода.
/// Поэтому имена читаются каждый опрос, но БЕЗ копии реестра.</para>
///
/// <para>Вторая болезнь, найденная тем же прибором и тремя дампами пользователя: КАЖДЫЙ
/// клик по детали стоил 72,4–77,6 мс, и все они уходили в этой панели —
/// <c>OnSceneSelectionChanged</c> сносил и строил заново ВСЕ строки дерева ради того,
/// чтобы перекрасить одну. Прочие слушатели выделения стоили нули, а цена панели не
/// зависела от числа выделенных рендереров (1, 8, 9, 11) — она платила за весь список.
/// Отсюда второй счётчик: число ПОСТРОЕННЫХ СТРОК.</para>
///
/// <para>Обратные входы ниже — по одному на способ изменить состав дерева: добавили,
/// удалили, переименовали, перенесли в группу, переименовали группу, прикрепили фасад,
/// выделили деталь в свёрнутой группе. Без них «ноль перестроений» читалось бы как
/// «панель мертва».</para></summary>
public class HierarchyPanelPollWorkTests
{
    private const int QuietPolls = 20;

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

        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();

        _selectionBefore = SelectionManager.Instance;
        _selectionGo = new GameObject("Selection");
        _selection = _selectionGo.AddComponent<SelectionManager>();
        SelectionManager.Instance = _selection;

        _panelHost = new GameObject("HierarchyPanelHost");
        _panel = _panelHost.AddComponent<HierarchyPanelUI>();
        _panel.Build(_canvasGo.transform);
        HierarchyPanelUI.TakeRebuilds();
        HierarchyPanelUI.TakeRowsBuilt();
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
        HierarchyPanelUI.TakeRebuilds();
        HierarchyPanelUI.TakeRowsBuilt();
    }

    /// <summary>Куб, а не пустой <c>GameObject</c>: выделение красит РЕНДЕРЕР, и деталь
    /// без него проверяла бы путь, которого у пользователя нет.</summary>
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

    private DrawerElement MakeDrawer(string name)
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400,
            name, Vector3.zero);
        _spawned.Add(go);
        var drawer = go.GetComponent<DrawerElement>();
        PartRegistry.Register(drawer);
        return drawer;
    }

    private FacadeElement MakeFacade(string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var facade = go.AddComponent<FacadeElement>();
        facade.PartName = name;
        facade.DimensionsMM = new Vector3Int(400, 300, 18);
        PartRegistry.Register(facade);
        _spawned.Add(go);
        return facade;
    }

    private Transform Panel => _canvasGo.transform.Find("HierarchyPanel");

    private Transform Content => Panel.Find("HierarchyPanelBody/HierarchyPanelBodyContent");

    private static string LabelOf(Transform row)
    {
        var text = row.Find("Main").GetComponentInChildren<TMP_Text>();
        return text != null ? text.text : "";
    }

    private List<string> RowLabels()
    {
        var labels = new List<string>();
        foreach (Transform row in Content) labels.Add(LabelOf(row));
        return labels;
    }

    /// <summary>Строка группы подписана «Имя (N)», строка детали — одним именем.</summary>
    private bool HasRowNamed(string name)
    {
        foreach (var label in RowLabels())
            if (label == name || label.StartsWith(name + " (", System.StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>Панель показана и знает текущую сцену: дальше любое перестроение —
    /// заслуга изменения, а не первого показа.</summary>
    private void ShowAndSettle()
    {
        _panel.SetVisible(true);
        _panel.PollSceneForChanges();
        HierarchyPanelUI.TakeRebuilds();
        HierarchyPanelUI.TakeRowsBuilt();
    }

    private Color TintOf(string label)
    {
        foreach (Transform row in Content)
        {
            if (LabelOf(row) != label) continue;
            var image = row.GetComponent<Image>();
            if (image != null) return image.color;
        }
        return Color.clear;
    }

    private bool IsPainted(string label) => TintOf(label) == UIStyle.RowSelected;

    private BasePlate MakePlate()
    {
        var plate = BasePlate.Create();
        PartRegistry.Register(plate.Element);
        _spawned.Add(plate.gameObject);
        return plate;
    }

    [Test]
    public void BasePlate_HasNoRowInTheHierarchy()
    {
        MakeElement("Полка");
        MakePlate();

        ShowAndSettle();

        Assert.IsTrue(HasRowNamed("Полка"), "посылка: обычная деталь в дереве есть");
        Assert.IsFalse(HasRowNamed("BasePlate"),
            "плита - служебный якорь сцены, а не предмет проекта: строки у неё быть не должно. Строки: "
            + string.Join(" | ", RowLabels()));
    }

    [Test]
    public void SceneWithOnlyTheBasePlate_ShowsTheEmptyState()
    {
        MakePlate();

        ShowAndSettle();

        Assert.IsTrue(Panel.Find("HierEmpty").gameObject.activeSelf,
            "в проекте одна плита - это пустой проект, и панель обязана сказать об этом, а не показывать служебную строку");
        Assert.AreEqual(0, RowLabels().Count);
    }

    /// <summary>Главный сенсор: сцена стоит на месте — опрос обязан быть даровым.</summary>
    [Test]
    public void QuietScene_ManyPolls_CopyTheRegistryNotOnce_AndRebuildNothing()
    {
        var shelf = MakeElement("Полка");
        MakeElement("Боковина");
        ShowAndSettle();

        long scansBefore = SceneScanCounter.Scans;
        for (int i = 0; i < QuietPolls; i++) _panel.PollSceneForChanges();
        int rebuilds = HierarchyPanelUI.TakeRebuilds();
        long scans = SceneScanCounter.Scans - scansBefore;

        TestContext.WriteLine($"{QuietPolls} опросов покоя: копий реестра {scans}, "
            + $"перестроений {rebuilds}");

        Assert.AreEqual(0L, scans,
            $"{QuietPolls} опросов покоя обязаны стоить НОЛЬ копий реестра: проверка «надо "
            + "ли обновляться» не может стоить столько же, сколько само обновление. "
            + "Обходившие: " + SceneScanCounter.Since(scansBefore));
        Assert.AreEqual(0, rebuilds,
            "и ни одного перестроения дерева: в сцене ничего не менялось");

        string nameBefore = shelf.PartName;
        DrawerLinks.Rename(shelf, "Stoleshnitsa");
        Assume.That(shelf.PartName, Is.Not.EqualTo(nameBefore),
            "положительный контроль контроля: имя обязано было смениться, иначе панели "
            + "нечего было бы замечать");
        _panel.PollSceneForChanges();

        Assert.AreEqual(1, HierarchyPanelUI.TakeRebuilds(),
            "положительный контроль к нулям выше: панель ЖИВА и следующий же опрос после "
            + "настоящего изменения перестраивает дерево. Без этой проверки «ноль работы» "
            + "нельзя отличить от мёртвой панели");
    }

    /// <summary>Одно изменение — одно перестроение, а не одно на каждый последующий
    /// опрос: иначе панель отдала бы весь выигрыш обратно, просто позже.</summary>
    [Test]
    public void OneChange_CostsOneRebuild_AndTheNextPollsAreFreeAgain()
    {
        MakeElement("Полка");
        ShowAndSettle();

        MakeElement("Боковина");
        for (int i = 0; i < QuietPolls; i++) _panel.PollSceneForChanges();

        int rebuilds = HierarchyPanelUI.TakeRebuilds();
        TestContext.WriteLine($"одно изменение и {QuietPolls} опросов: перестроений {rebuilds}");

        Assert.AreEqual(1, rebuilds,
            $"добавление одной детали и {QuietPolls} опросов после него обязаны стоить "
            + "РОВНО одно перестроение: число, равное числу опросов, и есть возвращённая "
            + "болезнь — панель перестраивается «на всякий случай»");
    }

    [Test]
    public void ElementAdded_TheTreeShowsIt()
    {
        MakeElement("Полка");
        ShowAndSettle();

        MakeElement("Боковина");
        _panel.PollSceneForChanges();

        Assert.Contains("Боковина", RowLabels(),
            "добавленная деталь обязана появиться в дереве: состав сцены — это ровно то, "
            + "что окно «Сцена» обещает показывать");
    }

    [Test]
    public void ElementRemoved_TheTreeDropsIt()
    {
        var shelf = MakeElement("Полка");
        MakeElement("Боковина");
        ShowAndSettle();

        PartRegistry.Unregister(shelf);
        _panel.PollSceneForChanges();

        Assert.That(RowLabels(), Has.No.Member("Полка"),
            "строка, пережившая свой элемент, кликается и выделяет мертвеца");
        Assert.Contains("Боковина", RowLabels(), "и это перестроение, а не очистка");
    }

    /// <summary>Тот самый вход, который калитка на одной <c>SceneRevision</c> проспала
    /// бы: переименование не поднимает ревизию ничем.</summary>
    [Test]
    public void ElementRenamed_TheTreeShowsTheNewName()
    {
        var shelf = MakeElement("Полка");
        ShowAndSettle();
        int revisionBefore = SceneRevision.Version;

        DrawerLinks.Rename(shelf, "Stoleshnitsa");
        string renamed = shelf.PartName;
        Assume.That(renamed, Is.Not.EqualTo("Полка"),
            "предпосылка: имя в самой детали действительно сменилось. Сверяемся с ним, а "
            + "не с тем, что просили: ElementNaming.Sanitize транслитерирует кириллицу, и "
            + "ожидание «покажется ровно то, что ввели» проверяло бы не панель, а нормализацию");
        _panel.PollSceneForChanges();

        Assert.AreEqual(revisionBefore, SceneRevision.Version,
            "предпосылка этого входа: переименование НЕ двигает ревизию сцены. Станет "
            + "двигать — калитку на ревизии можно будет поставить, и этот тест первым "
            + "об этом скажет");
        Assert.Contains(renamed, RowLabels(),
            "переименованная деталь обязана показаться под новым именем");
        Assert.That(RowLabels(), Has.No.Member("Полка"), "и не остаться под старым");
    }

    [Test]
    public void ElementMovedToAGroup_TheTreeShowsItUnderTheGroup()
    {
        var shelf = MakeElement("Полка");
        ShowAndSettle();

        var group = GroupManager.Create("Шкаф");
        GroupManager.MoveTo(shelf, group);
        _panel.PollSceneForChanges();

        Assert.IsTrue(HasRowNamed("Шкаф"),
            "деталь перенесли в группу — группа обязана появиться в дереве");
        Assert.Contains("Полка", RowLabels(), "а сама деталь — остаться видимой под ней");
    }

    [Test]
    public void GroupRenamed_TheTreeShowsTheNewName()
    {
        var shelf = MakeElement("Полка");
        var group = GroupManager.Create("Шкаф");
        GroupManager.MoveTo(shelf, group);
        ShowAndSettle();

        GroupManager.Rename(group, "Пенал");
        _panel.PollSceneForChanges();

        Assert.IsTrue(HasRowNamed("Пенал"), "группа обязана показаться под новым именем");
        Assert.IsFalse(HasRowNamed("Шкаф"), "и не остаться под старым");
    }

    /// <summary>Главный сенсор ВТОРОЙ болезни, найденной прибором: клик по детали стоил
    /// 72,4–77,6 мс (три дампа, 15 кадров) — это <c>OnSceneSelectionChanged</c> сносил и
    /// строил заново ВСЕ строки дерева ради того, чтобы перекрасить одну. Цена не зависела
    /// от числа выделенных рендереров (1, 8, 9, 11) — панель платила за весь список.
    /// Выделение не меняет состав дерева, поэтому строк обязано строиться НОЛЬ.</summary>
    [Test]
    public void SelectionChanged_RepaintsTheRows_WithoutBuildingASingleOne()
    {
        var shelf = MakeElement("Полка");
        var side = MakeElement("Боковина");
        _selection.Select(shelf);
        ShowAndSettle();
        Assume.That(IsPainted("Полка"), Is.True, "предпосылка: выделенная строка покрашена");

        _selection.Select(side);

        int rows = HierarchyPanelUI.TakeRowsBuilt();
        int rebuilds = HierarchyPanelUI.TakeRebuilds();
        TestContext.WriteLine($"смена выделения: строк построено {rows}, перестроений {rebuilds}");

        Assert.AreEqual(0, rows,
            "смена выделения не меняет состав дерева: строить заново нечего. Число, равное "
            + "числу строк списка, и есть те самые 75 мс на каждый клик — снос и постройка "
            + "всех GameObject строки ради одной перекраски");
        Assert.AreEqual(0, rebuilds, "и ни одного перестроения");

        Assert.IsTrue(IsPainted("Боковина"),
            "но перекраска обязана состояться: выделение переехало на другую строку");
        Assert.IsFalse(IsPainted("Полка"),
            "а со старой — сойти, иначе в дереве окажутся два выделения");
    }

    /// <summary>Обратный вход к нулям выше: если вместе с выделением изменился и состав,
    /// дерево обязано перестроиться В ТОТ ЖЕ КАДР, а не ждать опроса. Так и создаётся
    /// новая деталь — её регистрируют и тут же выделяют.</summary>
    [Test]
    public void SelectionChanged_AfterTheSceneGrew_RebuildsRightAway()
    {
        MakeElement("Полка");
        ShowAndSettle();

        var fresh = MakeElement("Боковина");
        _selection.Select(fresh);

        Assert.Greater(HierarchyPanelUI.TakeRowsBuilt(), 0,
            "новая деталь появилась и сразу выделена — перекраской тут не обойтись");
        Assert.Contains("Боковина", RowLabels(),
            "и показать её обязано немедленно: ждать полсекунды до следующего опроса — "
            + "значит полсекунды показывать неправду");
        Assert.IsTrue(IsPainted("Боковина"), "и сразу выделенной");
    }

    /// <summary>Второй обратный вход: выделение детали внутри свёрнутой группы
    /// РАСКРЫВАЕТ её, то есть меняет видимый состав — тут перестроение обязательно.</summary>
    [Test]
    public void SelectionInsideACollapsedGroup_ExpandsIt_AndRebuilds()
    {
        var shelf = MakeElement("Полка");
        var group = GroupManager.Create("Шкаф");
        GroupManager.MoveTo(shelf, group);
        _panel.SetVisible(true);
        _panel.ToggleCollapse(group.id);
        Assume.That(RowLabels(), Has.No.Member("Полка"),
            "предпосылка: группа свёрнута и строки детали в дереве нет");
        HierarchyPanelUI.TakeRebuilds();
        HierarchyPanelUI.TakeRowsBuilt();

        _selection.Select(shelf);

        Assert.Greater(HierarchyPanelUI.TakeRowsBuilt(), 0,
            "раскрытие группы меняет видимый состав дерева — одной перекраской не обойтись");
        Assert.Contains("Полка", RowLabels(),
            "выделенная деталь обязана стать видимой: выделение, спрятанное в свёрнутой "
            + "группе, пользователю не показывает ничего");
    }

    /// <summary>Скрытая панель не платит вовсе — а показанная показывает правду: до
    /// починки спрятанное окно «Сцена» тратило те же 75 мс на каждый клик.</summary>
    [Test]
    public void SelectionChanged_WhileHidden_CostsNothing_AndTheTreeIsTrueWhenShownAgain()
    {
        var shelf = MakeElement("Полка");
        var side = MakeElement("Боковина");
        ShowAndSettle();
        _panel.SetVisible(false);
        HierarchyPanelUI.TakeRebuilds();
        HierarchyPanelUI.TakeRowsBuilt();

        _selection.Select(shelf);
        _selection.Select(side);

        Assert.AreEqual(0, HierarchyPanelUI.TakeRowsBuilt(),
            "скрытое окно не показывает ничего, значит и строить ему нечего");

        _panel.SetVisible(true);

        Assert.IsTrue(IsPainted("Боковина"),
            "а по возвращении обязано показать ТЕКУЩЕЕ выделение, а не то, на котором его "
            + "спрятали: экономия, пережившая показ, превращается в ложь");
    }

    /// <summary>Третье слагаемое отпечатка — прикреплённый фасад: он уезжает из
    /// верхнего уровня под своего хозяина, то есть состав дерева меняется, хотя состав
    /// реестра нет.</summary>
    /// <summary>M6 (обзор ui-mcp): отпечаток хэшировал только <c>GroupId</c>, не
    /// <c>LevelId</c> — MCP <c>edit_elements level_id</c> переносит деталь на другой
    /// этаж, а дерево (сгруппированное по этажам, <c>BuildNodesGroupedByLevel</c>)
    /// продолжает показывать её на старом, пока что-то ещё не тронет состав сцены.</summary>
    [Test]
    public void ElementMovedToAnotherLevel_TheTreeRebuilds()
    {
        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 3000),
        });
        var shelf = MakeElement("Полка");
        shelf.LevelId = "1";
        ShowAndSettle();

        shelf.LevelId = "2";
        _panel.PollSceneForChanges();

        Assert.AreEqual(1, HierarchyPanelUI.TakeRebuilds(),
            "перенос детали на другой этаж меняет ГРУППИРОВКУ дерева, хотя состав "
            + "реестра и её GroupId остались прежними — отпечаток обязан это заметить");
    }

    /// <summary>Второй вход того же отпечатка: переименование уровня не меняло
    /// хэш вовсе (хэшировались только id и отметка), так что корневой ярлык
    /// («N этаж») застревал со старым именем до постороннего перестроения.</summary>
    [Test]
    public void LevelRenamed_TheTreeShowsTheNewRootLabel()
    {
        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 3000),
        });
        MakeElement("Полка").LevelId = "1";
        MakeElement("Боковина").LevelId = "2";
        ShowAndSettle();
        Assume.That(HasRowNamed("2 этаж"), Is.True, "предпосылка: корневой ярлык второго этажа виден");

        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "Мансарда", 3000, 3000),
        });
        _panel.PollSceneForChanges();

        Assert.IsTrue(HasRowNamed("Мансарда"), "переименованный этаж обязан показаться под новым именем");
        Assert.IsFalse(HasRowNamed("2 этаж"), "и не остаться под старым");
    }

    [Test]
    public void FacadeAttachedToAHost_TheTreeRebuilds()
    {
        var drawer = MakeDrawer("Ящик");
        MakeFacade("Фасад");
        ShowAndSettle();

        drawer.AttachedFacadeName = "Фасад";
        _panel.PollSceneForChanges();

        Assert.AreEqual(1, HierarchyPanelUI.TakeRebuilds(),
            "прикрепление фасада меняет ВЛОЖЕННОСТЬ дерева, не меняя состава реестра: "
            + "проспав его, панель показывала бы фасад на верхнем уровне, хотя он уже "
            + "ребёнок ящика");
    }
}
