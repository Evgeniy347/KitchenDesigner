using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;

public class KeyBindingsTests
{
    [Test]
    public void KeyBindings_Primary_FallsBackToDefault_WhenNoOverride()
    {
        var bindings = new KeyBindings();
        Assert.AreEqual(KeyBindingDefaults.PrimaryOf(InputAction.CameraMoveForward),
            bindings.Primary(InputAction.CameraMoveForward));
    }

    [Test]
    public void KeyBindings_Alt_IsEmpty_WhenNoOverride()
    {
        var bindings = new KeyBindings();
        Assert.IsTrue(bindings.Alt(InputAction.Undo).IsEmpty,
            "альтернативная привязка по умолчанию не заполнена ни для одного действия");
    }

    [Test]
    public void KeyBindings_SetPrimary_OverridesTheDefault()
    {
        var bindings = new KeyBindings();
        var chord = new KeyChord(KeyCode.K);

        bindings.SetPrimary(InputAction.CameraFocusSelection, chord);

        Assert.AreEqual(chord, bindings.Primary(InputAction.CameraFocusSelection));
        Assert.IsFalse(bindings.IsDefault(InputAction.CameraFocusSelection));
    }

    [Test]
    public void KeyBindings_SetPrimary_ToTheDefaultValue_ClearsTheOverride()
    {
        var bindings = new KeyBindings();
        var action = InputAction.CameraFocusSelection;

        bindings.SetPrimary(action, new KeyChord(KeyCode.K));
        bindings.SetPrimary(action, KeyBindingDefaults.PrimaryOf(action));

        Assert.IsTrue(bindings.IsDefault(action),
            "возврат основной привязки к дефолтному значению — это не «переопределение на дефолт», "
            + "а отсутствие переопределения вовсе");
    }

    [Test]
    public void KeyBindings_SetAlt_ToEmpty_ClearsTheOverride()
    {
        var bindings = new KeyBindings();
        var action = InputAction.Undo;

        bindings.SetAlt(action, new KeyChord(KeyCode.U));
        bindings.SetAlt(action, KeyChord.Empty);

        Assert.IsTrue(bindings.IsDefault(action));
    }

    [Test]
    public void KeyBindings_ClearOverrides_RestoresEveryActionToItsDefault()
    {
        var bindings = new KeyBindings();
        bindings.SetPrimary(InputAction.Undo, new KeyChord(KeyCode.U));
        bindings.SetAlt(InputAction.Redo, new KeyChord(KeyCode.R));

        bindings.ClearOverrides();

        Assert.IsTrue(bindings.IsDefault(InputAction.Undo));
        Assert.IsTrue(bindings.IsDefault(InputAction.Redo));
        Assert.IsEmpty(bindings.OverriddenActions.ToArray());
    }

    [Test]
    public void KeyBindings_FindConflicts_EmptyByDefault()
    {
        var bindings = new KeyBindings();
        Assert.IsEmpty(bindings.FindConflicts(),
            "таблица дефолтов не должна назначать один и тот же chord двум действиям");
    }

    [Test]
    public void KeyBindings_FindConflicts_DetectsTwoActionsSharingAPrimaryBinding()
    {
        var bindings = new KeyBindings();
        var shared = new KeyChord(KeyCode.K);
        bindings.SetPrimary(InputAction.CameraFocusSelection, shared);
        bindings.SetPrimary(InputAction.ToggleDevConsole, shared);

        var conflicts = bindings.FindConflicts();

        Assert.AreEqual(1, conflicts.Count);
        Assert.AreEqual(shared, conflicts[0].Chord);
        CollectionAssert.AreEquivalent(
            new[] { InputAction.CameraFocusSelection, InputAction.ToggleDevConsole },
            conflicts[0].Actions);
    }

    [Test]
    public void KeyBindings_FindConflicts_DetectsAConflictThroughTheAlternativeBindingToo()
    {
        var bindings = new KeyBindings();
        var shared = new KeyChord(KeyCode.K);

        bindings.SetPrimary(InputAction.CameraFocusSelection, shared);
        bindings.SetAlt(InputAction.ToggleDevConsole, shared);

        var conflicts = bindings.FindConflicts();

        Assert.AreEqual(1, conflicts.Count,
            "конфликт обязан находиться, даже если один из двух chord — альтернативная привязка");
        CollectionAssert.AreEquivalent(
            new[] { InputAction.CameraFocusSelection, InputAction.ToggleDevConsole },
            conflicts[0].Actions);
    }

    [Test]
    public void KeyBindings_FindConflicts_SameChordInBothSlotsOfOneAction_IsNotAConflict()
    {
        var bindings = new KeyBindings();
        var chord = new KeyChord(KeyCode.K);
        var action = InputAction.CameraFocusSelection;

        bindings.SetPrimary(action, chord);
        bindings.SetAlt(action, chord);

        Assert.IsEmpty(bindings.FindConflicts(),
            "один и тот же chord в обеих привязках ОДНОГО действия — не конфликт, "
            + "конфликт бывает только между разными действиями");
    }

    [Test]
    public void KeyBindings_FindConflicts_IgnoresEmptyChords()
    {
        var bindings = new KeyBindings();
        Assert.IsEmpty(bindings.FindConflicts(),
            "множество пустых альтернативных привязок не должно засчитываться как конфликт");
    }
}
