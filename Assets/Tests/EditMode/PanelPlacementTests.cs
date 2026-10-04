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
        var ui = go.AddComponent<SpecificationPanelUI>();
        ui.Build(_canvasGo!.transform);

        var header = ui.Table.HeaderLabel(SpecificationRows.NameColumn);

        Assert.AreEqual(UIStyle.TextSecondary, header.color,
            "Шапка таблицы — вторичный цвет текста, а НЕ жёлтый: жёлтым в проекте помечено "
            + "«значение изменено», и один цвет обязан значить одно (правило 10)");
        Assert.AreNotEqual(UIStyle.HighlightChanged, header.color);
    }

    [Test]
    public void SpecificationPanel_Export_IsTheAccentedMainAction_AndThereIsNoCloseButtonNextToTheCross()
    {
        var go = new GameObject("Spec");
        go.transform.SetParent(_canvasGo!.transform);
        go.AddComponent<SpecificationPanelUI>().Build(_canvasGo!.transform);

        var panel = _canvasGo!.transform.Find("SpecPanel")!;
        var export = panel.Find("SpecPanelFooter/SpecExport")!.GetComponent<Image>();
        var copy = panel.Find("SpecPanelFooter/SpecCopy")!.GetComponent<Image>();

        Assert.AreEqual(UIStyle.Accent, export.color,
            "Экспорт CSV — главное действие окна спецификации, поэтому он один выделен "
            + "акцентным цветом");
        Assert.AreEqual(UIStyle.Surface, copy.color,
            "«Копировать» остаётся обычной кнопкой — иначе выделение перестаёт что-либо значить");
        Assert.IsNull(panel.Find("SpecClose"),
            "«Закрыть» рядом с × — две двери с одним смыслом (D5, NN/g): её нет");
    }
}
