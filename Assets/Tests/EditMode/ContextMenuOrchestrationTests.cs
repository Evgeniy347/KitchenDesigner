using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class ContextMenuOrchestrationTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    /// <summary>Панель строится ОДИН раз на класс: сборка контекстного меню — 0,31 с,
    /// и девять сборок это 2,8 с из прогона EditMode при бюджете 170 с. Почему это
    /// безопасно — в сводке <see cref="ContextMenuLayoutTests"/>: боевой сценарий и есть
    /// ОДНА панель, переоткрываемая через <c>Open</c>, и через <c>Open</c> проходит
    /// КАЖДЫЙ тест этого класса без исключений.
    ///
    /// Своё, отдельное от прочих секций, у этого класса — отложенное закрытие: см.
    /// <see cref="ForgetDeferredClose"/>.</summary>
    [OneTimeSetUp]
    public void BuildThePanelOnce()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
    }

    [OneTimeTearDown]
    public void DropThePanel()
    {
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
    }

    /// <summary>Панель переживает тест — значит потестовое состояние сбрасывается здесь.
    /// <c>ForgetLastApplyFrame</c> — окно склейки правок: в EditMode
    /// <c>Time.frameCount</c> стоит на месте, и окно, взведённое предыдущим тестом,
    /// съело бы первую правку следующего.
    ///
    /// Фокус здесь не удобство, а несущая часть: <c>RefreshUnfocused</c> МОЛЧА
    /// пропускает сфокусированное поле, а именно через него панель показывает «Заход в
    /// корпус» и поля посадки опоры. Сфокусированное поле даёт и ложное «значение не
    /// обновилось», и ложное «обновилось», а <c>EventSystem</c> в EditMode один на весь
    /// прогон и переживает не только тест, но и класс.</summary>
    [SetUp]
    public void Setup()
    {
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
        ConfirmDeleteButton.DisarmAll();
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null) es.SetSelectedGameObject(null);
        ForgetDeferredClose();
    }

    /// <summary>Вторая защёлка кадра, и она принадлежит ИМЕННО этому классу:
    /// <c>OnSelectionChanged(null)</c> не закрывает панель сразу, а взводит
    /// <c>_deferCloseFrame</c>, и <c>Close()</c> его НЕ снимает (а <c>Open</c> не
    /// снимает потому, что приходящий из него <c>OnSelectionChanged</c> отсекается
    /// флагом <c>_openInProgress</c>). Со своей панелью на тест защёлка умирала вместе
    /// с панелью; с общей — <see cref="Deselecting_DoesNotClosethePanelInTheSameFrame"/>
    /// оставляет её взведённой, и следующий тест, который зовёт
    /// <c>ProcessDeferredClose</c>, закрыл бы панель, как только его кадр окажется
    /// НОВЕЕ взведённого. Внутри одного кадра EditMode это не срабатывает, поэтому и
    /// падало бы не всегда — что хуже, чем всегда.
    ///
    /// Снимается защёлка единственным путём, который для неё есть: панель открывают на
    /// проходной детали и повторяют выбор того же элемента — <c>OnSelectionChanged</c>
    /// с непустым элементом обнуляет <c>_deferCloseFrame</c>, а <c>element ==
    /// _target</c> не даёт ей переоткрыться.</summary>
    private void ForgetDeferredClose()
    {
        var scratch = ElementFactory.CreatePart(new Vector3Int(120, 120, 12), "Сброс", Vector3.zero);
        var element = scratch.GetComponent<KitchenElement>();
        _menu!.Open(element);
        _menu!.OnSelectionChanged(element);
        _menu!.Close();
        Object.DestroyImmediate(scratch);
        PartRegistry.Clear();
    }

    /// <summary><c>Close()</c> обязан идти ДО <c>DestroyImmediate</c> спавнов: он
    /// обнуляет <c>_target</c>, иначе живая панель осталась бы с уничтоженным элементом
    /// в руках.</summary>
    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        if (_menu != null) _menu!.Close();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private T Spawn<T>(GameObject go) where T : KitchenElement
    {
        _spawned.Add(go);
        return go.GetComponent<T>();
    }

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;

    private bool PanelOpen() => Panel().gameObject.activeSelf;

    private KitchenElement Board(string name) =>
        Spawn<KitchenElement>(ElementFactory.CreatePart(
            new Vector3Int(600, 300, 18), name, Vector3.zero));

    private TMP_InputField Insertion() =>
        Panel().Find("F_Заход в корпус")!.GetComponent<TMP_InputField>();

    /// <summary>«Заход в корпус» больше не вводят — его показывают. Поле в живом
    /// проекте стояло на заводских 25 мм, пока резьба сидела на 38: два описания
    /// одной величины разошлись, и сверить их было нечем. Строка осталась на
    /// месте (её читают), но правит её геометрия.</summary>
    [Test]
    public void ScrewLegInsertionRow_ShowsTheMeasuredDepth_AndIsNotEditable()
    {
        var host = Spawn<KitchenElement>(ElementFactory.CreatePart(
            new Vector3Int(482, 80, 16), "Царга", new Vector3(0f, 0.060f, 0f)));
        var leg = Spawn<ScrewLegElement>(ElementFactory.CreateScrewLeg("Opora",
            new Vector3(0f, 0.029f, 0f)));
        ScrewLegHostLink.Apply(leg, PartRegistry.GetAll());

        _menu!.Open(leg);

        Assert.AreEqual(host.PartName, leg.AttachedToName, "предусловие: хозяин вывелся");
        Assert.AreEqual("38", Insertion().text,
            "резьба кончается на 58 мм, дно царги — на 20: панель показывает измеренные 38, "
            + "а не заводские 25 из поля");
        Assert.IsFalse(Insertion().interactable,
            "правит эту величину геометрия, а не человек — иначе поле снова разойдётся "
            + "с тем, что показывает сцена");
    }

    /// <summary>Тихий ноль здесь запрещён: он неотличим от «вошла на 0 мм».
    /// У опоры без хозяина величины нет вовсе, и панель говорит именно это.</summary>
    [Test]
    public void ScrewLegInsertionRow_WithNoHost_ShowsADash()
    {
        var leg = Spawn<ScrewLegElement>(ElementFactory.CreateScrewLeg("Odna",
            new Vector3(0f, 0.029f, 0f)));
        ScrewLegHostLink.Apply(leg, PartRegistry.GetAll());

        _menu!.Open(leg);

        Assert.AreEqual("", leg.AttachedToName, "предусловие: опора ни во что не ввинчена");
        Assert.AreEqual("—", Insertion().text, "нечего мерить — и нуля быть не должно");
    }

    [Test]
    public void SelectingAnotherElement_ThroughDeselectFirst_KeepsThePanelOpen()
    {
        var first = Board("Первая");
        var second = Board("Вторая");
        _menu!.Open(first);

        _menu!.OnSelectionChanged(null);
        _menu!.OnSelectionChanged(second);
        _menu!.ProcessDeferredClose();

        Assert.IsTrue(PanelOpen(),
            "SelectionManager.Select() сперва шлёт DeselectAll, поэтому OnSelectionChanged(null) "
            + "приходит ПЕРЕД новым элементом: без отложенного закрытия панель мигала бы "
            + "на каждом переключении");
    }

    [Test]
    public void Deselecting_DoesNotClosethePanelInTheSameFrame()
    {
        _menu!.Open(Board("Полка"));

        _menu!.OnSelectionChanged(null);

        Assert.IsTrue(PanelOpen(),
            "закрытие откладывается до конца кадра: снятие выделения — первая половина "
            + "переключения на другой элемент, и закрыться прямо здесь значит мигнуть панелью");
    }

    [Test]
    public void SelectionChange_DuringOpen_IsIgnored()
    {
        var board = Board("Полка");
        _menu!.Open(board);
        _menu!.ProcessDeferredClose();

        Assert.IsTrue(PanelOpen(),
            "Open() сам зовёт SelectionManager.Select(), и приходящий оттуда "
            + "OnSelectionChanged не должен закрывать только что открытую панель");
    }

    [Test]
    public void FacadeRow_SaysDrawerFacade_ForADrawer()
    {
        var drawer = Spawn<DrawerElement>(ElementFactory.CreateDrawer(
            DrawerType.A, 350, DrawerColor.Anthracite, 400, "Ящик", Vector3.zero));
        _menu!.Open(drawer);

        Assert.AreEqual("Фасад ящика",
            Panel().Find("L_Фасад ящика")!.GetComponent<TMP_Text>().text,
            "в панели ящика рядом стоят и другие «фасадные» строки — подпись обязана уточнять, "
            + "о каком фасаде речь");
    }

    [Test]
    public void FacadeRow_SaysPlainFacade_ForADishwasher()
    {
        var dishwasher = Spawn<DishwasherElement>(
            ElementFactory.CreateDishwasher("Посудомойка", Vector3.zero));
        _menu!.Open(dishwasher);

        Assert.AreEqual("Фасад",
            Panel().Find("L_Фасад ящика")!.GetComponent<TMP_Text>().text,
            "у посудомойки уточнять нечего — фасад у неё один");
    }

    [Test]
    public void AssembledFillDropdown_OrderIsBlindShowcaseGlass()
    {
        var assembled = Spawn<AssembledFacadeElement>(ElementFactory.CreateAssembledFacade(
            new Vector3Int(450, 700, 18), "Сборный", Vector3.zero));
        _menu!.Open(assembled);
        var dd = Panel().Find("CtxFill")!.GetComponent<TMP_Dropdown>();

        dd.value = 0;
        Assert.AreEqual(AssembledFill.Blind, assembled.Fill, "первый пункт — глухая панель");
        dd.value = 1;
        Assert.AreEqual(AssembledFill.Open, assembled.Fill, "второй — витрина (пустой центр)");
        dd.value = 2;
        Assert.AreEqual(AssembledFill.Glass, assembled.Fill, "третий — стекло");
    }

    /// <summary>Панель одна на класс, поэтому «после Open дропдаун показывает стекло»
    /// прошло бы вхолостую на двойке, оставшейся от соседнего теста. Открываем СНАЧАЛА
    /// глухую панель, убеждаемся в нуле — и только потом стеклянную: невыполненная
    /// перерисовка теперь краснеет.</summary>
    [Test]
    public void AssembledFillDropdown_ShowsTheCurrentFill()
    {
        var blind = Spawn<AssembledFacadeElement>(ElementFactory.CreateAssembledFacade(
            new Vector3Int(451, 701, 18), "Глухой", Vector3.zero, AssembledFill.Blind));
        _menu!.Open(blind);
        Assert.AreEqual(0, Panel().Find("CtxFill")!.GetComponent<TMP_Dropdown>().value,
            "предусловие: панель показывает глухую панель");

        var assembled = Spawn<AssembledFacadeElement>(ElementFactory.CreateAssembledFacade(
            new Vector3Int(450, 700, 18), "Сборный", new Vector3(0.9f, 0f, 0f)));
        assembled.Fill = AssembledFill.Glass;
        _menu!.Open(assembled);

        Assert.AreEqual(2, Panel().Find("CtxFill")!.GetComponent<TMP_Dropdown>().value,
            "порядок пунктов не совпадает с порядком значений AssembledFill, поэтому перевод "
            + "значения в индекс идёт по таблице, а не кастом");
    }
}
