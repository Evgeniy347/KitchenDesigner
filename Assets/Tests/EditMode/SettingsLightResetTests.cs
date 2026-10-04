using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// «Сбросить раздел» в футере на странице «Свет» (бывшая кнопка «По умолчанию» с верха страницы):
/// сбрасывает ВСЕ настройки страницы одним шагом отмены и гаснет, когда сбрасывать нечего. Гаснуть
/// и загораться она обязана от ЛЮБОЙ правки страницы — поэтому набор проверяется не списком полей,
/// а обходом настоящих ползунков, тумблеров и списков страницы: добавленный на страницу виджет,
/// которого нет в <see cref="PhotoLightLook"/>, не включит кнопку, и этот обход его называет.
/// </summary>
public class SettingsLightResetTests
{
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
        _ui.OpenTab(SettingsPanelUI.PageOrder.ToList().IndexOf(SettingsPanelUI.LightId));
    }

    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear();
        EditModeManager.Reset();
        UiTestCanvas.Release(_canvasGo);
        _globals?.Restore();
    }

    private Transform Page => _canvasGo!.transform.Find(SettingsWindowPaths.Page(SettingsPanelUI.LightId));

    private Button ResetButton =>
        _canvasGo!.transform.Find(SettingsWindowPaths.Footer + "/" + SettingsPanelUI.ResetButtonName)
            .GetComponent<Button>();

    private void Refresh() => _ui!.SyncFooter();

    private static PhotoLightLook Moved()
    {
        var d = PhotoLightLook.Defaults;
        return new PhotoLightLook(d.AmbientPct + 10, d.FloorBouncePct, d.AmbientSkyPct, d.AmbientEquatorPct,
            d.BounceMaxPct, d.Tonemap, d.ExposurePct + 10, d.ContrastPct, d.SaturationPct, d.BloomPct,
            d.BloomThresholdPct, d.BloomClampPct, d.VignettePct, d.SunShadowStrengthPct,
            d.ShadowDistanceM, !d.LampShadows);
    }

    [Test]
    public void Button_LivesInTheFooter_WithTheResetSectionCaption_AndNotOnThePage()
    {
        var caption = ResetButton.GetComponentInChildren<TMP_Text>().text;

        Assert.AreEqual(Loc.T("settings.reset.section"), caption.Replace("​", ""));
        Assert.IsTrue(ResetButton.gameObject.activeInHierarchy, "у страницы «Свет» сброс есть");
        Assert.IsEmpty(Page.GetComponentsInChildren<Button>(true),
            "кнопка «По умолчанию» уехала с верха страницы в футер: на самой странице кнопок больше нет");
    }

    [Test]
    public void Button_IsDisabled_WhenThePageIsAtDefaults()
    {
        Refresh();

        Assert.IsFalse(ResetButton.interactable,
            "на свежем проекте сбрасывать нечего: живая кнопка обещала бы действие, которого нет");
    }

    [Test]
    public void Button_IsEnabled_AfterAnySliderToggleOrDropdownOfThePageChanges()
    {
        var widgets = new List<(string name, System.Action change)>();
        foreach (var slider in Page.GetComponentsInChildren<Slider>(true))
        {
            var s = slider;
            widgets.Add((s.name, () => s.value = s.value < s.maxValue ? s.maxValue : s.minValue));
        }
        foreach (var toggle in Page.GetComponentsInChildren<Toggle>(true))
        {
            if (toggle.GetComponentInParent<TMP_Dropdown>(true) != null) continue;
            var t = toggle;
            widgets.Add((t.name, () => t.isOn = !t.isOn));
        }
        foreach (var dropdown in Page.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            var d = dropdown;
            widgets.Add((d.name, () => d.value = d.value < d.options.Count - 1 ? d.options.Count - 1 : 0));
        }
        Assert.GreaterOrEqual(widgets.Count, 15,
            "обход не нашёл виджетов страницы «Свет» — он бы зеленел впустую");

        var silent = new List<string>();
        foreach (var (name, change) in widgets)
        {
            KitchenSettings.Instance.ApplyLightLook(PhotoLightLook.Defaults);
            _ui!.SyncFromSettings();
            Refresh();
            Assume.That(ResetButton.interactable, Is.False, "перед правкой страница обязана быть заводской");

            change();
            Refresh();
            if (!ResetButton.interactable) silent.Add(name);
        }

        Assert.IsEmpty(silent,
            "правка этих виджетов не включила сброс: их настройки не входят в "
            + "PhotoLightLook, и кнопка не сбросит их: " + string.Join(", ", silent));
    }

    [Test]
    public void Button_GoesDarkAgain_WhenTheSettingsReturnToDefaults()
    {
        KitchenSettings.Instance.ApplyLightLook(Moved());
        Refresh();
        Assume.That(ResetButton.interactable, Is.True);

        KitchenSettings.Instance.ApplyLightLook(PhotoLightLook.Defaults);
        Refresh();

        Assert.IsFalse(ResetButton.interactable,
            "пара к проверке включения: вернули заводские значения — кнопка снова серая");
    }

    [Test]
    public void Click_ResetsTheWholePage_InOneUndoStep_AndUndoBringsItBack()
    {
        var settings = KitchenSettings.Instance;
        var moved = Moved();
        settings.ApplyLightLook(moved);
        _ui!.SyncFromSettings();
        Refresh();
        int before = CommandStack.UndoCount;

        ResetButton.onClick.Invoke();

        Assert.AreEqual(PhotoLightLook.Defaults, settings.CaptureLightLook(), "настройки сброшены");
        Assert.AreEqual(before + 1, CommandStack.UndoCount,
            "сброс всей страницы — ОДИН шаг отмены, а не по шагу на каждый ползунок");
        Refresh();
        Assert.IsFalse(ResetButton.interactable, "после сброса кнопка гаснет");

        CommandStack.Undo();

        Assert.AreEqual(moved, settings.CaptureLightLook(),
            "один Ctrl+Z возвращает все настройки страницы такими, какими они были");
        Refresh();
        Assert.IsTrue(ResetButton.interactable, "после отмены сбрасывать снова есть что");
    }

    [Test]
    public void Click_UpdatesTheSlidersOnScreen_NotOnlyTheSettings()
    {
        var settings = KitchenSettings.Instance;
        settings.PhotoAmbientPct = 250;
        _ui!.SyncFromSettings();
        string key = Loc.T("settings.light.ambient");
        var ambient = Page.Find(SettingsWindowPaths.Slider(key)).GetComponent<Slider>();
        Assume.That(ambient.value, Is.EqualTo(250f));

        Refresh();
        ResetButton.onClick.Invoke();

        Assert.AreEqual(KitchenSettings.PHOTO_AMBIENT_DEFAULT_PCT, ambient.value,
            "ползунок на экране обязан показать заводское значение, иначе окно врёт о настройках");
    }

    [Test]
    public void Button_IsHiddenOnPagesWithoutADefaultsAction()
    {
        _ui!.OpenTab(SettingsPanelUI.PageOrder.ToList().IndexOf(SettingsPanelUI.AboutId));

        Assert.IsFalse(ResetButton.gameObject.activeSelf,
            "у «О программе» сбрасывать нечего: кнопка, которая ничего не сбрасывает, — шум в футере");
    }
}
