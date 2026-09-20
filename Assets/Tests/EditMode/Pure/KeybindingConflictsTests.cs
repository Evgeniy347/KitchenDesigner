using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;

/// <summary>
/// Красный текст в ячейке привязки решается по факту конфликта, а не по вкусу: две
/// привязки на один и тот же аккорд — оба конфликтуют, разные аккорды — не конфликтуют
/// ни одна. Описание называет ИМЕННО другую сторону конфликта, а не самого себя.
/// </summary>
public class KeybindingConflictsTests
{
    [Test]
    public void TwoActionsShareOneChord_BothReadAsInConflict()
    {
        var bindings = new KeyBindings();
        var chord = new KeyChord(KeyCode.K);
        bindings.SetPrimary(InputAction.Undo, chord);
        bindings.SetPrimary(InputAction.Redo, chord);

        var conflicts = bindings.FindConflicts();

        Assert.IsTrue(KeybindingConflicts.IsInConflict(conflicts, bindings.Primary(InputAction.Undo)));
        Assert.IsTrue(KeybindingConflicts.IsInConflict(conflicts, bindings.Primary(InputAction.Redo)));
    }

    [Test]
    public void DistinctChords_AreNotInConflict()
    {
        var bindings = new KeyBindings();
        var conflicts = bindings.FindConflicts();

        Assert.IsFalse(KeybindingConflicts.IsInConflict(conflicts, bindings.Primary(InputAction.Undo)));
        Assert.IsFalse(KeybindingConflicts.IsInConflict(conflicts, bindings.Primary(InputAction.Redo)));
    }

    [Test]
    public void EmptyChord_IsNeverInConflict()
    {
        var bindings = new KeyBindings();
        var conflicts = bindings.FindConflicts();

        Assert.IsFalse(KeybindingConflicts.IsInConflict(conflicts, KeyChord.Empty),
            "пустая ячейка не занимает аккорд ни у кого, красной быть не может");
    }

    [Test]
    public void DescribeOthers_NamesTheOtherAction_NotItself()
    {
        var bindings = new KeyBindings();
        var chord = new KeyChord(KeyCode.K);
        bindings.SetPrimary(InputAction.Undo, chord);
        bindings.SetPrimary(InputAction.Redo, chord);
        var conflicts = bindings.FindConflicts();

        string forUndo = KeybindingConflicts.DescribeOthers(conflicts, InputAction.Undo, chord);
        string forRedo = KeybindingConflicts.DescribeOthers(conflicts, InputAction.Redo, chord);

        Assert.AreEqual(InputActionCatalog.DisplayNameOf(InputAction.Redo), forUndo);
        Assert.AreEqual(InputActionCatalog.DisplayNameOf(InputAction.Undo), forRedo);
    }

    [Test]
    public void DescribeOthers_OnANonConflictingChord_IsEmpty()
    {
        var bindings = new KeyBindings();
        var conflicts = bindings.FindConflicts();

        string description = KeybindingConflicts.DescribeOthers(
            conflicts, InputAction.Undo, bindings.Primary(InputAction.Undo));

        Assert.AreEqual(string.Empty, description);
    }

    [Test]
    public void ThreeWayConflict_ListsBothOtherActions()
    {
        var bindings = new KeyBindings();
        var chord = new KeyChord(KeyCode.K);
        bindings.SetPrimary(InputAction.Undo, chord);
        bindings.SetPrimary(InputAction.Redo, chord);
        bindings.SetPrimary(InputAction.DeleteSelected, chord);
        var conflicts = bindings.FindConflicts();

        string description = KeybindingConflicts.DescribeOthers(conflicts, InputAction.Undo, chord);

        StringAssert.Contains(InputActionCatalog.DisplayNameOf(InputAction.Redo), description);
        StringAssert.Contains(InputActionCatalog.DisplayNameOf(InputAction.DeleteSelected), description);
        StringAssert.DoesNotContain(InputActionCatalog.DisplayNameOf(InputAction.Undo), description);
    }
}
