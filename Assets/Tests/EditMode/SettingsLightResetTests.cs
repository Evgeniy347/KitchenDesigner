using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопка «По умолчанию» на вкладке «Свет»: сбрасывает ВСЕ настройки вкладки одним шагом
/// отмены и гаснет, когда сбрасывать нечего. Гаснуть и загораться она обязана от ЛЮБОЙ
/// правки вкладки — поэтому набор проверяется не списком полей, а обходом настоящих
/// ползунков и тумблеров страницы: добавленный на вкладку виджет, которого нет в
/// <see cref="PhotoLightLook"/>, не включит кнопку, и этот обход его называет.
/// </summary>
public class SettingsLightResetTests
{
    private const string PagePath = "SettingsPanel/SettingsPanelBody/SettingsPanelBodyContent/Tab_Light";

    private ProjectLoadStateGuard? _globals;
    private GameObject? _canvasGo;
    private SettingsPanelUI? _ui;

    [SetUp]
    public void SetUp()
    {
        _globals = ProjectLoadStateGuard.Capture();
        KitchenSettings.Instance.ResetToDefaults();
        CommandStack.Clear();

        _canvasGo = UiTestCanvas.Create("LightResetCanvas");
        _ui = _canvasGo.AddComponent<SettingsPanelUI>();
        _ui.Build(_canvasGo.transform);
        _ui.SetVisible(true);
    }

    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear();
        EditModeManager.Reset();
        UiTestCanvas.Release(_canvasGo);
        _globals?.Restore();
    }

    private Transform Page => _canvasGo!.transform.Find(PagePath);

    private Button DefaultsButton =>
        Page.Find("RowLightDefaults/" + SettingsLightTab.DefaultsButtonName).GetComponent<Button>();

    private void Refresh() => DefaultsButton.GetComponent<SettingsDefaultsButton>().Refresh();

    private static PhotoLightLook Moved()
    {
        var d = PhotoLightLook.Defaults;
        return new PhotoLightLook(d.AmbientPct + 10, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct,
            d.BounceMaxPct, d.Tonemap, d.ExposurePct + 10, d.ContrastPct, d.SaturationPct, d.BloomPct,
            d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct,
            d.ShadowDistanceM, !d.LampShadows);
    }

    [Test]
    public void Button_ExistsOnTheLightTab_WithTheDefaultsCaption()
    {
        var caption = DefaultsButton.GetComponentInChildren<TMPro.TMP_Text>().text;

        Assert.AreEqual(Loc.T("settings.light.resetDefaults"), caption.Replace("​", ""));
    }

    [Test]
    public void Button_IsDisabled_WhenTheTabIsAtDefaults()
    {
        Refresh();

        Assert.IsFalse(DefaultsButton.interactable,
            "на свежем проекте сбрасывать нечего: живая кнопка обещала бы действие, которого нет");
    }

    [Test]
    public void Button_IsEnabled_AfterAnySliderOrToggleOfTheTabChanges()
    {
        var widgets = new List<(string name, System.Action change)>();
        foreach (var slider in Page.GetComponentsInChildren<Slider>(true))
        {
            var s = slider;
            widgets.Add((s.name, () => s.value = s.value < s.maxValue ? s.maxValue : s.minValue));
        }
        foreach (var toggle in Page.GetComponentsInChildren<Toggle>(true))
        {
            var t = toggle;
            widgets.Add((t.name, () => t.isOn = !t.isOn));
        }
        Assert.GreaterOrEqual(widgets.Count, 15,
            "обход не нашёл виджетов вкладки «Свет» — он бы зеленел впустую");

        var silent = new List<string>();
        foreach (var (name, change) in widgets)
        {
            KitchenSettings.Instance.ApplyLightLook(PhotoLightLook.Defaults);
            _ui!.SyncFromSettings();
            Refresh();
            Assume.That(DefaultsButton.interactable, Is.False, "перед правкой вкладка обязана быть заводской");

            change();
            Refresh();
            if (!DefaultsButton.interactable) silent.Add(name);
        }

        Assert.IsEmpty(silent,
            "правка этих виджетов не включила «По умолчанию»: их настройки не входят в "
            + "PhotoLightLook, и кнопка не сбросит их: " + string.Join(", ", silent));
    }

    [Test]
    public void Button_GoesDarkAgain_WhenTheSettingsReturnToDefaults()
    {
        KitchenSettings.Instance.ApplyLightLook(Moved());
        Refresh();
        Assume.That(DefaultsButton.interactable, Is.True);

        KitchenSettings.Instance.ApplyLightLook(PhotoLightLook.Defaults);
        Refresh();

        Assert.IsFalse(DefaultsButton.interactable,
            "пара к проверке включения: вернули заводские значения — кнопка снова серая");
    }

    [Test]
    public void Click_ResetsTheWholeTab_InOneUndoStep_AndUndoBringsItBack()
    {
        var settings = KitchenSettings.Instance;
        var moved = Moved();
        settings.ApplyLightLook(moved);
        _ui!.SyncFromSettings();
        Refresh();
        int before = CommandStack.UndoCount;

        DefaultsButton.onClick.Invoke();

        Assert.AreEqual(PhotoLightLook.Defaults, settings.CaptureLightLook(), "настройки сброшены");
        Assert.AreEqual(before + 1, CommandStack.UndoCount,
            "сброс всей вкладки — ОДИН шаг отмены, а не по шагу на каждый ползунок");
        Refresh();
        Assert.IsFalse(DefaultsButton.interactable, "после сброса кнопка гаснет");

        CommandStack.Undo();

        Assert.AreEqual(moved, settings.CaptureLightLook(),
            "один Ctrl+Z возвращает все настройки вкладки такими, какими они были");
        Refresh();
        Assert.IsTrue(DefaultsButton.interactable, "после отмены сбрасывать снова есть что");
    }

    [Test]
    public void Click_UpdatesTheSlidersOnScreen_NotOnlyTheSettings()
    {
        var settings = KitchenSettings.Instance;
        settings.PhotoAmbientPct = 250;
        _ui!.SyncFromSettings();
        var ambient = Page.GetComponentsInChildren<Slider>(true)
            .First(s => s.name == "Sld_" + Loc.T("settings.light.ambient"));
        Assume.That(ambient.value, Is.EqualTo(250f));

        Refresh();
        DefaultsButton.onClick.Invoke();

        Assert.AreEqual(KitchenSettings.PHOTO_AMBIENT_DEFAULT_PCT, ambient.value,
            "ползунок на экране обязан показать заводское значение, иначе окно врёт о настройках");
    }
}
