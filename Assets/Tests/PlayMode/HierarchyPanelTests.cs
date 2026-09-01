using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Окно «Сцена»: выделение в обе стороны, группы, фильтр по имени,
/// выпадающий список «Переместить в…» и поллинг изменений сцены.
/// PlayMode — перестроение списка сносит старые строки через Object.Destroy.</summary>
public class HierarchyPanelTests
{
    private GameObject _cameraGo = null!;
    private GameObject _canvasGo = null!;
    private GameObject _panelHost = null!;
    private GameObject _selectionHost = null!;
    private HierarchyPanelUI _panel = null!;
    private SelectionManager _selection = null!;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        GroupManager.Clear();
        ProjectWindows.Clear();

        _cameraGo = new GameObject("Main Camera");
        _cameraGo.tag = "MainCamera";
        _cameraGo.AddComponent<Camera>();

        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();

        _selectionHost = new GameObject("SelectionManagerHost");
        _selection = _selectionHost.AddComponent<SelectionManager>();

        _panelHost = new GameObject("HierarchyPanelHost");
        _panel = _panelHost.AddComponent<HierarchyPanelUI>();
        _panel.Build(_canvasGo.transform);
    }

    [TearDown]
    public void TearDown()
    {
        if (_panelHost != null) Object.DestroyImmediate(_panelHost);
        if (_selectionHost != null) Object.DestroyImmediate(_selectionHost);
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        GroupManager.Clear();
        ProjectWindows.Clear();
    }

    private KitchenElement MakeElement(string name)
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 400, 18), name, Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private Transform Panel => _canvasGo.transform.Find("HierarchyPanel");

    private Transform Content => Panel.Find("HierViewport/HierContent");

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

    private void ClickRow(string label)
    {
        foreach (Transform row in Content)
            if (LabelOf(row) == label)
            {
                row.Find("Main").GetComponent<Button>().onClick.Invoke();
                return;
            }
        Assert.Fail("в дереве нет строки «" + label + "»");
    }

    private Transform Row(string label)
    {
        foreach (Transform row in Content)
            if (LabelOf(row) == label) return row;
        Assert.Fail("в дереве нет строки «" + label + "»");
        return null!;
    }

    private TMP_InputField Search => Panel.Find("HierSearch").GetComponent<TMP_InputField>();

    private TMP_Dropdown MoveTo => Panel.Find("HierMoveTo").GetComponent<TMP_Dropdown>();

    [UnityTest]
    public IEnumerator RowClick_SelectsTheElementInTheScene()
    {
        var shelf = MakeElement("Полка");
        MakeElement("Столешница");
        _panel.SetVisible(true);
        yield return null;

        ClickRow(shelf.PartName);

        Assert.IsTrue(_selection.IsSelected(shelf),
            "строка дерева — это способ выделить деталь, которую в сцене не видно "
            + "или не поймать мышью");
    }

    [UnityTest]
    public IEnumerator GroupRowClick_SelectsEveryMemberOfTheGroup()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        var group = GroupManager.Link(new List<KitchenElement> { a, b })!;
        GroupManager.Rename(group, "Модуль");
        yield return null;

        ClickRow("Модуль (2)");

        Assert.IsTrue(_selection.IsSelected(a));
        Assert.IsTrue(_selection.IsSelected(b),
            "клик по группе выделяет ВСЕХ её членов: иначе перетащить модуль "
            + "целиком нельзя");
    }

    [UnityTest]
    public IEnumerator SceneSelection_ExpandsTheGroupOfTheSelectedElement()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        var group = GroupManager.Link(new List<KitchenElement> { a, b })!;
        GroupManager.Rename(group, "Модуль");
        yield return null;

        Row("Модуль (2)").Find("Fold").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assume.That(RowLabels(), Has.No.Member("A"), "группа свёрнута");

        _selection.Select(a);
        yield return null;

        Assert.Contains("A", RowLabels(),
            "выделение в сцене обязано раскрыть группу: подсвечивать строку, "
            + "которой не видно, бессмысленно");
        Assert.AreEqual(1, _selection.SelectedElements.Count);
    }

    [UnityTest]
    public IEnumerator AddGroupButton_CreatesAnEmptyGroup_AndTheTreeShowsIt()
    {
        yield return null;

        Panel.Find("HierAddGroup").GetComponent<Button>().onClick.Invoke();
        yield return null;

        var groups = new List<LinkGroup>(GroupManager.AllGroups());
        Assert.AreEqual(1, groups.Count);
        Assert.Contains(groups[0].name + " (0)", RowLabels(),
            "новая группа появляется в дереве сама — панель обновляется по событию "
            + "GroupManager.Changed, а не по следующему клику");
    }

    [UnityTest]
    public IEnumerator GroupMenuButton_EmptyGroup_DissolvesItRightAway()
    {
        GroupManager.Create("Пустая");
        yield return null;

        Row("Пустая (0)").Find("GroupMenu").GetComponent<Button>().onClick.Invoke();
        yield return null;

        Assert.IsEmpty(new List<LinkGroup>(GroupManager.AllGroups()),
            "у пустой группы терять нечего, поэтому она распускается сразу; "
            + "у непустой роспуск идёт только через подписанные кнопки меню — "
            + "в один клик без подтверждения теряется структура сцены");
    }

    [UnityTest]
    public IEnumerator Search_ShowsMatches_KeepsTheirGroup_AndIgnoresCollapse()
    {
        var shelf = MakeElement("Полка верхняя");
        var side = MakeElement("Боковина");
        string needle = shelf.PartName.Substring(0, 4).ToLowerInvariant();
        var group = GroupManager.Link(new List<KitchenElement> { shelf, side })!;
        GroupManager.Rename(group, "Модуль");
        yield return null;

        Row("Модуль (2)").Find("Fold").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assume.That(RowLabels(), Has.No.Member(shelf.PartName), "группа свёрнута");

        Search.text = needle;
        yield return null;

        var labels = RowLabels();
        Assert.Contains(shelf.PartName, labels,
            "совпадение внутри свёрнутой группы обязано быть видно: иначе поиск "
            + "молча ничего не находит");
        Assert.Contains("Модуль (2)", labels,
            "группа остаётся в списке, если совпал кто-то из её членов — без неё "
            + "непонятно, где найденная деталь лежит");
        Assert.That(labels, Has.No.Member(side.PartName));
        Assert.IsFalse(Panel.Find("HierSearch/HierSearchHint").gameObject.activeSelf,
            "подсказка «Поиск…» прячется, как только в поле что-то есть");
    }

    [UnityTest]
    public IEnumerator MoveToDropdown_MovesTheCurrentSelection_AndResetsItselfSilently()
    {
        var a = MakeElement("A");
        var target = GroupManager.Create("Цель");
        yield return null;

        _selection.Select(a);
        yield return null;
        MoveTo.value = 2;
        yield return null;

        Assert.AreEqual(target.id, a.GroupId,
            "индекс 2 — первая группа списка: 0 занят плейсхолдером «Переместить в…», "
            + "1 — «вне групп»");
        Assert.AreEqual(0, MoveTo.value,
            "список возвращается на плейсхолдер: он команда, а не состояние");
        Assert.AreEqual(1, new List<LinkGroup>(GroupManager.AllGroups()).Count,
            "программный сброс не должен снова дёрнуть обработчик — иначе он "
            + "создал бы лишнюю группу или перенёс выделение второй раз");
    }

    [UnityTest]
    public IEnumerator MoveToDropdown_UngroupedEntry_TakesTheElementOutOfItsGroup()
    {
        var a = MakeElement("A");
        var b = MakeElement("B");
        var group = GroupManager.Link(new List<KitchenElement> { a, b })!;
        Assume.That(a.GroupId, Is.EqualTo(group.id), "Link требует минимум двух участников");
        yield return null;

        _selection.Select(a);
        yield return null;
        MoveTo.value = 1;
        yield return null;

        Assert.AreEqual(0, a.GroupId, "пункт «— вне групп —» вынимает деталь из группы");
    }

    [UnityTest]
    public IEnumerator MoveToDropdown_LastEntry_CreatesANewGroupForTheSelection()
    {
        var a = MakeElement("A");
        GroupManager.Create("Уже есть");
        yield return null;

        _selection.Select(a);
        yield return null;
        MoveTo.value = MoveTo.options.Count - 1;
        yield return null;

        var groups = new List<LinkGroup>(GroupManager.AllGroups());
        Assert.AreEqual(2, groups.Count, "последний пункт списка — «+ Новая группа»");
        Assert.AreNotEqual(0, a.GroupId, "выделение переезжает в только что созданную группу");
    }

    [UnityTest]
    public IEnumerator Polling_NoticesARenameThatRaisedNoEvent()
    {
        var a = MakeElement("Полка");
        yield return new WaitForSecondsRealtime(0.7f);
        Assume.That(RowLabels(), Has.Member(a.PartName), "созданная деталь попадает в дерево");

        a.PartName = "Bokovina";
        yield return new WaitForSecondsRealtime(0.7f);

        Assert.Contains("Bokovina", RowLabels(),
            "переименование из MCP, undo или загрузки проекта не поднимает событий: "
            + "панель ловит его дешёвым поллингом отпечатка сцены, иначе дерево "
            + "показывает старые имена, пока по нему не щёлкнут");
    }

    [UnityTest]
    public IEnumerator ResizeHandle_IsTheLastChild_SoTheBottomStripResizes()
    {
        yield return null;

        var handle = Panel.Find("ResizeHandle");
        Assert.IsNotNull(handle);
        Assert.AreEqual(Panel.childCount - 1, handle.GetSiblingIndex(),
            "хэндл ресайза добавляется ПОСЛЕ скролл-зоны: иначе нижние 10 px окна "
            + "ловят клики по строкам списка, а не протяжку края");
    }

    [UnityTest]
    public IEnumerator AddGroupButton_KeepsItsDistanceFromTheCloseButton()
    {
        yield return null;

        var add = (RectTransform)Panel.Find("HierAddGroup");
        var close = (RectTransform)Panel.Find("CloseBtn");
        float closeLeftEdge = Mathf.Abs(close.anchoredPosition.x) + close.sizeDelta.x;
        float addRightEdge = Mathf.Abs(add.anchoredPosition.x);

        Assert.Greater(addRightEdge, closeLeftEdge,
            "«+ Группа» отодвинута от кнопки закрытия: создание и закрытие не должны "
            + "соседствовать (правило 3 UI-GUIDELINES о промахе мимо кнопки)");
    }
}
