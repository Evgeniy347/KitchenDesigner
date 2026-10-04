using System;
using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// D5 / NN/g «Cancel ≠ Close»: у окна с × кнопки «Закрыть» в футере не бывает — две двери с одним
// смыслом. Обход — по крестикам, как у WindowChromeGuardTests: новое окно попадает под сторожа
// само. Окна, где «Закрыть» ещё стоит, перечислены поимённо со своей задачей; список обязан
// убывать, а запись, чьё окно уже вылечено, краснеет.
public class NoDuplicateCloseGuardTests
{
    private static readonly Dictionary<string, string> KnownGaps = new()
    {
        ["SettingsPanel"] = "T6 — docs/ui-redesign/settings.md, п. 7",
        ["SpecPanel"] = "T7 — docs/ui-redesign/tables.md, «Спецификация»",
    };

    private GameObject? _canvas;
    private GameObject? _host;

    [SetUp]
    public void SetUp()
    {
        LevelRegistry.Reset();
        CommandStack.Clear();
        _canvas = UiTestCanvas.Create("CloseGuardCanvas");
        _host = new GameObject("CloseGuardHost");
        foreach (var type in WindowTypes())
        {
            var build = type.GetMethod("Build", new[] { typeof(Transform) });
            build!.Invoke(_host.AddComponent(type), new object[] { _canvas.transform });
        }
    }

    [TearDown]
    public void TearDown()
    {
        UiTestCanvas.Release(_canvas);
        if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
        LevelRegistry.Reset();
        CommandStack.Clear();
        ProjectWindows.Clear();
    }

    private static IEnumerable<Type> WindowTypes() =>
        typeof(IProjectWindow).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(t)
                && typeof(IProjectWindow).IsAssignableFrom(t))
            .Concat(new[] { typeof(ContextMenuUI), typeof(GroupMenuUI), typeof(MeasurePropertiesUI) })
            .Distinct()
            .OrderBy(t => t.Name);

    private List<RectTransform> WindowsWithACross() =>
        _canvas!.GetComponentsInChildren<Button>(true)
            .Where(b => b.name == WindowChrome.CloseButtonName)
            .Select(b => (RectTransform)b.transform.parent)
            .ToList();

    private static bool HasCloseCaptionButton(RectTransform panel)
    {
        string close = Loc.T("common.close");
        return panel.GetComponentsInChildren<Button>(true)
            .Where(b => b.name != WindowChrome.CloseButtonName)
            .Any(b => b.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text == close));
    }

    [Test]
    public void NoWindowWithACross_AlsoHasACloseButtonInItsBody()
    {
        var offenders = new List<string>();
        foreach (var panel in WindowsWithACross())
        {
            bool doubled = HasCloseCaptionButton(panel);
            bool known = KnownGaps.ContainsKey(panel.name);
            if (doubled && !known) offenders.Add(panel.name + ": «Закрыть» рядом с ×");
            if (!doubled && known) offenders.Add(panel.name + ": уже вылечено — убери из KnownGaps");
        }

        Assert.IsEmpty(offenders,
            "окно закрывается одной дверью — ×; кнопка «Закрыть» в футере дублирует её (D5, NN/g):\n"
            + string.Join("\n", offenders));
    }

    [Test]
    public void TheSweep_SeesTheKnownOffenders()
    {
        var names = WindowsWithACross().Select(p => p.name).ToList();
        Assert.GreaterOrEqual(names.Count, 12, "обход крестиков нашёл слишком мало окон — смотрит не туда");
        foreach (var known in KnownGaps.Keys)
            CollectionAssert.Contains(names, known, "запись долга пережила своё окно: " + known);
        CollectionAssert.Contains(names, "ProjectInstructionsPanel",
            "окно на WindowChrome обязано попадать в обход по крестику");
    }
}
