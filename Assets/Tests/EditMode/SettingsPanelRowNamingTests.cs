using System.Collections.Generic;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Правило раскладки «Настроек», которое жило комментарием и потому ничем не проверялось.
///
/// Строка окна называется по своей подписи, а подписи повторяются —
/// «Контур» есть у стен и у объектов, «Тени» у фоторежима и «Тени сцены» у
/// света. Имена объектов сцены обязаны оставаться уникальными: тесты и
/// снапшоты ищут строку через transform.Find, и вторая строка с тем же именем
/// просто не находится. Поэтому у таких строк есть отдельный ключ (id).
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
