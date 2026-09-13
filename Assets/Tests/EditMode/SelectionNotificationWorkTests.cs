using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Клик по детали стоил 200 мс на сцене в 411 деталей, и сумма ВСЕХ маркеров
/// такого кадра была 21 мс — остальное не измерял никто. Здесь стерегутся две единицы
/// РАБОТЫ, которые клик заказывал по нескольку раз без нужды; обе считаются штуками, а не
/// миллисекундами (тест по времени в batch флаки и зеленеет на выкинутом раннем выходе).
///
/// **Оповещение.** Одно событие <c>OnSelectionChanged</c> тянет за собой пять подписчиков
/// из четырёх слоёв: <c>ContextMenuUI</c> пересобирает меню, <c>ResizeHandleManager</c> —
/// ручки габаритов, <c>HierarchyPanelUI</c> — строки дерева. <c>Select</c> рассылал его
/// ДВАЖДЫ (сначала <c>null</c> из вложенного <c>DeselectAll</c>, потом сам элемент), то
/// есть ручки габаритов сносились и собирались заново на каждом клике; <c>SelectOnly</c>
/// на группе из N деталей рассылал N+1 раз. Правило теперь одно: один жест — одно
/// оповещение, и несёт оно то же самое, что несла ПОСЛЕДНЯЯ рассылка старого кода
/// (итоговый <c>Selected</c>), иначе подписчик увидит чужое состояние.
///
/// **Проверка сцены.** Возврат материала одной детали кончается
/// <c>ElementHighlighter.ApplyForElement</c>, а это <c>PartRegistry.GetAll</c> плюс полная
/// <c>ConstraintValidator.Validate</c> — 17,3 мс по дампу. Снятие выделения с N деталей
/// заказывало N таких проверок. Теперь на группу приходится одна, через уже существующий
/// <c>HighlightBatch</c>.
///
/// Счётчик заказов стоит ДО проверки «а есть ли в сцене подсветчик»: заказ — это решение
/// выделения, и цена целиком в нём. Поэтому стенду не нужен живой
/// <c>ElementHighlighter</c> — а он здесь и вреден, потому что материалы сверяются ПО
/// ССЫЛКЕ, а живой подсветчик перекрашивает деталь сразу после возврата.</summary>
public class SelectionNotificationWorkTests
{
    private GameObject? _selectionGo;
    private SelectionManager? _selection;
    private readonly List<GameObject> _elements = new List<GameObject>();
    private ElementHighlighter? _highlighter;

    private readonly List<KitchenElement?> _heard = new List<KitchenElement?>();

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        _highlighter = ElementHighlighter.Instance;
        ElementHighlighter.Instance = null;
        HighlightBatch.Reset();
        SelectionWorkLog.Forget();
        _heard.Clear();

        _selectionGo = new GameObject("SelectionManager сторожа");
        _selection = _selectionGo.AddComponent<SelectionManager>();
        SelectionManager.Instance = _selection;
        _selection.OnSelectionChanged += Hear;
    }

    [TearDown]
    public void TearDown()
    {
        LogAssert.ignoreFailingMessages = true;
        if (_selection != null)
        {
            _selection.OnSelectionChanged -= Hear;
            _selection.DeselectAll();
        }
        SelectionManager.Instance = null;
        if (_selectionGo != null) Object.DestroyImmediate(_selectionGo);
        foreach (var go in _elements)
            if (go != null) Object.DestroyImmediate(go);
        _elements.Clear();
        _selectionGo = null;
        _selection = null;
        ElementHighlighter.Instance = _highlighter;
        _highlighter = null;
        HighlightBatch.Reset();
        SelectionWorkLog.Forget();
        MaterialManager.ClearCache();
        LogAssert.ignoreFailingMessages = false;
    }

    private void Hear(KitchenElement? selected) => _heard.Add(selected);

    private (KitchenElement element, MeshRenderer root, MeshRenderer child) MakeComposite(string name)
    {
        var go = new GameObject(name);
        _elements.Add(go);
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = new Vector3Int(600, 400, 18);

        var root = go.AddComponent<MeshRenderer>();
        go.AddComponent<MeshFilter>();

        var childGo = new GameObject(name + "/стекло");
        childGo.transform.SetParent(go.transform, worldPositionStays: false);
        var child = childGo.AddComponent<MeshRenderer>();
        childGo.AddComponent<MeshFilter>();

        var start = MaterialManager.GetSharedMaterial(MaterialCatalog.Default);
        Assert.IsNotNull(start, "без материала на рендерере запоминать нечего");
        root.sharedMaterial = start;
        child.sharedMaterial = start;

        Assume.That(ElementRenderers.BodyOf(element).Count, Is.EqualTo(2),
            "тело элемента — оба меша; на одном сторож ничего не проверит");
        return (element, root, child);
    }

    [Test]
    public void ClickingAnotherElement_NotifiesListenersOnce_AndCarriesTheNewSelection()
    {
        var (first, _, _) = MakeComposite("Первая деталь");
        var (second, _, _) = MakeComposite("Вторая деталь");

        _selection!.Select(first);
        Assume.That(_heard.Count, Is.EqualTo(1), "выделение первой детали — тоже один жест");
        _heard.Clear();

        _selection.Select(second);

        Assert.AreEqual(1, _heard.Count,
            "было два оповещения на клик: null из вложенного DeselectAll, затем сам элемент. "
            + "Ручки габаритов на этом сносились и собирались заново, а меню успевало "
            + "назначить себе отложенное закрытие и тут же его отменить. Услышано: "
            + _heard.Count);
        Assert.AreSame(second, _heard[0],
            "оповещение обязано нести итоговый Selected — то же, что несла ПОСЛЕДНЯЯ "
            + "рассылка старого кода");
    }

    [Test]
    public void SelectingAGroup_NotifiesOnce_NotOncePerMember()
    {
        var members = new List<KitchenElement>
        {
            MakeComposite("Деталь 1").element,
            MakeComposite("Деталь 2").element,
            MakeComposite("Деталь 3").element,
        };

        _selection!.SelectOnly(members);

        Assert.AreEqual(1, _heard.Count,
            "SelectOnly рассылал N+1 раз: один null из DeselectAll и по одному на каждую "
            + "добавленную деталь. Каждая рассылка — весь хвост подписчиков");
        Assert.AreSame(members[0], _heard[0],
            "последняя рассылка старого кода несла тот же Selected — первую деталь группы");
        Assert.AreEqual(3, _selection.SelectedElements.Count,
            "схлопывание оповещений не имеет права менять САМО выделение");
    }

    [Test]
    public void DeselectingAGroup_AsksForOneSceneValidation_NotOnePerElement()
    {
        var members = new List<KitchenElement>
        {
            MakeComposite("Деталь 1").element,
            MakeComposite("Деталь 2").element,
            MakeComposite("Деталь 3").element,
        };
        _selection!.SelectOnly(members);
        SelectionWorkLog.Forget();

        _selection.DeselectAll();

        Assert.AreEqual(1, SelectionWorkLog.Count(SelectionWork.SceneValidationAsked),
            "каждая из трёх деталей заказывала свою полную проверку сцены — 17,3 мс за "
            + "штуку на сцене в 411 деталей");
    }

    [Test]
    public void DeselectingASingleElement_StillAsksForItsOwnPointRefresh()
    {
        var (element, _, _) = MakeComposite("Одинокая деталь");
        _selection!.Select(element);
        SelectionWorkLog.Forget();

        _selection.DeselectAll();

        Assert.AreEqual(1, SelectionWorkLog.Count(SelectionWork.SceneValidationAsked),
            "обратный вход к предыдущему тесту: на одной детали точечное обновление дешевле "
            + "общего, и подменять его обходом всей сцены — это замедление, а не ускорение");
    }

    [Test]
    public void SelectionPaintsEveryRendererOfTheElement_AndGivesEachOneBack()
    {
        var (element, root, child) = MakeComposite("Составная деталь");
        var rootBefore = root.sharedMaterial;
        var childBefore = child.sharedMaterial;

        _selection!.Select(element);

        Assert.AreNotSame(rootBefore, root.sharedMaterial, "выделение красит корень");
        Assert.AreNotSame(childBefore, child.sharedMaterial, "и дочерний меш — деталь целиком");
        Assert.AreEqual(2, SelectionWorkLog.Count(SelectionWork.TintCreated),
            "по краске на рендерер — столько же, сколько было до схлопывания оповещений");

        _selection.DeselectAll();

        Assert.AreSame(rootBefore, root.sharedMaterial, "снятие возвращает исходный материал корня");
        Assert.AreSame(childBefore, child.sharedMaterial, "и дочернего меша");
    }

    [Test]
    public void DeselectingAGroup_GivesBackTheMaterialOfEveryMember()
    {
        var first = MakeComposite("Деталь 1");
        var second = MakeComposite("Деталь 2");
        var third = MakeComposite("Деталь 3");
        var before = first.root.sharedMaterial;

        _selection!.SelectOnly(new List<KitchenElement>
        {
            first.element, second.element, third.element,
        });
        Assume.That(first.root.sharedMaterial, Is.Not.SameAs(before),
            "предпосылка теста: группа действительно выделилась");

        _selection.DeselectAll();

        Assert.AreSame(before, first.root.sharedMaterial, "первая деталь группы");
        Assert.AreSame(before, second.child.sharedMaterial,
            "дочерний меш второй: отложенное через HighlightBatch обновление не имеет права "
            + "оставить тонировку на ком-то из группы");
        Assert.AreSame(before, third.root.sharedMaterial, "третья деталь группы");
    }
}
