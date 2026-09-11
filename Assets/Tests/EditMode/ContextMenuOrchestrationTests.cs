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
    /// КАЖДЫЙ тест этого класса без исключений.</summary>
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
    }

    /// <summary><c>Close()</c> обязан идти ДО <c>DestroyImmediate</c> спавнов: он
    /// обнуляет <c>_target</c>, иначе живая панель осталась бы с уничтоженным элементом
    /// в руках.
    ///
    /// Он же снимает защёлку отложенного закрытия, и потому в <c>[SetUp]</c> её больше
    /// не сбрасывают руками: раньше <c>Close()</c> защёлку НЕ снимал, взведённая в
    /// <see cref="Deselecting_DoesNotClosethePanelInTheSameFrame"/> она переезжала в
    /// следующий тест и гасила ему панель, как только кадр оказывался новее
    /// взведённого. Чинить это в <c>[SetUp]</c> было лечением симптома: та же защёлка
    /// точно так же переживала закрытие и у живого пользователя — см.
    /// <see cref="PanelOpenedAfterDeselect_StaysOpen"/>.</summary>
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

    /// <summary>Защёлка отложенного закрытия принадлежит ОДНОЙ жизни панели и обязана
    /// умереть вместе с ней. Пока её снимал только <c>OnSelectionChanged</c> с непустым
    /// элементом, она переживала и закрытие, и следующее открытие: панель, открытую не
    /// через <c>SelectionManager</c> (клик мимо детали снял выделение, затем панель
    /// подняли из кода), ближайший <c>Update</c> гасил сам собой — пользователь видел,
    /// как панель мигнула и пропала.
    ///
    /// Проверка не про кадр, а про состояние, и потому не зависит от порядка тестов: в
    /// EditMode <c>Time.frameCount</c> внутри теста не растёт, и «сработает ли
    /// <c>ProcessDeferredClose</c>» тут ответа не даёт вовсе — а взведённая защёлка
    /// видна сразу.</summary>
    [Test]
    public void PanelOpenedAfterDeselect_StaysOpen()
    {
        _menu!.Open(Board("Полка"));
        _menu!.OnSelectionChanged(null);
        Assume.That(_menu!.DeferredCloseIsPending, Is.True,
            "предусловие: снятие выделения взводит отложенное закрытие");

        _menu!.Close();
        _menu!.Open(Board("Другая"));
        _menu!.ProcessDeferredClose();

        Assert.IsFalse(_menu!.DeferredCloseIsPending,
            "открытие — начало новой жизни панели: отложенное закрытие, взведённое до "
            + "него, к этой панели уже не относится");
        Assert.IsTrue(PanelOpen(), "иначе панель мигнула бы и пропала без причины");
    }

    /// <summary>Второй конец той же жизни. Закрытая панель не может иметь незакрытого
    /// дела: без этого защёлка тихо ждала в поле и стреляла в первую же панель,
    /// открытую после неё, — в том числе через тест, идущий следом.</summary>
    [Test]
    public void Closing_ForgetsTheDeferredClose()
    {
        _menu!.Open(Board("Полка"));
        _menu!.OnSelectionChanged(null);

        _menu!.Close();

        Assert.IsFalse(_menu!.DeferredCloseIsPending,
            "панель уже закрыта — закрывать её второй раз нечему");
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
