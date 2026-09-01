using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

public class DayNightPanelLayoutTests
{
    private GameObject? _canvasGo;

    [SetUp]
    public void Setup()
    {
        _canvasGo = new GameObject("Canvas");
        _canvasGo!.AddComponent<Canvas>();
    }

    [TearDown]
    public void Teardown()
    {
        ProjectWindows.Clear();
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    [Test]
    public void DayNightPanel_ResetButton_DoesNotStickToTheBottomEdge()
    {
        var go = new GameObject("DayNight");
        go.transform.SetParent(_canvasGo!.transform);
        go.AddComponent<DayNightPanelUI>().Build(_canvasGo!.transform);

        var panel = (RectTransform)_canvasGo!.transform.Find("DayNightPanel")!;
        var reset = (RectTransform)panel.Find("DnReset")!;

        float panelBottom = -DayNightPanelUI.PanelHeight * 0.5f;
        float resetBottom = reset.anchoredPosition.y - reset.sizeDelta.y * 0.5f;

        Assert.AreEqual(DayNightPanelUI.PanelHeight, panel.sizeDelta.y,
            "высота панели — та самая, из которой считается запас снизу");
        Assert.GreaterOrEqual(resetBottom - panelBottom, UIStyle.GapInner,
            "Высота панели взята с запасом именно для того, чтобы «Сброс» не прилипал к "
            + "нижнему краю: контенту нужен отступ со всех сторон (правило 7 UI-GUIDELINES)");
    }
}
