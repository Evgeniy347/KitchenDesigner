using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class GroupMenuLayoutTests
{
    private GameObject? _canvasGo;
    private GameObject? _host;
    private GameObject? _selectionGo;
    private SelectionManager? _selectionBefore;
    private SelectionManager? _selection;
    private GroupMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        _canvasGo = new GameObject("Canvas");
        _canvasGo!.AddComponent<Canvas>();

        _selectionBefore = SelectionManager.Instance;
        _selectionGo = new GameObject("Selection");
        _selection = _selectionGo.AddComponent<SelectionManager>();
        SelectionManager.Instance = _selection;

        _host = new GameObject("GroupMenuHost");
        _host!.transform.SetParent(_canvasGo!.transform);
        _menu = _host!.AddComponent<GroupMenuUI>();
        _menu.Build(_canvasGo!.transform);
    }

    [TearDown]
    public void Teardown()
    {
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        if (_selectionGo != null) Object.DestroyImmediate(_selectionGo);
        SelectionManager.Instance = _selectionBefore;
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
    }

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

    private Transform Settings()
    {
        var panel = _canvasGo!.transform.Find(GroupMenuUI.SettingsPanelName);
        Assert.IsNotNull(panel, "панель меню группы не построена — тест бы зеленел впустую");
        return panel!;
    }

    private Transform LinkPrompt() => _canvasGo!.transform.Find(GroupMenuUI.LinkPanelName)!;

    private KitchenElement OpenOnAGroup(out LinkGroup group)
    {
        var a = MakeElement("Боковина");
        var b = MakeElement("Полка");
        group = GroupManager.Link(new List<KitchenElement> { a, b })!;
        GroupManager.Rename(group, "Модуль");
        _selection!.SelectOnly(GroupManager.MembersOf(group));
        _menu!.Open(a);
        return a;
    }

    [Test]
    public void GroupMenu_HasTheStandardChrome_WithAQuietCrossAndAFooter()
    {
        var panel = Settings();

        Assert.IsNotNull(panel.Find("CloseBtn"), "у окна обязана быть кнопка закрытия");
        Assert.IsNotNull(panel.Find(GroupMenuUI.SettingsPanelName + "Footer"), "действия группы живут в футере (D5, D7)");
    }

    [Test]
    public void GroupMenu_LinkPrompt_IsMoreCompactThanTheGroupSettings()
    {
        Assert.Less(LinkPrompt().GetComponent<RectTransform>().sizeDelta.y,
            Settings().GetComponent<RectTransform>().sizeDelta.y,
            "«Связать выделенные?» — это заголовок и одна кнопка, и окно под них заметно "
            + "ниже окна настроек группы: пустое место читалось бы как «тут что-то пропало»");
    }

    [Test]
    public void GroupMenu_Footer_DissolveOnTheLeft_DeleteWithContentsOnTheRight_NeverTouching()
    {
        var footer = Settings().Find(GroupMenuUI.SettingsPanelName + "Footer");
        var dissolve = (RectTransform)footer.Find(GroupMenuUI.UnlinkNode);
        var delete = (RectTransform)footer.Find(GroupMenuUI.DeleteAllNode);

        float dissolveRight = dissolve.anchoredPosition.x + dissolve.sizeDelta.x;
        float deleteLeft = ((RectTransform)Settings()).sizeDelta.x + delete.anchoredPosition.x - delete.sizeDelta.x;

        Assert.AreEqual(0f, dissolve.anchorMin.x, "«Расформировать» — слева");
        Assert.AreEqual(1f, delete.anchorMin.x, "«Удалить с содержимым» — справа");
        Assert.Greater(deleteLeft, dissolveRight + UIStyle.Space2 - 0.01f,
            "две кнопки футера не наезжают друг на друга при текущем языке");
        Assert.AreEqual(Loc.T("group.unlink"), dissolve.GetComponentInChildren<TMP_Text>().text);
        Assert.AreEqual(Loc.T("group.deleteAll"), delete.GetComponentInChildren<TMP_Text>().text);
    }

    [Test]
    public void OpenOnAGroupMember_ShowsTheSettings_AndHidesThePrompt()
    {
        OpenOnAGroup(out _);

        Assert.IsTrue(Settings().gameObject.activeSelf);
        Assert.IsFalse(LinkPrompt().gameObject.activeSelf);
        Assert.IsTrue(_menu!.IsOpen);
        Assert.AreEqual("Модуль", Settings().GetComponentInChildren<TMP_InputField>(true).text);
    }

    [Test]
    public void OpenOnTwoUngroupedSelected_ShowsTheLinkPrompt()
    {
        var a = MakeElement("А");
        var b = MakeElement("Б");
        _selection!.SelectOnly(new List<KitchenElement> { a, b });

        _menu!.Open(a);

        Assert.IsTrue(LinkPrompt().gameObject.activeSelf);
        Assert.IsFalse(Settings().gameObject.activeSelf);
        Assert.IsNotNull(LinkPrompt().Find(GroupMenuUI.LinkPanelName + "Footer/" + GroupMenuUI.LinkNode));
    }

    [Test]
    public void DeleteWithContents_NeedsTwoClicks_TheFirstOnlyArms()
    {
        var a = OpenOnAGroup(out _);
        var delete = Settings().Find(GroupMenuUI.SettingsPanelName + "Footer/" + GroupMenuUI.DeleteAllNode)
            .GetComponent<Button>();

        delete.onClick.Invoke();

        Assert.IsTrue(PartRegistry.GetAll().Contains(a), "первый клик взводит кнопку, ничего не удаляя (§3)");
        Assert.IsTrue(delete.GetComponent<ConfirmDeleteButton>().Armed);

        delete.onClick.Invoke();

        Assert.IsFalse(PartRegistry.GetAll().Contains(a), "второй клик удаляет вместе с содержимым");
        Assert.IsFalse(_menu!.IsOpen, "меню закрылось: группы больше нет");
        ConfirmDeleteButton.DisarmAll();
    }

    [Test]
    public void Dissolve_IsOneClick_BecauseItLosesNothingAndUndoes()
    {
        var a = OpenOnAGroup(out _);
        var dissolve = Settings().Find(GroupMenuUI.SettingsPanelName + "Footer/" + GroupMenuUI.UnlinkNode)
            .GetComponent<Button>();

        dissolve.onClick.Invoke();

        Assert.AreEqual(0, a.GroupId);
        Assert.IsTrue(PartRegistry.GetAll().Contains(a));
        CommandStack.Undo();
        Assert.IsNotNull(GroupManager.GroupOf(a), "«Расформировать» отменяется");
    }
}
