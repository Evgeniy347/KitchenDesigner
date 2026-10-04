using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// D1: «Масштаб интерфейса» (Авто / 90–150 %) — настройка ПРОГРАММЫ: меняет масштаб каждой
// канвы, собранной UIFactory.CreateCanvas, сразу, без перезапуска и без отметки в проекте.
public class UiScaleSettingTests
{
    private int _before;
    private GameObject? _settingsCanvas;
    private Canvas? _appCanvas;

    [SetUp]
    public void SetUp()
    {
        _before = UiScalePreference.Percent;
        UiScalePreference.Choose(UiScale.AutoPercent);

        _settingsCanvas = new GameObject("TestCanvas");
        var canvas = _settingsCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _settingsCanvas.AddComponent<CanvasScaler>();
        _settingsCanvas.AddComponent<GraphicRaycaster>();
        var settings = _settingsCanvas.AddComponent<SettingsPanelUI>();
        settings.Build(canvas.transform);
        settings.SetVisible(true);

        _appCanvas = UIFactory.CreateCanvas("AppCanvas");
    }

    [TearDown]
    public void TearDown()
    {
        UiScalePreference.Choose(_before);
        EditModeManager.Reset();
        if (_settingsCanvas != null) Object.DestroyImmediate(_settingsCanvas);
        if (_appCanvas != null) Object.DestroyImmediate(_appCanvas.gameObject);
    }

    private TMP_Dropdown ScaleDropdown() =>
        _settingsCanvas!.GetComponentsInChildren<TMP_Dropdown>(true)
            .Single(d => d.name == "Dd_" + SettingsGeneralTab.UiScaleRowId);

    [Test]
    public void UiScaleRow_StartsAtAuto_AndListsTheRange()
    {
        var dd = ScaleDropdown();
        Assert.AreEqual(0, dd.value, "по умолчанию — «Авто»");
        Assert.AreEqual(Loc.T("settings.project.uiScaleAuto"), dd.options[0].text);
        Assert.AreEqual(UiScale.ChoicePercents.Count, dd.options.Count);
        Assert.AreEqual(SettingsGeneralTab.UiScaleChoiceLabel(UiScale.MaxPercent), dd.options.Last().text,
            "проценты — через NumberFormat, с неразрывным пробелом перед «%»");
    }

    [Test]
    public void ChoosingAPercent_RescalesEveryAppCanvas_AtOnce()
    {
        var fit = _appCanvas!.GetComponent<UiScaleFit>();
        Assert.IsNotNull(fit, "CreateCanvas обязан вешать UiScaleFit — иначе настройка ни на что не влияет");
        fit!.EmulatedScreen = new Vector2(1920, 1080);
        fit.Apply();
        float auto = fit.AppliedScaleFactor;

        var dd = ScaleDropdown();
        int at125 = UiScale.ChoiceIndex(125);
        Assert.Greater(at125, 0, "в списке есть 125 %");
        dd.value = at125;
        fit.Apply();

        Assert.AreEqual(125, UiScalePreference.Percent, "выбор в списке и есть настройка");
        Assert.AreEqual(auto * 1.25f, fit.AppliedScaleFactor, 1e-4f,
            "канва перемасштабирована сразу: «Масштаб интерфейса» — множитель поверх автоматического");
    }

    [Test]
    public void AppCanvas_OnTheBaseLaptopScreen_NeverScalesBelowTheFloor()
    {
        var fit = _appCanvas!.GetComponent<UiScaleFit>()!;
        fit.EmulatedScreen = new Vector2(1366, 768);
        fit.Apply();
        float canvasHeight = Screen.height / fit.AppliedScaleFactor;
        Assert.AreEqual(945.2f, canvasHeight, 0.5f,
            "1366×768 даёт канву 945 реф. px (масштаб 0,8125), а не 1080 (0,71): тело 16 → 13 px (D1)");
    }
}
