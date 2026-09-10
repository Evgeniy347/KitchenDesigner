using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class ElementTypeConverterTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    /// <summary>Панель строится ОДИН раз на класс: сборка контекстного меню —
    /// 0,31 с, и десять сборок это 3,0 с прогона EditMode при бюджете 170.
    /// Боевой сценарий — это и есть ОДНА панель, переоткрываемая через
    /// <c>Open</c>; полный разбор того, что <c>Open</c> сбрасывает, — в сводке
    /// <see cref="ContextMenuLayoutTests"/>.
    ///
    /// Для ЭТОГО класса важно, что список типов не снимок сборки:
    /// <c>ElementTypeConverter.ShowFor</c> зовёт <c>ClearOptions</c> и набивает
    /// строки заново на КАЖДОМ открытии, поэтому общая панель не может показать
    /// родню предыдущего элемента. Дропдауны, которые действительно снимаются в
    /// <c>Build</c> (материалы, декоры), этот класс не читает.
    ///
    /// Двух тестов, которые панель не открывают, это не касается: один
    /// спрашивает имя узла, расставленное <c>Build</c>
    /// (<see cref="TypeDropdown_KeepsItsName"/>), второй — арифметику самого
    /// <c>ElementTypeConverter.GroupOf</c> и панели не касается вовсе.</summary>
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

    /// <summary>Панель переживает тест — значит потестовое состояние вокруг неё
    /// возвращается на место здесь: фокус (<c>RefreshUnfocused</c> не трогает
    /// сфокусированное поле) и окно склейки правок (в EditMode
    /// <c>Time.frameCount</c> стоит на месте, и взведённое предыдущим тестом
    /// окно съело бы первую правку следующего).</summary>
    [SetUp]
    public void Setup()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
    }

    /// <summary>Панель закрывается ДО уничтожения элементов: <c>Close</c>
    /// обнуляет <c>_target</c>, иначе панель осталась бы с уничтоженной деталью
    /// в руках, а взведённая кнопка «Удалить» — взведённой на следующий
    /// тест.</summary>
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

    private KitchenElement Board() =>
        Spawn<KitchenElement>(ElementFactory.CreatePart(
            new Vector3Int(600, 300, 18), "Полка", Vector3.zero));

    private FacadeElement Facade() =>
        Spawn<FacadeElement>(ElementFactory.CreateFacade(
            new Vector3Int(450, 700, 18), "Фасад", Vector3.zero, 2, 2, 2, 2));

    private DrawerElement Drawer() =>
        Spawn<DrawerElement>(ElementFactory.CreateDrawer(
            DrawerType.A, 350, DrawerColor.Anthracite, 400, "Ящик", Vector3.zero));

    private TMP_Dropdown TypeDropdown() =>
        _canvas!.transform.Find("ContextMenu")!.Find("CtxType")!.GetComponent<TMP_Dropdown>();

    private static List<string> Labels(TMP_Dropdown dd)
    {
        var texts = new List<string>();
        foreach (var o in dd.options) texts.Add(o.text);
        return texts;
    }

    /// <summary>Тест, которому не нужно открывать панель: узел строки «Тип»
    /// расставляет <c>Build</c>, и вопрос здесь ровно про имя, по которому его
    /// ищут остальные. Про содержимое списка этот тест не утверждает НИЧЕГО —
    /// иначе с общей панелью он читал бы родню элемента, открытого предыдущим
    /// тестом.</summary>
    [Test]
    public void TypeDropdown_KeepsItsName()
    {
        Assert.NotNull(_canvas!.transform.Find("ContextMenu")!.Find("CtxType"),
            "строка «Тип» ищется тестами по имени CtxType");
    }

    [Test]
    public void Board_OffersOnlyTheStructuralFamily()
    {
        _menu!.Open(Board());
        CollectionAssert.AreEqual(
            new[] { "Деталь", "Фасад", "Сборный фасад", "Радиусная полка" },
            Labels(TypeDropdown()),
            "конвертация идёт ТОЛЬКО внутри родственной группы: деталь не превращается в ящик");
    }

    [Test]
    public void Drawer_OffersOnlyTheDrawerFamily()
    {
        _menu!.Open(Drawer());
        CollectionAssert.AreEqual(new[] { "Ящик GTV", "Ящик Movento" }, Labels(TypeDropdown()),
            "у ящика родственная группа своя — смена системы выдвижения, а не смена роли");
    }

    [Test]
    public void Table_HasNoTypeChoicesAndHidesTheRow()
    {
        var table = Spawn<TableElement>(ElementFactory.CreateTable(
            new Vector3Int(1200, 750, 700), "Стол", Vector3.zero));
        _menu!.Open(table);

        Assert.AreEqual(0, TypeDropdown().options.Count,
            "у стола своя роль, конвертации нет");
        Assert.IsFalse(TypeDropdown().gameObject.activeSelf,
            "строка «Тип» без вариантов не показывается");
    }

    /// <summary>Вопрос к самой группировке, без панели: <c>GroupOf</c> — чистая
    /// функция, и элементы здесь нужны ей только как аргументы.</summary>
    [Test]
    public void GroupOf_AppliancesAndStructureAreNotRelatives()
    {
        Assert.AreEqual(ElementTypeConverter.Group.Structural,
            ElementTypeConverter.GroupOf(Board()));
        Assert.AreEqual(ElementTypeConverter.Group.Structural,
            ElementTypeConverter.GroupOf(Facade()),
            "фасад и сборный фасад — подклассы одной структурной группы");
        Assert.AreEqual(ElementTypeConverter.Group.Drawer,
            ElementTypeConverter.GroupOf(Drawer()));
        Assert.AreEqual(ElementTypeConverter.Group.None, ElementTypeConverter.GroupOf(null));
    }

    [Test]
    public void CurrentChoice_MatchesTheOpenedElement()
    {
        _menu!.Open(Facade());
        Assert.AreEqual("Фасад", TypeDropdown().options[TypeDropdown().value].text,
            "открытый элемент обязан быть выбран в списке — иначе строка предлагает сменить "
            + "тип на тот, который уже стоит");
    }

    [Test]
    public void SelectingTheSameType_ChangesNothing()
    {
        var board = Board();
        _menu!.Open(board);
        CommandStack.Clear();

        _menu!.Types.Select(0);

        Assert.AreEqual(0, CommandStack.UndoCount,
            "выбор текущего типа — не правка: пересоздавать элемент незачем");
        Assert.IsTrue(board != null, "элемент остался прежним");
    }

    [Test]
    public void SelectingAnotherStructuralType_ConvertsTheElement()
    {
        var board = Board();
        _menu!.Open(board);

        _menu!.Types.Select(1);

        var converted = PartRegistry.GetAll();
        bool anyFacade = false;
        foreach (var el in converted)
            if (el is FacadeElement) anyFacade = true;
        Assert.IsTrue(anyFacade,
            "родственный структурный тип пересоздаёт элемент через ElementConverter");
    }

    [Test]
    public void SelectingAnotherDrawerSystem_KeepsTheSameElement()
    {
        var drawer = Drawer();
        _menu!.Open(drawer);
        Assume.That(drawer.System, Is.EqualTo(DrawerSystem.Gtv));

        _menu!.Types.Select(1);

        Assert.AreEqual(DrawerSystem.Movento, drawer.System,
            "смена системы выдвижения — тот же элемент: пересобирается только меш и меню");
    }

    [Test]
    public void SwitchingTheDrawerSystem_ReopensThePanelOnTheSameDrawer()
    {
        var drawer = Drawer();
        _menu!.Open(drawer);

        _menu!.Types.Select(1);

        Assert.AreEqual("Ящик Movento", TypeDropdown().options[TypeDropdown().value].text,
            "панель обязана переоткрыться: заголовок, значение списка и спецификация "
            + "иначе остались бы от прежней системы");
    }
}
