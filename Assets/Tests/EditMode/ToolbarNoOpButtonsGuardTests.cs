using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.UI;

/// <summary>Страж правила UI-GUIDELINES §9 «Кнопка, нажатие которой в этом состоянии ничего не сделает, выключена»
/// для ВСЕГО тулбара, а не для одной кнопки со скриншота. Каждая кнопка панели обязана попасть
/// ровно в одну из двух таблиц ниже: либо у неё есть условие «в этом состоянии нажатие пустое»,
/// и тогда <c>interactable</c> обязан ему следовать в каждом из проверяемых состояний; либо она
/// названа «всегда исполнимой» с причиной. Новая кнопка, не попавшая ни в одну таблицу, роняет
/// тест — автор вынужден решить вопрос, а не забыть про него.</summary>
public class ToolbarNoOpButtonsGuardTests
{
    private sealed class FakeToolbarHost : IToolbarHost
    {
        public void TogglePanel(ToolbarPanel panel) { }
        public bool IsPanelVisible(ToolbarPanel panel) => false;
        public void SaveCurrent() { }
        public void NewProject() { }
        public void SaveAs() { }
        public void LoadDialog() { }
    }

    private static readonly Dictionary<string, Func<bool>> NoOpWhen = new Dictionary<string, Func<bool>>
    {
        ["Undo"] = () => !CommandStack.CanUndo,
        ["Redo"] = () => !CommandStack.CanRedo,
        ["GotoIssue"] = () => !HasIssueToGoTo(),
        ["LevelUp"] = () => !CurrentSwitcher().CanGoUp,
        ["LevelDown"] = () => !CurrentSwitcher().CanGoDown,
    };

    private static readonly Dictionary<string, string> AlwaysActionable = new Dictionary<string, string>
    {
        ["Spec"] = "переключатель панели: открыть и закрыть возможно всегда",
        ["Hierarchy"] = "переключатель панели",
        ["Errors"] = "переключатель панели: пустой список тоже показывают («ошибок нет»)",
        ["ProjectInstructions"] = "переключатель панели",
        ["Settings"] = "переключатель панели",
        ["New"] = "всегда открывает диалог создания проекта",
        ["Save"] = "сохранение исполнимо и в чистом проекте: перезаписывает файл",
        ["SaveAs"] = "всегда открывает диалог",
        ["Load"] = "переключатель окна",
        ["LevelsWindow"] = "переключатель окна: через него этаж и добавляют",
        ["HandleMode"] = "переключает режим ручек в обе стороны",
        ["MeasureToggle"] = "тоггл режима",
        ["Eyedropper"] = "тоггл режима",
        ["LightsToggle"] = "тоггл: включить и выключить возможно всегда",
        ["DayNight"] = "переключатель панели",
        ["ModeNormal"] = "сегмент выбора режима: выбранный показан нажатым фоном, а не выключен",
        ["ModeRoom"] = "сегмент выбора режима",
        ["ModePhoto"] = "тоггл режима",
        ["Music"] = "переключатель панели",
    };

    private GameObject? _canvasGo;
    private ToolbarUI _toolbar = null!;
    private Transform _bar = null!;

    [SetUp]
    public void SetUp()
    {
        LevelRegistry.Reset();
        CommandStack.Clear();
        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_canvasGo != null) UnityEngine.Object.DestroyImmediate(_canvasGo);
        LevelRegistry.Reset();
        CommandStack.Clear();
    }

    private static LevelSwitcherState CurrentSwitcher() =>
        LevelSwitcherState.Of(LevelRegistry.Snapshot(), LevelRegistry.CurrentId);

    private static bool HasIssueToGoTo()
    {
        foreach (var issue in SceneAnalyzer.Analyze())
            if (issue.Level == IssueLevel.Error || issue.Level == IssueLevel.Warning) return true;
        return false;
    }

    private void Build()
    {
        _toolbar = new ToolbarUI();
        _toolbar.Build(_canvasGo!.transform, new FakeToolbarHost());
        _bar = _canvasGo.transform.Find("Toolbar")!;
        _toolbar.Refresh();
    }

    private static Level[] Floors(int count)
    {
        var levels = new Level[count];
        for (int i = 0; i < count; i++)
            levels[i] = new Level((i + 1).ToString(), (i + 1) + " этаж", i * 3000, 3000);
        return levels;
    }

    private IEnumerable<Button> BarButtons()
    {
        foreach (var button in _bar.GetComponentsInChildren<Button>(true))
            yield return button;
    }

    [Test]
    public void EveryToolbarButton_IsClassified_ExactlyOnce()
    {
        Build();

        var unclassified = new List<string>();
        var twice = new List<string>();
        foreach (var button in BarButtons())
        {
            bool dynamic = NoOpWhen.ContainsKey(button.name);
            bool fixedOn = AlwaysActionable.ContainsKey(button.name);
            if (!dynamic && !fixedOn) unclassified.Add(button.name);
            if (dynamic && fixedOn) twice.Add(button.name);
        }

        Assert.IsEmpty(unclassified,
            "кнопка тулбара, не попавшая в NoOpWhen и AlwaysActionable: " + string.Join(", ", unclassified)
            + ". Решите: бывает ли её нажатие пустым? Если да — выключайте её через interactable и впишите условие в NoOpWhen; "
            + "если нет — впишите в AlwaysActionable с причиной (UI-GUIDELINES §9)");
        Assert.IsEmpty(twice, "кнопка не может быть и «иногда пустой», и «всегда исполнимой»: " + string.Join(", ", twice));
    }

    [Test]
    public void NoStaleEntries_EveryTableNameIsARealToolbarButton()
    {
        Build();
        var real = new HashSet<string>();
        foreach (var button in BarButtons()) real.Add(button.name);

        var stale = new List<string>();
        foreach (var name in NoOpWhen.Keys) if (!real.Contains(name)) stale.Add(name);
        foreach (var name in AlwaysActionable.Keys) if (!real.Contains(name)) stale.Add(name);

        Assert.IsEmpty(stale, "в таблицах стража значатся кнопки, которых на тулбаре нет: " + string.Join(", ", stale));
    }

    private static IEnumerable<TestCaseData> States()
    {
        yield return new TestCaseData("пустой проект, один этаж, стек пуст", 1, "1", false);
        yield return new TestCaseData("один этаж, есть что отменять", 1, "1", true);
        yield return new TestCaseData("три этажа, нижний, стек пуст", 3, "1", false);
        yield return new TestCaseData("три этажа, средний, есть что отменять", 3, "2", true);
        yield return new TestCaseData("три этажа, верхний, стек пуст", 3, "3", false);
    }

    [TestCaseSource(nameof(States))]
    public void EveryButtonWithANoOpState_IsDisabledExactlyThen(string state, int levels, string currentId, bool undoAvailable)
    {
        LevelRegistry.Set(Floors(levels));
        LevelRegistry.CurrentId = currentId;
        if (undoAvailable) CommandStack.Execute(new RenameLevelCommand(LevelRegistry.Current, "Переименован"));
        Build();

        var wrong = new List<string>();
        foreach (var button in BarButtons())
        {
            if (!NoOpWhen.TryGetValue(button.name, out var isNoOp)) continue;
            bool expectedInteractable = !isNoOp();
            if (button.interactable != expectedInteractable)
                wrong.Add($"{button.name}: interactable={button.interactable}, ожидалось {expectedInteractable}");
        }

        Assert.IsEmpty(wrong, $"состояние «{state}»: " + string.Join("; ", wrong));
    }

    [TestCaseSource(nameof(States))]
    public void AlwaysActionableButtons_StayEnabled_InEveryState(string state, int levels, string currentId, bool undoAvailable)
    {
        LevelRegistry.Set(Floors(levels));
        LevelRegistry.CurrentId = currentId;
        if (undoAvailable) CommandStack.Execute(new RenameLevelCommand(LevelRegistry.Current, "Переименован"));
        Build();

        var dead = new List<string>();
        foreach (var button in BarButtons())
            if (AlwaysActionable.ContainsKey(button.name) && !button.interactable) dead.Add(button.name);

        Assert.IsEmpty(dead, $"состояние «{state}»: кнопка названа всегда исполнимой, а выключена: " + string.Join(", ", dead));
    }
}
