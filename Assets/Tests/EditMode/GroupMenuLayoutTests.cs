using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

public class GroupMenuLayoutTests
{
    private GameObject? _canvasGo;
    private GameObject? _host;

    [SetUp]
    public void Setup()
    {
        _canvasGo = new GameObject("Canvas");
        _canvasGo!.AddComponent<Canvas>();

        _host = new GameObject("GroupMenuHost");
        _host!.transform.SetParent(_canvasGo!.transform);
        _host!.AddComponent<GroupMenuUI>().Build(_canvasGo!.transform);
    }

    [TearDown]
    public void Teardown()
    {
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    private Transform Panel()
    {
        var panel = _canvasGo!.transform.Find("GroupMenu");
        Assert.IsNotNull(panel, "панель меню группы не построена — тест бы зеленел впустую");
        return panel!;
    }

    [Test]
    public void GroupMenu_CloseButton_IsBuiltLast_SoItSitsOverTheDragStrip()
    {
        var panel = Panel();
        var close = panel.Find("CloseBtn");

        Assert.IsNotNull(close, "у окна обязана быть кнопка закрытия");
        Assert.AreEqual(panel.childCount - 1, close!.GetSiblingIndex(),
            "Полоса перетаскивания накрывает весь заголовок окна, а перекрывают её только "
            + "контролы, созданные ПОЗЖЕ — поэтому крестик строится последним, иначе по нему "
            + "нельзя было бы кликнуть, окно просто поехало бы за курсором");
    }

    [Test]
    public void GroupMenu_LinkPrompt_IsMoreCompactThanTheGroupSettings()
    {
        Assert.Less(GroupMenuUI.CompactLinkPromptSize.y, GroupMenuUI.GroupSettingsSize.y,
            "«Связать выделенные?» — это заголовок и одна кнопка, и окно под них заметно "
            + "ниже окна настроек группы: пустое место читалось бы как «тут что-то пропало»");
        Assert.Less(GroupMenuUI.CompactLinkPromptSize.x, GroupMenuUI.GroupSettingsSize.x);
    }
}
