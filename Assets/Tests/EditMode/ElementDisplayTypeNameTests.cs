using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Подпись типа в заголовке окна свойств — данные ЭЛЕМЕНТА, а не лестница в UI.
/// До этих тестов имя типа считала тернарная лестница на 16 ветвей внутри
/// <see cref="ContextMenuUI"/>: новый тип элемента означал правку в UI, а забытая
/// ветка молча давала «Деталь». Теперь на вопрос отвечает сам элемент
/// (<see cref="KitchenElement.DisplayTypeName"/>), а тесты ниже держат обе
/// стороны: значение у каждого типа и то, что заголовок берёт именно его.
/// </summary>
public class ElementDisplayTypeNameTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private Canvas? _canvas;
    private ContextMenuUI? _menu;

    [TearDown]
    public void Teardown()
    {
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        _menu = null;
        _canvas = null;
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private T Spawn<T>(GameObject go) where T : KitchenElement
    {
        _spawned.Add(go);
        var el = go.GetComponent<T>();
        Assert.IsNotNull(el, $"фабрика обязана вернуть {typeof(T).Name}");
        return el!;
    }

    private KitchenElement Part() =>
        Spawn<KitchenElement>(ElementFactory.CreatePart(
            new Vector3Int(800, 400, 18), "Деталь-1", Vector3.zero));

    private static Vector3Int Sheet => new Vector3Int(600, 700, 18);

    [Test]
    public void Part_DisplayTypeName_IsTheGenericBoard()
    {
        Assert.AreEqual("Деталь", Part().DisplayTypeName,
            "базовый KitchenElement — «Деталь»: это значение по умолчанию, от него "
            + "отталкиваются все переопределения");
    }

    [Test]
    public void Facade_DisplayTypeName_IsFacade()
    {
        var facade = Spawn<FacadeElement>(
            ElementFactory.CreateFacade(Sheet, "Ф-1", Vector3.zero));
        Assert.AreEqual("Фасад", facade.DisplayTypeName);
    }

    [Test]
    public void AssembledFacade_DisplayTypeName_OverridesTheFacadeOne()
    {
        var assembled = Spawn<AssembledFacadeElement>(
            ElementFactory.CreateAssembledFacade(Sheet, "СФ-1", Vector3.zero));
        Assert.AreEqual("Сборный фасад", assembled.DisplayTypeName,
            "сборный фасад — наследник FacadeElement: без собственного "
            + "переопределения он назвался бы просто «Фасад»");
    }

    [Test]
    public void RadialShelf_DisplayTypeName_IsRadialShelf()
    {
        var shelf = Spawn<RadialShelfElement>(
            ElementFactory.CreateRadialShelf(600, 300, 18, 100, "РП-1", Vector3.zero));
        Assert.AreEqual("Радиусная полка", shelf.DisplayTypeName);
    }

    [Test]
    public void Panel_DisplayTypeName_IsHardboard()
    {
        var panel = Spawn<PanelElement>(ElementFactory.Instance.CreatePanel(
            new Vector3Int(600, 700, 3), "П-1", Vector3.zero));
        Assert.AreEqual("ДВП/ХДФ", panel.DisplayTypeName);
    }

    [Test]
    public void Table_DisplayTypeName_IsTable()
    {
        var table = Spawn<TableElement>(ElementFactory.CreateTable(
            new Vector3Int(1200, 750, 600), "Стол-1", Vector3.zero));
        Assert.AreEqual("Стол", table.DisplayTypeName);
    }

    [Test]
    public void RadiusTable_DisplayTypeName_IsRadiusTable()
    {
        var table = Spawn<RadiusTableElement>(ElementFactory.CreateRadiusTable(
            new Vector3Int(1200, 750, 600), "РС-1", Vector3.zero));
        Assert.AreEqual("Радиусный стол", table.DisplayTypeName,
            "радиусный стол — отдельный тип, а не вариант TableElement");
    }

    [Test]
    public void Pillar_DisplayTypeName_IsPillar()
    {
        var pillar = Spawn<PillarElement>(
            ElementFactory.CreatePillar(700, "Оп-1", Vector3.zero));
        Assert.AreEqual("Опора", pillar.DisplayTypeName);
    }

    [Test]
    public void Sink_DisplayTypeName_IsSink()
    {
        var sink = Spawn<SinkElement>(ElementFactory.CreateSink("М-1", Vector3.zero));
        Assert.AreEqual("Мойка", sink.DisplayTypeName);
    }

    [Test]
    public void LightSource_DisplayTypeName_IsLightSource()
    {
        var lamp = Spawn<LightSourceElement>(
            ElementFactory.CreateLightSource("Л-1", Vector3.zero));
        Assert.AreEqual("Источник света", lamp.DisplayTypeName);
    }

    [Test]
    public void Window_DisplayTypeName_IsWindow()
    {
        var window = Spawn<WindowElement>(ElementFactory.CreateWindow(
            new Vector3Int(1200, 1400, 200), "Ок-1", Vector3.zero));
        Assert.AreEqual("Окно", window.DisplayTypeName);
    }

    [Test]
    public void Door_DisplayTypeName_IsDoor()
    {
        var door = Spawn<DoorElement>(ElementFactory.CreateDoor(
            new Vector3Int(900, 2100, 200), "Дв-1", Vector3.zero));
        Assert.AreEqual("Дверь", door.DisplayTypeName);
    }

    [Test]
    public void Oven_DisplayTypeName_IsTheApplianceModel()
    {
        var oven = Spawn<OvenElement>(ElementFactory.CreateOven("Дх-1", Vector3.zero));
        Assert.AreEqual(OvenElement.MODEL, oven.DisplayTypeName,
            "у техники заголовок показывает МОДЕЛЬ, а не родовое слово: "
            + "константа живёт на самом классе, и лестница в UI её только пересказывала");
    }

    [Test]
    public void Dishwasher_DisplayTypeName_IsTheApplianceModel()
    {
        var dw = Spawn<DishwasherElement>(
            ElementFactory.CreateDishwasher("ПММ-1", Vector3.zero));
        Assert.AreEqual(DishwasherElement.MODEL, dw.DisplayTypeName);
    }

    [Test]
    public void FreeCooktop_DisplayTypeName_IsTheGenericWord()
    {
        var cooktop = Spawn<CooktopElement>(
            ElementFactory.CreateCooktop("Вп-1", Vector3.zero));
        Assert.IsFalse(cooktop.HasFixedSize, "без модели варочная свободного размера");
        Assert.AreEqual("Варочная", cooktop.DisplayTypeName);
    }

    [Test]
    public void PresetCooktop_DisplayTypeName_IsTheModel()
    {
        var cooktop = Spawn<CooktopElement>(ElementFactory.CreateCooktop(
            "Вп-2", Vector3.zero, CooktopElement.MODEL_BOSCH_PUE611BB5E));
        Assert.IsTrue(cooktop.HasFixedSize, "модель задаёт габарит");
        Assert.AreEqual(CooktopElement.MODEL_BOSCH_PUE611BB5E, cooktop.DisplayTypeName,
            "один класс обслуживает и свободную варочную, и пресет — "
            + "подпись обязана различать их сама");
    }

    [Test]
    public void Drawer_DisplayTypeName_ComesFromTheDrawerSystem()
    {
        var drawer = Spawn<DrawerElement>(ElementFactory.CreateDrawer(
            DrawerType.A, 450, DrawerColor.Anthracite, 500, "Ящ-1", Vector3.zero));
        Assert.AreEqual(DrawerConstants.GetDefaultName(drawer.System), drawer.DisplayTypeName,
            "у ящика подпись зависит от системы (GTV/Movento), поэтому это не "
            + "константа, а функция состояния самого ящика");
    }

    [Test]
    public void ContextMenuTitle_ReadsTheNameFromTheElement()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);

        var oven = Spawn<OvenElement>(ElementFactory.CreateOven("Дх-2", Vector3.zero));
        _menu!.Open(oven);

        var title = _canvas!.transform.Find("ContextMenu")!.Find("CtxTitle")!
            .GetComponent<TMP_Text>();
        Assert.AreEqual($"{oven.DisplayTypeName} — {oven.PartName}", title.text,
            "заголовок обязан спрашивать подпись у элемента: собственная копия "
            + "лестницы в UI разошлась бы с элементом при первом же новом типе");
    }
}
