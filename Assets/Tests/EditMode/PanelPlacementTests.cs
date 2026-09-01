using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

public class PanelPlacementTests
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
    public void ModuleEditBanner_TucksUnderTheTopToolbar_WithoutAGapBetweenThem()
    {
        float bannerTop = ModuleEditBannerUI.TuckedUnderTheTopToolbarY;
        float bannerBottom = bannerTop - ModuleEditBannerUI.BannerHeight;
        float toolbarBottom = -ToolbarUI.BarHeight;

        Assert.Greater(bannerTop, toolbarBottom,
            "Баннер заходит верхним краем под тулбар, чтобы между ними не зияла полоса фона");
        Assert.Less(bannerBottom, toolbarBottom,
            "но основной своей частью висит НИЖЕ тулбара — иначе его не видно");
    }

    [Test]
    public void SpecificationPanel_HeaderRow_UsesSecondaryText_NotTheChangedValueYellow()
    {
        var go = new GameObject("Spec");
        go.transform.SetParent(_canvasGo!.transform);
        go.AddComponent<SpecificationPanelUI>().Build(_canvasGo!.transform);

        var headers = _canvasGo!.transform.Find("SpecPanel/SpecHeaders")!.GetComponent<TMP_Text>();

        Assert.AreEqual(UIStyle.TextSecondary, headers.color,
            "Шапка таблицы — вторичный цвет текста, а НЕ жёлтый: жёлтым в проекте помечено "
            + "«значение изменено», и один цвет обязан значить одно (правило 10)");
        Assert.AreNotEqual(UIStyle.HighlightChanged, headers.color);
    }

    [Test]
    public void SpecificationPanel_Export_IsTheAccentedMainAction()
    {
        var go = new GameObject("Spec");
        go.transform.SetParent(_canvasGo!.transform);
        go.AddComponent<SpecificationPanelUI>().Build(_canvasGo!.transform);

        var panel = _canvasGo!.transform.Find("SpecPanel")!;
        var export = panel.Find("SpecExport")!.GetComponent<Image>();
        var close = panel.Find("SpecClose")!.GetComponent<Image>();

        Assert.AreEqual(UIStyle.Accent, export.color,
            "Экспорт CSV — главное действие окна спецификации, поэтому он один выделен "
            + "акцентным цветом");
        Assert.AreEqual(UIFactory.ButtonColor, close.color,
            "а «Закрыть» остаётся обычной кнопкой — иначе выделение перестаёт что-либо значить");
    }
}
