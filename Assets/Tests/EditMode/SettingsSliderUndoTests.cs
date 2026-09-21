using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Ползунки настроек меняли значение мимо `CommandStack` — унаследованный долг, который
/// видно пользователю: подвигал ползунок, нажал Ctrl+Z и откатил не его, а предыдущую
/// правку, например только что назначенную привязку.
///
/// Особенность, из-за которой это не копия тумблера: перетаскивание выдаёт десятки
/// изменений значения, а шаг отмены обязан быть ОДИН. Граница жеста — нажатие и
/// отпускание: на нажатии запоминается значение, на отпускании кладётся одна команда
/// «было → стало». Отдельное изменение без перетаскивания (клик по дорожке, стрелки с
/// клавиатуры) — само по себе законченное действие и кладёт свой шаг.
///
/// Проверяется на настоящей панели: `CommandStack` живёт в `Core/Commands`, вне быстрого
/// пути, и копия стека в чистом слое была бы тестом, который не может упасть.
/// </summary>
public class SettingsSliderUndoTests
{
    private const string WasdSpeed = "Скорость WASD";

    private Canvas? _canvas;
    private KitchenSettingsData? _saved;

    [SetUp]
    public void SetUp()
    {
        UIFactory.EnsureEventSystem();

        var settings = KitchenSettings.Instance;
        _saved = settings.ToData();
        settings.ResetToDefaults();
        CommandStack.Clear();

        _canvas = UIFactory.CreateCanvas("SettingsSliderUndoCanvas");
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
    public void OneDragThroughManyValues_LeavesExactlyOneUndoStep_BackToWhereItStarted()
    {
        var settings = KitchenSettings.Instance;
        var slider = SliderNamed(WasdSpeed);
        float started = settings.WasdSpeed;

        Press(slider);
        slider.value = 1.5f;
        slider.value = 2f;
        slider.value = 2.5f;
        Release(slider);

        Assert.AreEqual(2.5f, settings.WasdSpeed, 0.001f, "ползунок обязан записать настройку");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "одно перетаскивание — ОДИН шаг отмены. Ноль означает, что значение поехало мимо "
            + "стека и Ctrl+Z откатит чужую правку; три означают, что человеку придётся жать "
            + "Ctrl+Z по разу на каждый кадр перетаскивания");

        CommandStack.Undo();

        Assert.AreEqual(started, settings.WasdSpeed, 0.001f,
            "отмена возвращает к значению ДО жеста, а не к предпоследнему кадру");
        Assert.AreEqual(started, slider.value, 0.001f, "и сам ползунок едет обратно");
    }

    [Test]
    public void AChangeWithoutADrag_IsItsOwnStep()
    {
        var settings = KitchenSettings.Instance;
        var slider = SliderNamed(WasdSpeed);
        float started = settings.WasdSpeed;

        slider.value = 2f;

        Assert.AreEqual(1, CommandStack.UndoCount,
            "клик по дорожке и стрелки с клавиатуры — законченные действия, у каждого свой шаг");

        CommandStack.Undo();
        Assert.AreEqual(started, settings.WasdSpeed, 0.001f);
    }

    [Test]
    public void ADragThatEndsWhereItBegan_LeavesNoStepAtAll()
    {
        var slider = SliderNamed(WasdSpeed);
        float started = slider.value;

        Press(slider);
        slider.value = 2.5f;
        slider.value = started;
        Release(slider);

        Assert.AreEqual(0, CommandStack.UndoCount,
            "жест, вернувшийся в исходное значение, ничего не изменил — класть в историю "
            + "нечего, иначе Ctrl+Z будет «отменять» бездействие");
    }

    [Test]
    public void UndoingADrag_ThenDraggingAgain_StartsFromTheRestoredValue()
    {
        var settings = KitchenSettings.Instance;
        var slider = SliderNamed(WasdSpeed);
        float started = settings.WasdSpeed;

        Press(slider);
        slider.value = 2.5f;
        Release(slider);
        CommandStack.Undo();

        Press(slider);
        slider.value = 2f;
        Release(slider);

        Assert.AreEqual(2f, settings.WasdSpeed, 0.001f);

        CommandStack.Undo();

        Assert.AreEqual(started, settings.WasdSpeed, 0.001f,
            "после отмены ползунок обязан помнить ВОССТАНОВЛЕННОЕ значение как исходное: "
            + "иначе следующая отмена вернёт то, чего на экране уже не было");
    }

    private static void Press(Slider slider) =>
        ExecuteEvents.Execute(slider.gameObject, new PointerEventData(EventSystem.current),
            ExecuteEvents.pointerDownHandler);

    private static void Release(Slider slider) =>
        ExecuteEvents.Execute(slider.gameObject, new PointerEventData(EventSystem.current),
            ExecuteEvents.pointerUpHandler);

    private Slider SliderNamed(string label)
    {
        var slider = _canvas!.GetComponentsInChildren<Slider>(true)
            .FirstOrDefault(s => s.name == "Sld_" + label);

        Assert.IsNotNull(slider, $"во вкладке «Управление» нет ползунка «{label}»");
        return slider!;
    }
}
