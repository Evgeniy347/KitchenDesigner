using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;

/// <summary>
/// Подписи вкладок «Настроек» получили кегль мельче стандартного: когда вкладок стало восемь,
/// полоса в 560 px перестала вмещать их по 14 px, и раскладка вместо того, чтобы расширить окно,
/// стала уменьшать кегль (11 px у «Строительство»). Правильная починка другая: окно стало шире
/// в полтора раза, а подпись вкладки — всегда <see cref="UIStyle.FontTab"/>, на любом языке.
/// Новая вкладка или язык с длинной подписью, которые не вмещаются, должны краснеть здесь, а не
/// мельчить шрифт у пользователя.
/// </summary>
public class SettingsTabStripGuardTests
{
    private const float WidthBeforeTheTabsOutgrewIt = 600f;

    private static readonly string[] Languages =
        { "ru", "en", "de", "es", "fr", "it", "pt", "ja", "zh-Hans", "ar-TN" };

    private GameObject? _canvasGo;

    [TearDown]
    public void TearDown()
    {
        Loc.SetLanguage("ru");
        UiTestCanvas.Release(_canvasGo);
    }

    private Transform BuildPanel(string language)
    {
        Loc.SetLanguage(language);
        _canvasGo = UiTestCanvas.Create("TabStripCanvas");
        _canvasGo.AddComponent<SettingsPanelUI>().Build(_canvasGo.transform);
        return _canvasGo.transform.Find("SettingsPanel");
    }

    private static List<Transform> TabButtons(Transform panel) =>
        panel.Cast<Transform>().Where(t => t.name.StartsWith("Tab_") && char.IsDigit(t.name[4])).ToList();

    [TestCaseSource(nameof(Languages))]
    public void EveryTabCaption_IsTheStandardFontSize_AndFitsItsTab(string language)
    {
        var panel = BuildPanel(language);
        var tabs = TabButtons(panel);
        Assume.That(tabs.Count, Is.EqualTo(8), "в окне восемь вкладок");

        var offenders = new List<string>();
        foreach (var tab in tabs)
        {
            var caption = tab.GetComponentInChildren<TMP_Text>();
            string text = caption.text.Replace("​", "");
            if (!Mathf.Approximately(caption.fontSize, UIStyle.FontTab))
                offenders.Add($"«{text}»: кегль {caption.fontSize:F1}, стандарт {UIStyle.FontTab}");

            float needed = caption.GetPreferredValues(caption.text).x + 2f * UIStyle.GapInner;
            float width = ((RectTransform)tab).rect.width;
            if (needed > width + 0.01f)
                offenders.Add($"«{text}»: нужно {needed:F0} px с отступами, вкладка {width:F0} px");
        }

        Assert.IsEmpty(offenders,
            $"язык {language}: подпись вкладки не мельчится и не вылезает за вкладку — расширяйте окно, "
            + "а не уменьшайте шрифт:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void SettingsWindow_IsOneAndAHalfTimesWiderThanBeforeTheTabsOutgrewIt()
    {
        var panel = BuildPanel("ru");

        Assert.AreEqual(WidthBeforeTheTabsOutgrewIt * 1.5f, ((RectTransform)panel).sizeDelta.x, 0.01f,
            "окно настроек шире на 50 %: восемь вкладок по 14 px в 600 px не помещались, " +
            "и их шрифт уменьшали");
    }
}
