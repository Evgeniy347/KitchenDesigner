using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;

/// <summary>
/// Очистка ячейки — явный override в «пусто», а не откат к дефолту: пользователь мог
/// нарочно захотеть, чтобы у действия вообще не было основной клавиши.
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

        Assert.IsTrue(bindings.Primary(action).IsEmpty);
        Assert.IsFalse(bindings.IsDefault(action),
            "пустая основная клавиша — это оверрайд, а не «как было»");
    }

    [Test]
    public void ClearAlt_SetsItEmpty_AndDropsTheOverride()
    {
        var bindings = new KeyBindings();
        var action = InputAction.Undo;
        bindings.SetAlt(action, new KeyChord(KeyCode.U));

        KeybindingEditing.Clear(bindings, action, primary: false);

        Assert.IsTrue(bindings.Alt(action).IsEmpty);
        Assert.IsTrue(bindings.IsDefault(action),
            "альтернативная клавиша по умолчанию и так пуста — очистка возвращает к отсутствию оверрайда");
    }

    [Test]
    public void Write_ThenRead_RoundTrips_ForPrimaryAndAlt()
    {
        var bindings = new KeyBindings();
        var action = InputAction.SaveProject;
        var chord = new KeyChord(KeyCode.F2, ctrl: true);

        KeybindingEditing.Write(bindings, action, primary: true, chord);
        Assert.AreEqual(chord, KeybindingEditing.Read(bindings, action, primary: true));

        var altChord = new KeyChord(KeyCode.F3);
        KeybindingEditing.Write(bindings, action, primary: false, altChord);
        Assert.AreEqual(altChord, KeybindingEditing.Read(bindings, action, primary: false));
    }
}
