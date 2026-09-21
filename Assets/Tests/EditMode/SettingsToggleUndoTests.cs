using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Переключатель настроек обязан ходить через `CommandStack` (docs/UI-GUIDELINES.md §2:
/// «новое свойство без Command — дефект»), и проверить это можно только здесь: стек живёт
/// в `Core/Commands`, а быстрый путь собирает `Core/Geometry` и `Core/Pure`. Копия стека в
/// чистом слое была бы тестом, который не может упасть.
///
/// Цена пропуска видна человеку: без своей команды Ctrl+Z после переключения тумблера
/// откатывает не его, а ПРЕДЫДУЩУЮ правку — например, только что назначенную привязку.
/// Поэтому проверок две половины сразу: значение изменилось ровно за ОДИН шаг отмены, и
/// этот шаг возвращает и настройку, и сам тумблер на экране.
/// </summary>
public class SettingsToggleUndoTests
{
    private const string InvertY = "Инверсия мыши по вертикали";
    private const string InvertX = "Инверсия мыши по горизонтали";

    private Canvas? _canvas;
    private KitchenSettingsData? _saved;

    [SetUp]
    public void SetUp()
    {
        var settings = KitchenSettings.Instance;
        _saved = settings.ToData();
        settings.ResetToDefaults();
        CommandStack.Clear();

        _canvas = UIFactory.CreateCanvas("SettingsToggleUndoCanvas");
        var panel = _canvas.gameObject.AddComponent<SettingsPanelUI>();
        panel.Build(_canvas.transform);
        panel.OpenControlsTab();
    }

    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear();
        if (_saved != null) KitchenSettings.Instance.ApplyFrom(_saved);
        EditModeManager.Reset();
        if (_canvas != null) UnityEngine.Object.DestroyImmediate(_canvas.gameObject);
    }

    [Test]
    public void TogglingTheVerticalInversion_TakesExactlyOneUndoStep_AndComesBack()
    {
        var settings = KitchenSettings.Instance;
        var toggle = ToggleNamed(InvertY);

        Assert.IsFalse(settings.MouseInvertY, "заводская настройка — инверсия выключена");
        Assert.IsFalse(toggle.isOn, "тумблер обязан показывать настройку, а не своё состояние");

        toggle.isOn = true;

        Assert.IsTrue(settings.MouseInvertY, "переключатель обязан записать настройку");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "одно переключение — ровно один шаг отмены. Ноль означает, что настройка "
            + "поменялась мимо стека, и Ctrl+Z откатит чужую правку; больше одного — что "
            + "человеку придётся жать Ctrl+Z дважды за одно действие");

        CommandStack.Undo();

        Assert.IsFalse(settings.MouseInvertY, "отмена обязана вернуть настройку");
        Assert.IsFalse(toggle.isOn,
            "и сам тумблер: настройка, откатившаяся молча под включённым тумблером, — два "
            + "описания одного значения на экране");
    }

    [Test]
    public void TheHorizontalInversion_HasItsOwnUndoStep()
    {
        var settings = KitchenSettings.Instance;

        ToggleNamed(InvertX).isOn = true;
        ToggleNamed(InvertY).isOn = true;

        Assert.AreEqual(2, CommandStack.UndoCount, "две правки — два шага");

        CommandStack.Undo();

        Assert.IsFalse(settings.MouseInvertY, "последним отменяется последнее");
        Assert.IsTrue(settings.MouseInvertX,
            "а соседняя настройка не имеет права уехать вместе с ним");
    }

    [Test]
    public void RedoAfterUndo_PutsBackBothTheSettingAndItsToggle()
    {
        var settings = KitchenSettings.Instance;
        ToggleNamed(InvertX).isOn = true;

        CommandStack.Undo();
        Assert.IsFalse(settings.MouseInvertX);

        CommandStack.Redo();
        Assert.IsTrue(settings.MouseInvertX, "повтор обязан вернуть то, что отменили");
        Assert.IsTrue(ToggleNamed(InvertX).isOn, "вместе с тумблером");
    }

    private Toggle ToggleNamed(string label)
    {
        var toggle = _canvas!.GetComponentsInChildren<Toggle>(true)
            .FirstOrDefault(t => t.name == "Tgl_" + label);

        Assert.IsNotNull(toggle, $"во вкладке «Управление» нет переключателя «{label}»");
        return toggle!;
    }
}
