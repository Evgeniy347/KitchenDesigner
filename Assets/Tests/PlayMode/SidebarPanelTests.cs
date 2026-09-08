using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Левая палитра объектов: заголовки-аккордеоны, раскладка пунктов по
/// их фактической высоте, свёрнутый режим с иконками групп (docs/todo_evolution.md,
/// дефект D5 — буквы «Д Ф Я М Т С П» не читались и запрещены §4 UI-GUIDELINES),
/// серые пункты в режиме помещения и — с тех пор как каталог перерос экран — прокрутка.
///
/// PlayMode, и это не вкусовщина: сайдбар подписывается на статическое событие
/// EditModeManager.Changed и отписывается в OnDestroy, а вне Play mode Unity
/// OnDestroy не зовёт. Тот же набор в EditMode оставлял живой обработчик с уже
/// уничтоженными кнопками, и следующий же набор, трогающий режим, падал с
/// MissingReferenceException — 20 чужих тестов подряд.
///
/// Узлы здесь ищет <see cref="Child"/>, а НЕ Transform.Find, и это не стиль.
/// Find трактует «/» как разделитель пути, а в каталоге есть пункт «ДВП/ХДФ»:
/// объект называется SbItem_детали_ДВП/ХДФ, Find уходит искать ребёнка
/// «SbItem_детали_ДВП» с ребёнком «ХДФ», не находит и возвращает null. Пока
/// тесты дёргали пункты поимённо, слэш никому не попадался; первый же обход
/// всего каталога упал NullReferenceException без единого слова о причине.
/// Child перебирает детей по точному имени и падает текстом, называющим
/// недостающий узел.</summary>
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

    private static string ItemNode(string group, string name) => "SbItem_" + group + "_" + name;

    private Transform Item(string group, string name) => Child(FullContent, ItemNode(group, name));

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

    private static float RowBottom(RectTransform rt)
        => -rt.anchoredPosition.y + rt.sizeDelta.y;

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

    /// <summary>D7/D8: вторая строка кнопки отдана габаритам, а не переносу
    /// длинного имени — имя теперь ровно одна строка и обрезается многоточием,
    /// когда не влезает, вместо того чтобы то одно-, то двухстрочно раздувать
    /// ритм списка.</summary>
    [Test]
    public void ItemNameLabel_NeverWraps_ItTruncatesWhatDoesNotFit()
    {
        var shortName = Item("Детали", "Полка").GetComponentInChildren<TMP_Text>();
        var longName = Item("Техника", "Варочная " + CooktopElement.MODEL_BOSCH_PUE611BB5E)
            .GetComponentInChildren<TMP_Text>();

        Assert.IsFalse(shortName.enableWordWrapping,
            "имя не переносится по словам — вторая строка занята габаритами");
        Assert.AreEqual(TextOverflowModes.Truncate, shortName.overflowMode);
        Assert.IsFalse(longName.enableWordWrapping,
            "длинное имя (с моделью прибора) ведёт себя так же: обрезается, а не переносится");
        Assert.AreEqual(TextOverflowModes.Truncate, longName.overflowMode);
    }

    /// <summary>D8: габариты каталога (Ш×В×Г, мм) видны под именем пункта без
    /// спауна — раньше их можно было узнать только заспавнив элемент.</summary>
    [Test]
    public void ItemButtons_ShowDimensionsInMillimetres_BelowTheName()
    {
        var shelf = Item("Детали", "Полка");
        var shelfDims = Child(shelf, "SbItemDims_Детали_Полка").GetComponent<TMP_Text>();
        Assert.AreEqual(SidebarUI.FormatDims(new Vector3Int(600, 400, 16)), shelfDims.text);

        var table = Item("Мебель", "Прямоугольный стол");
        var tableDims = Child(table, "SbItemDims_Мебель_Прямоугольный стол").GetComponent<TMP_Text>();
        Assert.AreEqual(SidebarUI.FormatDims(new Vector3Int(2000, 750, 1000)), tableDims.text);
    }

    /// <summary>D7: модель прибора («Bosch PUE611BB5E») больше не печатается в
    /// списке — она делает кнопку двухстрочной без всякой пользы (модель нужна
    /// в паспорте элемента, не в перечне). Подпись кнопки — короткий тип
    /// прибора; полное имя с моделью остаётся в tooltip.</summary>
    [Test]
    public void ApplianceButtons_ShowShortName_NotTheModel()
    {
        var cooktop = Item("Техника", "Варочная " + CooktopElement.MODEL_BOSCH_PUE611BB5E)
            .GetComponentInChildren<TMP_Text>();

        Assert.AreEqual("Варочная", cooktop.text,
            "в списке — короткий тип прибора без модели");
        StringAssert.DoesNotContain(CooktopElement.MODEL_BOSCH_PUE611BB5E, cooktop.text);
    }

    [Test]
    public void Items_AreLaidOutByTheirActualHeight_SoOneItemNeverOverlapsTheNext()
    {
        foreach (var g in SidebarCatalog.Build())
            for (int i = 0; i + 1 < g.items.Count; i++)
            {
                var current = Item(g.title, g.items[i].name).GetComponent<RectTransform>();
                var next = Item(g.title, g.items[i + 1].name).GetComponent<RectTransform>();

                float step = current.anchoredPosition.y - next.anchoredPosition.y;
                Assert.GreaterOrEqual(step, current.sizeDelta.y,
                    $"«{g.items[i].name}» в группе «{g.title}» накрывает следующий пункт "
                    + $"«{g.items[i + 1].name}»: шаг раскладки обязан браться по фактической "
                    + "высоте пункта");
            }
    }

    /// <summary>Каждый пункт каталога обязан доехать до содержимого панели.
    /// Проверка идёт по НАБОРУ имён детей, а не поиском по одному: имя
    /// «ДВП/ХДФ» ищется через Transform.Find как путь и не находится вовсе.</summary>
    [Test]
    public void EveryCatalogItem_HasItsButtonInTheContent()
    {
        var present = new HashSet<string>();
        for (int i = 0; i < FullContent.childCount; i++)
            present.Add(FullContent.GetChild(i).name);

        foreach (var g in SidebarCatalog.Build())
            foreach (var it in g.items)
                Assert.IsTrue(present.Contains(ItemNode(g.title, it.name)),
                    $"пункта «{it.name}» из группы «{g.title}» нет среди кнопок каталога");
    }

    /// <summary>Сторож самого способа искать узлы. Проверки ниже опираются на
    /// то, что отсутствующий узел даёт ИМЕНОВАННОЕ падение: пока поиск шёл
    /// через Transform.Find, пропажа приходила как NullReferenceException без
    /// строчки о том, чего не хватает, и на разбор такого падения уходил целый
    /// прогон PlayMode. Если этот тест позеленеет неправильно — то есть Child
    /// снова начнёт возвращать null вместо падения, — вся диагностика соседних
    /// проверок молча вернётся к NRE.</summary>
    [Test]
    public void MissingNode_IsReportedByName_NotAsANullReference()
    {
        var ex = Assert.Throws<AssertionException>(() => Child(Full, "SbNoSuchNode"));

        Assert.IsNotNull(ex);
        StringAssert.Contains("SbNoSuchNode", ex!.Message,
            "сообщение обязано называть недостающий узел");
        StringAssert.Contains("SbFullContent", ex.Message,
            "и перечислять то, что рядом есть — иначе опечатку в имени не отличить от пропажи");
    }

    /// <summary>Каталог выше окна сайдбара — и обязан целиком доставаться
    /// прокруткой. Пока корень был жёсткие 960 px без ScrollRect, 34 пункта
    /// в 7 группах занимали 1280 px, и до нижних восьми было не добраться
    /// ничем.
    ///
    /// Нижний край ищется обходом ДЕТЕЙ содержимого, а не поиском кнопок по
    /// именам каталога: инвариант «всё внутри объявленной высоты» касается
    /// любой строки, которая там лежит, включая заголовки групп.</summary>
    [Test]
    public void CatalogTallerThanTheWindow_StaysReachableThroughScrolling()
    {
        var viewport = (RectTransform)Full;
        var scroll = Full.GetComponent<ScrollRect>();

        Assert.IsNotNull(scroll, "каталог не помещается в панель — без ScrollRect его не достать");

        var content = FullContent;
        Assert.AreSame(content, scroll.content, "прокрутке подсунут не тот узел содержимого");
        Assert.IsTrue(scroll.vertical);
        Assert.AreEqual(ScrollRect.MovementType.Clamped, scroll.movementType,
            "содержимое не должно оттягиваться за края — как в дереве объектов");

        float lowest = 0f;
        string lowestName = "";
        for (int i = 0; i < content.childCount; i++)
        {
            var rt = (RectTransform)content.GetChild(i);
            if (!rt.gameObject.activeSelf) continue;
            float bottom = RowBottom(rt);
            if (bottom <= lowest) continue;
            lowest = bottom;
            lowestName = rt.name;
        }

        Assert.Greater(content.childCount, 0, "в содержимом каталога нет ни одной строки");
        Assume.That(lowest, Is.GreaterThan(viewport.rect.height),
            "проверка имеет смысл, только пока каталог выше окна");
        Assert.GreaterOrEqual(content.sizeDelta.y, lowest,
            $"нижний край «{lowestName}» лежит на {lowest} px, а содержимому объявлено "
            + $"{content.sizeDelta.y} px: всё, что за этой границей, прокрутка не покажет");
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
    public void Collapsed_ShowsGroupIconsAndHidesThePin()
    {
        var pin = Child(Panel, "SbPin");
        Assume.That(pin.gameObject.activeSelf, Is.True);

        Child(Panel, "SbCollapse").GetComponent<Button>().onClick.Invoke();

        Assert.IsFalse(Full.gameObject.activeSelf);
        var mini = Child(Panel, "SbMini");
        Assert.IsTrue(mini.gameObject.activeSelf);
        var strip = Child(mini, "SbMiniContent");
        var techBtn = Child(strip, "SbMini_Техника");

        Assert.IsNull(techBtn.GetComponentInChildren<TMP_Text>(),
            "буквы-псевдоиконки запрещены (docs/UI-GUIDELINES.md §4, дефект D5) — "
            + "в узкой полосе группа рисуется иконкой, а не текстом");
        var icon = Child(techBtn, "SbMini_Техника_Icon").GetComponent<Image>();
        Assert.AreEqual(IconFactory.Appliance, icon.sprite,
            "«Техника» рисуется своей иконкой из IconFactory, той же, что в каталоге группы");
        Assert.IsNotNull(techBtn.gameObject.GetComponent<EventTrigger>(),
            "иконная кнопка обязана иметь tooltip — UI-GUIDELINES §5");
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
        var mini = Child(Panel, "SbMini");
        var content = (RectTransform)Child(mini, "SbMiniContent");
        var scroll = mini.GetComponent<ScrollRect>();

        Assert.IsNotNull(scroll, "полоса групп растёт с каталогом и тоже обязана прокручиваться");
        Assert.AreSame(content, scroll.content);
        Assert.AreEqual(SidebarLayout.MiniContentHeight(SidebarCatalog.Build().Count),
            content.sizeDelta.y,
            "высота полосы считается по числу групп, а не задаётся числом");
    }

    [Test]
    public void RoomMode_GraysOutRegularItems_ButKeepsWallsAndAlwaysItems()
    {
        var shelf = Item("Детали", "Полка").GetComponent<Button>();
        var wall = Item("Помещение", "Стена").GetComponent<Button>();
        var korob = Item("Помещение", EditModeManager.KorobName).GetComponent<Button>();
        var shelfLabel = shelf.GetComponentInChildren<TMP_Text>();
        Color normalColor = shelfLabel.color;

        EditModeManager.SetMode(EditMode.Room);

        Assert.IsFalse(shelf.interactable,
            "в режиме помещения обычные детали не добавляются — кнопка не только "
            + "серая, но и некликабельная");
        AssertSameColor(UIStyle.TextDisabled, shelfLabel.color,
            "недоступный пункт обязан выглядеть недоступным, а не просто молчать в ответ; "
            + "цвет именно TextDisabled, а не TextSecondary: погашенное во всём продукте "
            + "гаснет одной краской (docs/UI-GUIDELINES.md §9)");
        Assert.IsTrue(wall.interactable);
        Assert.IsTrue(korob.interactable, "короб доступен в любом режиме");

        EditModeManager.Reset();

        Assert.IsTrue(shelf.interactable);
        AssertSameColor(normalColor, shelfLabel.color,
            "возврат в обычный режим возвращает и цвет: подписка на "
            + "EditModeManager.Changed работает в обе стороны");
    }
}
