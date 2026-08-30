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

    [SetUp]
    public void Setup()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
    }

    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
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

    [Test]
    public void AssembledFillDropdown_ShowsTheCurrentFill()
    {
        var assembled = Spawn<AssembledFacadeElement>(ElementFactory.CreateAssembledFacade(
            new Vector3Int(450, 700, 18), "Сборный", Vector3.zero));
        assembled.Fill = AssembledFill.Glass;
        _menu!.Open(assembled);

        Assert.AreEqual(2, Panel().Find("CtxFill")!.GetComponent<TMP_Dropdown>().value,
            "порядок пунктов не совпадает с порядком значений AssembledFill, поэтому перевод "
            + "значения в индекс идёт по таблице, а не кастом");
    }
}
