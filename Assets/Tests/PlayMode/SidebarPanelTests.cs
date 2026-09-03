using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Левая палитра объектов: заголовки-аккордеоны, раскладка пунктов по
/// их фактической высоте, свёрнутый режим с буквами групп, серые пункты
/// в режиме помещения и — с тех пор как каталог перерос экран — прокрутка.
///
/// PlayMode, и это не вкусовщина: сайдбар подписывается на статическое событие
/// EditModeManager.Changed и отписывается в OnDestroy, а вне Play mode Unity
/// OnDestroy не зовёт. Тот же набор в EditMode оставлял живой обработчик с уже
/// уничтоженными кнопками, и следующий же набор, трогающий режим, падал с
/// MissingReferenceException — 20 чужих тестов подряд.</summary>
public class SidebarPanelTests
{
    private GameObject _canvasGo = null!;
    private GameObject _sidebarHost = null!;
    private SidebarUI _sidebar = null!;

    [SetUp]
    public void SetUp()
    {
        EditModeManager.Reset();
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
    }

    private Transform Panel => _canvasGo.transform.Find("Sidebar");

    private Transform Full => Panel.Find("SbFull");

    private RectTransform FullContent => (RectTransform)Full.Find("SbFullContent");

    private Transform Item(string group, string name) => FullContent.Find("SbItem_" + group + "_" + name);

    private Button Header(string group) => FullContent.Find("SbGrp_" + group).GetComponent<Button>();

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

    [Test]
    public void GroupHeader_PutsTheGlyphBeforeTheTitle_AndFlipsItOnToggle()
    {
        var header = Header("Техника");
        var label = header.GetComponentInChildren<TMP_Text>();

        Assert.AreEqual(UIStyle.GlyphExpanded + "  Техника", label.text,
            "глиф стоит СЛЕВА от названия: так видно, что строка раскрывается, "
            + "и у заголовка с пунктами общий левый край");

        header.onClick.Invoke();

        Assert.AreEqual(UIStyle.GlyphCollapsed + "  Техника", label.text);
        Assert.IsFalse(Item("Техника", "Духовка " + OvenElement.MODEL).gameObject.activeSelf,
            "свёрнутая группа прячет свои пункты");
    }

    [Test]
    public void ItemLabels_WrapByWords_AndTruncateWhatDoesNotFit()
    {
        var label = Item("детали", "Полка").GetComponentInChildren<TMP_Text>();

        Assert.IsTrue(label.enableWordWrapping,
            "длинное название переносится по словам — иначе оно уезжает за кнопку");
        Assert.AreEqual(TextOverflowModes.Truncate, label.overflowMode,
            "оценка ширины приблизительная: если TMP возьмёт строку сверх расчёта, "
            + "лишнее обязано обрезаться ВНУТРИ кнопки, а не наехать на соседний пункт");
    }

    [Test]
    public void Items_AreLaidOutByTheirActualHeight_SoATwoLineItemDoesNotOverlap()
    {
        var appliances = SidebarCatalog.Build()[4].items;
        var tall = Item("Техника", appliances[1].name).GetComponent<RectTransform>();
        var next = Item("Техника", appliances[2].name).GetComponent<RectTransform>();

        Assume.That(SidebarUI.ItemLines(appliances[1].name), Is.EqualTo(2),
            "для проверки нужен пункт, который реально занимает две строки");

        float step = tall.anchoredPosition.y - next.anchoredPosition.y;
        Assert.GreaterOrEqual(step, tall.sizeDelta.y,
            "шаг раскладки берётся по ФАКТИЧЕСКОЙ высоте пункта: с постоянным "
            + "шагом двухстрочная кнопка накрыла бы следующую");
    }

    /// <summary>Каталог выше окна сайдбара — и обязан целиком доставаться
    /// прокруткой. Пока корень был жёсткие 960 px без ScrollRect, 34 пункта
    /// в 7 группах занимали 1280 px, и до нижних восьми было не добраться
    /// ничем.</summary>
    [Test]
    public void CatalogTallerThanTheWindow_StaysReachableThroughScrolling()
    {
        var scroll = Full.GetComponent<ScrollRect>();
        var viewport = (RectTransform)Full;

        Assert.IsNotNull(scroll, "каталог не помещается в панель — без ScrollRect его не достать");
        Assert.AreSame(FullContent, scroll.content);
        Assert.IsTrue(scroll.vertical);
        Assert.AreEqual(ScrollRect.MovementType.Clamped, scroll.movementType,
            "содержимое не должно оттягиваться за края — как в дереве объектов");

        float lowest = 0f;
        foreach (var g in SidebarCatalog.Build())
            foreach (var it in g.items)
            {
                var rt = Item(g.title, it.name).GetComponent<RectTransform>();
                lowest = Mathf.Max(lowest, -rt.anchoredPosition.y + rt.sizeDelta.y);
            }

        Assume.That(lowest, Is.GreaterThan(viewport.rect.height),
            "проверка имеет смысл, только пока каталог выше окна");
        Assert.GreaterOrEqual(FullContent.sizeDelta.y, lowest,
            "высота содержимого обязана накрывать нижний край последнего пункта: "
            + "всё, что за ней, прокрутка не покажет");
    }

    [Test]
    public void Panel_TakesItsHeightFromTheScreen_AndLeavesTheStatusChipAlone()
    {
        var panel = (RectTransform)Panel;
        var canvas = (RectTransform)_canvasGo.transform;

        Assert.Greater(BottomY(panel), BottomY(canvas),
            "высота панели считается от экрана: зашитое число рано или поздно "
            + "оказывается больше экрана, и низ сайдбара уходит за его край");
        Assert.GreaterOrEqual(BottomY(panel), BottomY(canvas) + 34f,
            "внизу слева живёт плашка статуса высотой 26 px с отступом 8 — "
            + "панель обязана заканчиваться над ней");
    }

    [Test]
    public void Collapsed_ShowsShortLabelsAndHidesThePin()
    {
        var pin = Panel.Find("SbPin");
        Assume.That(pin.gameObject.activeSelf, Is.True);

        Panel.Find("SbCollapse").GetComponent<Button>().onClick.Invoke();

        Assert.IsFalse(Full.gameObject.activeSelf);
        var mini = Panel.Find("SbMini");
        Assert.IsTrue(mini.gameObject.activeSelf);
        Assert.AreEqual("Т", mini.Find("SbMiniContent/SbMini_Техника").GetComponentInChildren<TMP_Text>().text,
            "в узкой полосе группа подписана своей буквой из каталога");
        Assert.IsFalse(pin.gameObject.activeSelf,
            "булавка видна только в развёрнутом сайдбаре: в полосе шириной 52 px "
            + "её некуда поставить, а закреплять свёрнутую панель незачем");
    }

    /// <summary>Свёрнутая полоса сегодня короче экрана, но растёт на 42 px
    /// с каждой новой группой — ровно так же, как раскрытый каталог дорос до
    /// недостижимого низа. Полосы прокрутки в 52 px не поставить, поэтому
    /// у неё колесо и объявленная высота содержимого, а не молчаливая обрезка.</summary>
    [Test]
    public void CollapsedStrip_AlsoScrolls_SoNewGroupsCannotHideBelowTheEdge()
    {
        var mini = Panel.Find("SbMini");
        var scroll = mini.GetComponent<ScrollRect>();
        var content = (RectTransform)mini.Find("SbMiniContent");

        Assert.IsNotNull(scroll);
        Assert.AreSame(content, scroll.content);
        Assert.AreEqual(SidebarLayout.MiniContentHeight(SidebarCatalog.Build().Count),
            content.sizeDelta.y,
            "высота полосы считается по числу групп, а не задаётся числом");
    }

    [Test]
    public void RoomMode_GraysOutRegularItems_ButKeepsWallsAndAlwaysItems()
    {
        var shelf = Item("детали", "Полка").GetComponent<Button>();
        var wall = Item("Помещение", "Стена").GetComponent<Button>();
        var korob = Item("Помещение", EditModeManager.KorobName).GetComponent<Button>();
        var shelfLabel = shelf.GetComponentInChildren<TMP_Text>();
        Color normalColor = shelfLabel.color;

        EditModeManager.SetMode(EditMode.Room);

        Assert.IsFalse(shelf.interactable,
            "в режиме помещения обычные детали не добавляются — кнопка не только "
            + "серая, но и некликабельная");
        AssertSameColor(UIStyle.TextSecondary, shelfLabel.color,
            "недоступный пункт обязан выглядеть недоступным, а не просто молчать в ответ");
        Assert.IsTrue(wall.interactable);
        Assert.IsTrue(korob.interactable, "короб доступен в любом режиме");

        EditModeManager.Reset();

        Assert.IsTrue(shelf.interactable);
        AssertSameColor(normalColor, shelfLabel.color,
            "возврат в обычный режим возвращает и цвет: подписка на "
            + "EditModeManager.Changed работает в обе стороны");
    }
}
