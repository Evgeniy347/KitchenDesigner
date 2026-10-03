using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;

/// <summary>
/// Значение справа от ползунка в «Настройках» — ОДНА строка без переноса. «нейтральный» в колонке
/// шириной 64 px рвался на «нейтраль-ный», потому что подпись значения переносила слова. Обход идёт
/// по ВСЕМ подписям значений ползунков (узлы <c>Val_*</c>) на каждом языке и меряет не только то, что
/// показано сейчас, но и каждое имя тонмаппинга — значение, которое ползунок примет позже.
/// </summary>
public class SettingsSliderValueFitTests
{
    private static readonly string[] Languages = { "ru", "en", "de", "es", "fr", "it", "pt" };
    private static readonly string[] TonemapKeys =
        { "settings.light.tonemapNone", "settings.light.tonemapNeutral" };

    private ProjectLoadStateGuard? _globals;
    private GameObject? _canvasGo;

    [SetUp]
    public void SetUp() => _globals = ProjectLoadStateGuard.Capture();

    [TearDown]
    public void TearDown()
    {
        Loc.SetLanguage("ru");
        UiTestCanvas.Release(_canvasGo);
        _globals?.Restore();
    }

    private List<TMP_Text> ValueLabels()
    {
        _canvasGo = UiTestCanvas.Create("SliderValueCanvas");
        var ui = _canvasGo.AddComponent<SettingsPanelUI>();
        ui.Build(_canvasGo.transform);
        ui.SetVisible(true);
        return _canvasGo.GetComponentsInChildren<TMP_Text>(true).Where(t => t.name.StartsWith("Val_") && t.transform.parent.name.StartsWith("RowSld_")).ToList();
    }

    [TestCaseSource(nameof(Languages))]
    public void EverySliderValue_FitsOneLineWithoutWrapping(string language)
    {
        Loc.SetLanguage(language);
        var labels = ValueLabels();
        Assume.That(labels.Count, Is.GreaterThan(3), "предпосылка: ползунки построены");

        var offenders = new List<string>();
        foreach (var label in labels)
        {
            if (label.enableWordWrapping)
                offenders.Add($"{label.name}: перенос слов включён");
            float width = label.rectTransform.rect.width;
            bool isTonemap = label.name == "Val_" + Loc.T("settings.light.tonemap");
            var candidates = isTonemap ? TonemapKeys.Select(Loc.T).Append(label.text) : new[] { label.text };
            foreach (var text in candidates)
            {
                float needed = label.GetPreferredValues(text).x;
                if (needed > width + 0.01f)
                    offenders.Add($"{label.name}: «{text}» нужно {needed:F0} px, колонка {width:F0} px");
            }
        }

        Assert.IsEmpty(offenders.Distinct(),
            $"значение ползунка не помещается в одну строку (язык {language}): " + string.Join("\n", offenders.Distinct()));
    }
}
