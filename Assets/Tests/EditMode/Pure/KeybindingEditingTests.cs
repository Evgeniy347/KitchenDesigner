using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;

/// <summary>
/// Очистка ячейки — явный override в «пусто», а не откат к дефолту: пользователь мог
/// нарочно захотеть, чтобы у действия вообще не было основной привязки. С приходом мыши
/// то же самое обязано работать и для жеста, и — отдельная проверка — ячейка обязана
/// принимать жест ТУДА ЖЕ, где раньше жила клавиша: одна ячейка, два рода входа.
/// </summary>
public class KeybindingEditingTests
{
    [Test]
    public void ClearPrimary_SetsItEmpty_EvenThoughTheDefaultIsNot()
    {
        var bindings = new KeyBindings();
        var action = InputAction.CameraMoveForward;
        Assert.IsFalse(KeyBindingDefaults.PrimaryOf(action).IsEmpty);

        KeybindingEditing.Clear(bindings, action, primary: true);

        Assert.IsTrue(bindings.PrimaryBinding(action).IsEmpty);
        Assert.IsFalse(bindings.IsDefault(action),
            "пустая основная привязка — это оверрайд, а не «как было»");
    }

    [Test]
    public void ClearPrimary_OfAMouseAction_EmptiesTheGestureToo()
    {
        var bindings = new KeyBindings();
        var action = InputAction.CameraOrbit;
        Assert.IsTrue(KeyBindingDefaults.PrimaryOf(action).IsGesture, "дефолт поворота — жест");

        KeybindingEditing.Clear(bindings, action, primary: true);

        Assert.IsTrue(bindings.PrimaryBinding(action).IsEmpty);
    }

    [Test]
    public void ClearAlt_SetsItEmpty_AndDropsTheOverride()
    {
        var bindings = new KeyBindings();
        var action = InputAction.Undo;
        KeybindingEditing.Write(bindings, action, primary: false,
            InputBinding.FromKey(new KeyChord(KeyCode.U)));

        KeybindingEditing.Clear(bindings, action, primary: false);

        Assert.IsTrue(bindings.AltBinding(action).IsEmpty);
        Assert.IsTrue(bindings.IsDefault(action),
            "альтернативная привязка по умолчанию и так пуста — очистка возвращает к "
            + "отсутствию оверрайда");
    }

    [Test]
    public void Write_ThenRead_RoundTrips_ForPrimaryAndAlt()
    {
        var bindings = new KeyBindings();
        var action = InputAction.SaveProject;
        var primary = InputBinding.FromKey(new KeyChord(KeyCode.F2, ctrl: true));

        KeybindingEditing.Write(bindings, action, primary: true, primary);
        Assert.AreEqual(primary, KeybindingEditing.Read(bindings, action, primary: true));

        var alt = InputBinding.FromKey(new KeyChord(KeyCode.F3));
        KeybindingEditing.Write(bindings, action, primary: false, alt);
        Assert.AreEqual(alt, KeybindingEditing.Read(bindings, action, primary: false));
    }

    [Test]
    public void AGesture_CanBeWrittenIntoTheSameCellAsAKey()
    {
        var bindings = new KeyBindings();
        var action = InputAction.SelectClick;
        var gesture = InputBinding.FromGesture(
            new MouseGesture(MouseButtonKind.XButton1, ctrl: true));

        KeybindingEditing.Write(bindings, action, primary: false, gesture);

        var read = KeybindingEditing.Read(bindings, action, primary: false);
        Assert.AreEqual(gesture, read);
        Assert.IsTrue(read.IsGesture, "ячейка обязана вернуть жест жестом, а не пустым аккордом");
    }

    [Test]
    public void AKeyCanReplaceAGesture_AndBack()
    {
        var bindings = new KeyBindings();
        var action = InputAction.CameraZoomWheel;
        var key = InputBinding.FromKey(new KeyChord(KeyCode.PageUp));

        KeybindingEditing.Write(bindings, action, primary: true, key);
        Assert.AreEqual(key, KeybindingEditing.Read(bindings, action, primary: true));
        Assert.IsTrue(KeybindingEditing.Read(bindings, action, primary: true).IsKey);

        var gesture = InputBinding.FromGesture(MouseGesture.Wheel(shift: true));
        KeybindingEditing.Write(bindings, action, primary: true, gesture);
        Assert.AreEqual(gesture, KeybindingEditing.Read(bindings, action, primary: true));
        Assert.IsTrue(KeybindingEditing.Read(bindings, action, primary: true).IsGesture,
            "переназначение из клавиши обратно в жест не оставляет следа прежнего рода входа");
    }
}
