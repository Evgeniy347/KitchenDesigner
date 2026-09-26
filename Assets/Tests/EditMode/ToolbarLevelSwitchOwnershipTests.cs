using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>M3 (обзор ui-mcp): PageUp/PageDown были заявлены двумя владельцами
/// разом — ToolbarUI читал их напрямую через InputMap, панель «Ошибки» тоже читала
/// их напрямую (raw KeyCode), и ни один не спрашивал другого. Один физический
/// клик листал этажи И одновременно двигал фокус по списку ошибок. Проверяется
/// связка: ToolbarUI обязан спросить <see cref="PageKeyOwnership"/> через заявку
/// открытой панели «Ошибки» ПЕРЕД тем, как трогать LevelSwitch.</summary>
public class ToolbarLevelSwitchOwnershipTests
{
    private GameObject? _panelHost;
    private ErrorPanelUI? _panel;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        PartRegistry.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        if (_panelHost != null) Object.DestroyImmediate(_panelHost);
        _panelHost = null;
        _panel = null;

        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        LogAssert.ignoreFailingMessages = false;
    }

    private ErrorPanelUI BuildPanel()
    {
        var canvasGo = new GameObject("Canvas");
        canvasGo.AddComponent<Canvas>();
        _panelHost = canvasGo;
        _panel = canvasGo.AddComponent<ErrorPanelUI>();
        _panel.Build(canvasGo.transform);
        return _panel;
    }

    [Test]
    public void NoErrorPanelBuilt_LevelSwitchOwnsTheKeys()
    {
        Assert.IsTrue(ToolbarUI.LevelSwitchOwnsPageKeys(),
            "без панели «Ошибки» в сцене PageUp/PageDown как и раньше листают этажи");
    }

    [Test]
    public void ErrorPanelClosed_LevelSwitchOwnsTheKeys()
    {
        var panel = BuildPanel();
        panel.SetVisible(false);

        Assert.IsTrue(ToolbarUI.LevelSwitchOwnsPageKeys(),
            "закрытая панель «Ошибки» не имеет права забирать клавишу у переключателя этажа");
    }

    [Test]
    public void ErrorPanelOpenWithNoIssues_LevelSwitchOwnsTheKeys()
    {
        var panel = BuildPanel();
        panel.SetVisible(true);
        Assume.That(panel.TotalIssueCount, Is.EqualTo(0), "предпосылка: пустая сцена без находок");

        Assert.IsTrue(ToolbarUI.LevelSwitchOwnsPageKeys(),
            "панели нечего листать — PageUp/PageDown обязаны остаться за переключателем этажа");
    }

    [Test]
    public void ErrorPanelOpenWithIssues_ErrorPanelOwnsTheKeys()
    {
        // Две доски одна на другой — простейшее столкновение (COL-01).
        ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Доска А", Vector3.zero);
        ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Доска Б", Vector3.zero);
        var panel = BuildPanel();
        panel.SetVisible(true);
        Assume.That(panel.TotalIssueCount, Is.GreaterThan(0),
            "предпосылка: две доски одна на другой обязаны дать хотя бы одну находку (COL-01)");

        Assert.IsFalse(ToolbarUI.LevelSwitchOwnsPageKeys(),
            "панель «Ошибки» открыта и есть что листать — один и тот же PageDown не имеет "
            + "права одновременно листать список И переключать этаж");
    }
}
