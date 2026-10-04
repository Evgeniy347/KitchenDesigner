using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Футер «Настроек» (D5/D6): «Сбросить раздел» слева у страниц, где есть заводские значения, и тихая
/// строка «Изменения применяются сразу» справа; кнопки «Закрыть» нет. Сброс страницы — ОДИН шаг отмены,
/// и он меняет и настройки, и виджеты на экране. Страница «Свет» проверена в <c>SettingsLightResetTests</c>;
/// здесь — «Управление» и «Фоторежим», которые «По умолчанию» не имели вовсе.
/// </summary>
public class SettingsFooterTests
{
    private ProjectLoadStateGuard? _globals;
    private GameObject? _canvasGo;
    private SettingsPanelUI? _ui;

    [SetUp]
    public void SetUp()
    {
        _globals = ProjectLoadStateGuard.Capture();
        Loc.SetLanguage("ru");
        KitchenSettings.Instance.ResetToDefaults();
        CommandStack.Clear();
        _canvasGo = UiTestCanvas.Create("FooterCanvas");
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

    private Transform Footer => _canvasGo!.transform.Find(SettingsWindowPaths.Footer);

    private Button ResetButton => Footer.Find(SettingsPanelUI.ResetButtonName).GetComponent<Button>();

    private void Open(string id) => _ui!.OpenTab(SettingsPanelUI.PageOrder.ToList().IndexOf(id));

    private Transform Page(string id) => _canvasGo!.transform.Find(SettingsWindowPaths.Page(id));

    [Test]
    public void TheFooter_HasTheResetLinkAtTheLeft_AndTheQuietNoteAtTheRight()
    {
        Open(SettingsPanelUI.ControlId);
        var panel = (RectTransform)_canvasGo!.transform.Find(SettingsWindowPaths.Panel);
        var reset = (RectTransform)ResetButton.transform;
        var note = (RectTransform)Footer.Find(SettingsPanelUI.AppliedNoteNode);

        Assert.AreEqual(Loc.T("settings.footer.applied"), note.GetComponent<TMP_Text>().text.Replace("​", ""));
        Assert.Less(panel.InverseTransformPoint(reset.position).x, panel.InverseTransformPoint(note.position).x,
            "«Сбросить раздел» слева, пояснение справа");
        var corners = new Vector3[4];
        reset.GetWorldCorners(corners);
        float resetRight = panel.InverseTransformPoint(corners[2]).x;
        note.GetWorldCorners(corners);
        Assert.Less(resetRight, panel.InverseTransformPoint(corners[0]).x, "они не наезжают друг на друга");
    }

    [Test]
    public void TheFooter_HasNoCloseButton_AndNoPrimaryAction()
    {
        string close = Loc.T("common.close");
        var buttons = Footer.GetComponentsInChildren<Button>(true);

        Assert.IsFalse(buttons.Any(b => b.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text == close)),
            "«Закрыть» дублирует × (D5)");
        Assert.AreEqual(1, buttons.Length, "в футере одна кнопка — сброс раздела; применять нечего, всё применяется сразу");
    }

    [Test]
    public void TheResetLink_ShowsOnlyOnPagesWithFactoryDefaults()
    {
        var withReset = new[] { SettingsPanelUI.ControlId, SettingsPanelUI.LightId, SettingsPanelUI.PhotoId };
        foreach (var id in SettingsPanelUI.PageOrder)
        {
            Open(id);
            Assert.AreEqual(withReset.Contains(id), ResetButton.gameObject.activeSelf,
                $"«Сбросить раздел» на странице «{id}»");
        }
    }

    [Test]
    public void ControlReset_PutsSlidersTogglesAndHotkeysBack_InOneUndoStep()
    {
        var s = KitchenSettings.Instance;
        s.MouseSensitivity = 2.4f;
        s.WasdSpeed = 0.5f;
        s.MouseInvertX = true;
        s.KeyBindings.SetPrimaryBinding(InputAction.SaveProject, KeyBindingDefaults.PrimaryOf(InputAction.DuplicateSelected));
        _ui!.SyncFromSettings();
        Open(SettingsPanelUI.ControlId);
        int before = CommandStack.UndoCount;

        ResetButton.onClick.Invoke();

        Assert.AreEqual(1f, s.MouseSensitivity, 0.001f);
        Assert.AreEqual(1f, s.WasdSpeed, 0.001f);
        Assert.IsFalse(s.MouseInvertX);
        Assert.IsTrue(s.KeyBindings.IsDefault(InputAction.SaveProject), "горячие клавиши вернулись к заводским");
        Assert.AreEqual(before + 1, CommandStack.UndoCount, "сброс страницы — один шаг отмены");
        var slider = Page(SettingsPanelUI.ControlId).Find(SettingsWindowPaths.Slider(Loc.T("settings.control.mouseSensitivity")))
            .GetComponent<Slider>();
        Assert.AreEqual(1f, slider.value, 0.001f, "ползунок на экране показывает заводское значение");

        CommandStack.Undo();

        Assert.AreEqual(2.4f, s.MouseSensitivity, 0.001f, "один Ctrl+Z возвращает всё");
        Assert.IsTrue(s.MouseInvertX);
        Assert.IsFalse(s.KeyBindings.IsDefault(InputAction.SaveProject));
    }

    [Test]
    public void ResettingTheHotkeysFromTheSectionLink_LeavesTheSlidersAlone()
    {
        var s = KitchenSettings.Instance;
        s.MouseSensitivity = 2.4f;
        s.KeyBindings.SetPrimaryBinding(InputAction.SaveProject, KeyBindingDefaults.PrimaryOf(InputAction.DuplicateSelected));
        _ui!.SyncFromSettings();
        Open(SettingsPanelUI.ControlId);
        var link = Page(SettingsPanelUI.ControlId).GetComponentsInChildren<Button>(true)
            .Single(b => b.name.EndsWith("_Action"));
        Assert.AreEqual(Loc.T("settings.control.resetHotkeys"), link.GetComponentInChildren<TMP_Text>().text.Replace("​", ""));

        link.onClick.Invoke();

        Assert.IsTrue(s.KeyBindings.IsDefault(InputAction.SaveProject));
        Assert.AreEqual(2.4f, s.MouseSensitivity, 0.001f, "«Сбросить все» у горячих клавиш не трогает ползунки");
    }

    [Test]
    public void PhotoReset_RestoresThePhotoPage_ButNotTheLightLook()
    {
        var s = KitchenSettings.Instance;
        var light = new PhotoLightLook(PhotoLightLook.Defaults.AmbientPct + 20, PhotoLightLook.Defaults.FloorBouncePct,
            PhotoLightLook.Defaults.AmbientSkyPct, PhotoLightLook.Defaults.AmbientEquatorPct,
            PhotoLightLook.Defaults.BounceMaxPct, PhotoLightLook.Defaults.Tonemap, PhotoLightLook.Defaults.ExposurePct,
            PhotoLightLook.Defaults.ContrastPct, PhotoLightLook.Defaults.SaturationPct, PhotoLightLook.Defaults.BloomPct,
            PhotoLightLook.Defaults.BloomThresholdPct, PhotoLightLook.Defaults.BloomClampPct,
            PhotoLightLook.Defaults.VignettePct, PhotoLightLook.Defaults.SunShadowStrengthPct,
            PhotoLightLook.Defaults.ShadowDistanceM, PhotoLightLook.Defaults.LampShadows);
        s.ApplyLightLook(light);
        s.PhotoSupersampling = false;
        s.PhotoSSGI = true;
        s.PhotoRenderScalePct = KitchenSettings.PHOTO_RENDER_SCALE_MIN_PCT;
        _ui!.SyncFromSettings();
        Open(SettingsPanelUI.PhotoId);
        int before = CommandStack.UndoCount;

        ResetButton.onClick.Invoke();

        Assert.IsTrue(s.PhotoSupersampling);
        Assert.IsFalse(s.PhotoSSGI);
        Assert.AreEqual(KitchenSettings.PHOTO_RENDER_SCALE_DEFAULT_PCT, s.PhotoRenderScalePct);
        Assert.AreEqual(light, s.CaptureLightLook(), "страница «Свет» сбрасывается своей кнопкой, а не этой");
        Assert.AreEqual(before + 1, CommandStack.UndoCount);
        var toggle = Page(SettingsPanelUI.PhotoId).Find(SettingsWindowPaths.Switch(Loc.T("settings.photo.supersampling")))
            .GetComponent<Toggle>();
        Assert.IsTrue(toggle.isOn, "переключатель на экране показывает заводское значение");
    }

    [Test]
    public void ResettingAnUntouchedPage_PushesNoUndoStep()
    {
        Open(SettingsPanelUI.ControlId);

        ResetButton.onClick.Invoke();

        Assert.AreEqual(0, CommandStack.UndoCount, "сбрасывать нечего — шага отмены, который ничего не делает, нет");
    }

    [Test]
    public void ThePhotoQuality_ClickOnAnyPreset_SetsItEvenWhenTheCurrentOneIsCustom()
    {
        var s = KitchenSettings.Instance;
        s.PhotoSupersampling = false;
        s.PhotoQuality = PhotoQualityPresetTable.Detect(s);
        Assume.That(PhotoQualityPresetTable.Detect(s), Is.EqualTo(PhotoQualityPreset.Custom), "предпосылка: свои настройки");
        _ui!.SyncFromSettings();
        var quality = Page(SettingsPanelUI.PhotoId).Find("Row_" + SettingsPhotoTab.QualityId)
            .GetComponentInChildren<SegmentedControl>(true);

        quality.Segments[0].onClick.Invoke();

        Assert.AreEqual(PhotoQualityPreset.Low, PhotoQualityPresetTable.Detect(s),
            "клик по «Низкое» из состояния «Свои настройки» обязан сработать: сегмент 0 раньше был «выбран» по умолчанию");
    }

    [Test]
    public void ThePhotoQuality_ShowsNoSelectedSegment_AndTheCustomCaption_ForCustomSettings()
    {
        var s = KitchenSettings.Instance;
        s.PhotoSupersampling = false;
        s.PhotoSoftShadows = true;
        s.PhotoShadowMapPx = KitchenSettings.PHOTO_SHADOWMAP_MAX_PX;
        _ui!.SyncFromSettings();
        var row = Page(SettingsPanelUI.PhotoId).Find("Row_" + SettingsPhotoTab.QualityId);
        Assume.That(PhotoQualityPresetTable.Detect(s), Is.EqualTo(PhotoQualityPreset.Custom));

        var caption = row.Find(SettingsPhotoTab.CustomCaptionNode);
        Assert.IsTrue(caption.gameObject.activeSelf, "рядом со списком сказано «Свои настройки»");
        foreach (var segment in row.GetComponentInChildren<SegmentedControl>(true).Segments)
            Assert.AreEqual(UIStyle.Transparent, ((Image)segment.targetGraphic).color, "ни один сегмент не залит");
    }
}
