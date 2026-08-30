using System.Collections.Generic;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Два правила раскладки «Настроек», которые до сих пор жили комментариями и
/// потому ничем не проверялись.
///
/// Первое: строка окна называется по своей подписи, а подписи повторяются —
/// «Контур» есть у стен и у объектов, «Тени» у фоторежима и «Тени сцены» у
/// света. Имена объектов сцены обязаны оставаться уникальными: тесты и
/// снапшоты ищут строку через transform.Find, и вторая строка с тем же именем
/// просто не находится. Поэтому у таких строк есть отдельный ключ (id).
///
/// Второе: вкладок шесть, каждая ~93 px, и при обычном кегле «Управление» и
/// «О программе» ломаются на две строки.
/// </summary>
public class SettingsPanelRowNamingTests
{
    private Canvas? _canvas;
    private SettingsPanelUI? _ui;

    [SetUp]
    public void SetUp()
    {
        var go = new GameObject("TestCanvas");
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        _ui = _canvas.gameObject.AddComponent<SettingsPanelUI>();
        _ui.Build(_canvas.transform);
    }

    [TearDown]
    public void TearDown()
    {
        KitchenDesigner.Core.EditModeManager.Reset();
        if (_canvas != null) Object.DestroyImmediate(_canvas.gameObject);
    }

    [Test]
    public void EveryRow_HasAUniqueNameAmongItsSiblings()
    {
        var panel = _canvas!.transform.Find("SettingsPanel");
        Assume.That(panel, Is.Not.Null, "панель не построилась — проверять нечего");

        var duplicates = new List<string>();
        foreach (Transform page in panel!)
            CollectDuplicateChildNames(page, duplicates);

        Assert.IsEmpty(duplicates,
            "две строки с одинаковым именем — вторую уже не найти через transform.Find, "
            + "и тест на неё молча проверяет первую. Повторяющейся подписи нужен свой id: "
            + string.Join(", ", duplicates));
    }

    [Test]
    public void TabCaptions_FitOnOneLine()
    {
        var panel = _canvas!.transform.Find("SettingsPanel");
        Assume.That(panel, Is.Not.Null);

        var tooWide = new List<string>();
        for (int i = 0; ; i++)
        {
            var tab = panel!.Find($"Tab_{i}");
            if (tab == null) break;

            var caption = tab.GetComponentInChildren<TextMeshProUGUI>();
            var tabRect = tab.GetComponent<RectTransform>();
            if (caption == null || tabRect == null) continue;
            Assume.That(caption.font, Is.Not.Null, "без шрифта ширину текста не измерить");

            float needed = caption.GetPreferredValues(caption.text).x;
            if (needed > tabRect.sizeDelta.x)
                tooWide.Add($"{caption.text}: нужно {needed:F0} px, есть {tabRect.sizeDelta.x:F0} px");
        }

        Assert.IsEmpty(tooWide,
            "подпись вкладки шире самой вкладки переносится на вторую строку и обрезается "
            + "по высоте кнопки. Кегль вкладок (SettingsTabStrip.TabFontSize) подобран именно "
            + "под шесть вкладок: " + string.Join("; ", tooWide));
    }

    private static void CollectDuplicateChildNames(Transform parent, List<string> duplicates)
    {
        var seen = new HashSet<string>();
        foreach (Transform child in parent)
        {
            if (!seen.Add(child.name))
                duplicates.Add($"{parent.name}/{child.name}");
            CollectDuplicateChildNames(child, duplicates);
        }
    }
}
