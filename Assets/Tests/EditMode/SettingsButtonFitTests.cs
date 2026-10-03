using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Подпись кнопки — ОДНА строка, и вокруг неё остаётся внутренний отступ
/// <see cref="UIStyle.GapInner"/> с каждой стороны (docs/UI-GUIDELINES.md §8). «Обычный (текущий)»
/// переносился на вторую строку и заливал кнопку целиком: ширина текста была равна ширине
/// кнопки, отступа не было. Обход идёт по ВСЕМ кнопкам окна настроек, а не по той, что попала
/// на скриншот, и на каждом языке, где подписи длиннее русских.
///
/// Ячейки привязки клавиш (KbBtn_, KbClr_) вне обхода: их подпись — сочетание клавиш, длина которого
/// задана данными, и они сами подгоняют кегль под ширину (KeybindingCellLayout, свой сторож
/// KeybindingRowWidthGuardTests).
/// </summary>
public class SettingsButtonFitTests
{
    private static readonly string[] Languages = { "ru", "en", "de", "es", "fr", "it", "pt" };

    private ProjectLoadStateGuard? _globals;
    private GameObject? _canvasGo;

    [SetUp]
    public void SetUp() => _globals = ProjectLoadStateGuard.Capture();

    [TearDown]
    public void TearDown()
    {
        Loc.SetLanguage("ru");
        EditModeManager.Reset();
        UiTestCanvas.Release(_canvasGo);
        _globals?.Restore();
    }

    private SettingsPanelUI BuildPanel()
    {
        _canvasGo = UiTestCanvas.Create("ButtonFitCanvas");
        var ui = _canvasGo.AddComponent<SettingsPanelUI>();
        ui.Build(_canvasGo.transform);
        ui.SetVisible(true);
        return ui;
    }

    private IEnumerable<string> Offenders()
    {
        foreach (var button in _canvasGo!.GetComponentsInChildren<Button>(true))
        {
            var caption = button.GetComponentInChildren<TMP_Text>(true);
            if (caption == null) continue;
            string text = caption.text.Replace("​", "");
            if (text.Length <= 1) continue;

            if (IsKeyChordCell(button)) continue;

            var mark = button.transform.Find(PresetCurrentMark.MarkName);
            float lane = mark != null && mark.gameObject.activeSelf ? PresetCurrentMark.MarkLane : 0f;
            float width = ((RectTransform)button.transform).rect.width;
            float needed = caption.GetPreferredValues(caption.text).x + lane + 2f * UIStyle.GapInner;
            if (needed > width + 0.01f)
                yield return $"«{text}» ({button.name}): нужно {needed:F0} px с отступами, кнопка {width:F0} px";
        }
    }

    private static bool IsKeyChordCell(Button button) =>
        button.name.StartsWith("KbBtn_") || button.name.StartsWith("KbClr_");

    [TestCaseSource(nameof(Languages))]
    public void EveryButtonOfTheSettingsWindow_FitsItsCaptionOnOneLine_WithTheInnerPadding(string language)
    {
        Loc.SetLanguage(language);
        BuildPanel();

        var offenders = new List<string>();
        foreach (var mode in new[] { EditMode.Normal, EditMode.Room, EditMode.Photo })
        {
            EditModeManager.SetMode(mode);
            offenders.AddRange(Offenders());
        }

        Assert.IsEmpty(offenders.Distinct(),
            $"подпись кнопки не помещается в одну строку с отступом {UIStyle.GapInner} px (язык {language}): "
            + "кнопка шире текста на два внутренних отступа или текст короче. Режим 'текущий' "
            + "отмечается глифом, а не словом в скобках:\n" + string.Join("\n", offenders.Distinct()));
    }

    [Test]
    public void TheSweep_SeesTheViewPresetButtons_AndTheDetectorCatchesAnOverlongCaption()
    {
        BuildPanel();
        var preset = _canvasGo!.GetComponentsInChildren<Button>(true).First(b => b.name == "ViewPreset_0");
        var caption = preset.GetComponentInChildren<TMP_Text>();

        caption.text = new string('Ш', 40);

        Assert.IsNotEmpty(Offenders().Where(o => o.Contains("ViewPreset_0")),
            "сторож, который не краснеет на заведомо длинной подписи, зеленеет впустую");
    }
}
