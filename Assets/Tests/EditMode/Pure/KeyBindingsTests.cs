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
            bindings.PrimaryBinding(InputAction.CameraMoveForward));
    }

    [Test]
    public void KeyBindings_Alt_IsEmpty_WhenNoOverride()
    {
        var bindings = new KeyBindings();
        Assert.IsTrue(bindings.AltBinding(InputAction.Undo).IsEmpty,
            "альтернативная привязка по умолчанию не заполнена ни для одного действия");
    }

    [Test]
    public void KeyBindings_SetPrimary_OverridesTheDefault()
    {
        var bindings = new KeyBindings();
        var chord = new KeyChord(KeyCode.K);

        bindings.SetPrimaryBinding(InputAction.CameraFocusSelection, InputBinding.FromKey(chord));

        Assert.AreEqual(InputBinding.FromKey(chord), bindings.PrimaryBinding(InputAction.CameraFocusSelection));
        Assert.IsFalse(bindings.IsDefault(InputAction.CameraFocusSelection));
    }

    [Test]
    public void KeyBindings_SetPrimary_ToTheDefaultValue_ClearsTheOverride()
    {
        var bindings = new KeyBindings();
        var action = InputAction.CameraFocusSelection;

        bindings.SetPrimaryBinding(action, InputBinding.FromKey(new KeyChord(KeyCode.K)));
        bindings.SetPrimaryBinding(action, KeyBindingDefaults.PrimaryOf(action));

        Assert.IsTrue(bindings.IsDefault(action),
            "возврат основной привязки к дефолтному значению — это не «переопределение на дефолт», "
            + "а отсутствие переопределения вовсе");
    }

    [Test]
    public void KeyBindings_SetAlt_ToEmpty_ClearsTheOverride()
    {
        var bindings = new KeyBindings();
        var action = InputAction.Undo;

        bindings.SetAltBinding(action, InputBinding.FromKey(new KeyChord(KeyCode.U)));
        bindings.SetAltBinding(action, InputBinding.Empty);

        Assert.IsTrue(bindings.IsDefault(action));
    }

    [Test]
    public void KeyBindings_ClearOverrides_RestoresEveryActionToItsDefault()
    {
        var bindings = new KeyBindings();
        bindings.SetPrimaryBinding(InputAction.Undo, InputBinding.FromKey(new KeyChord(KeyCode.U)));
        bindings.SetAltBinding(InputAction.Redo, InputBinding.FromKey(new KeyChord(KeyCode.R)));

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
        bindings.SetPrimaryBinding(InputAction.CameraFocusSelection, InputBinding.FromKey(shared));
        bindings.SetPrimaryBinding(InputAction.ToggleDevConsole, InputBinding.FromKey(shared));

        var conflicts = bindings.FindConflicts();

        Assert.AreEqual(1, conflicts.Count);
        Assert.AreEqual(InputBinding.FromKey(shared), conflicts[0].Binding);
        CollectionAssert.AreEquivalent(
            new[] { InputAction.CameraFocusSelection, InputAction.ToggleDevConsole },
            conflicts[0].Actions);
    }

    [Test]
    public void KeyBindings_FindConflicts_DetectsAConflictThroughTheAlternativeBindingToo()
    {
        var bindings = new KeyBindings();
        var shared = new KeyChord(KeyCode.K);

        bindings.SetPrimaryBinding(InputAction.CameraFocusSelection, InputBinding.FromKey(shared));
        bindings.SetAltBinding(InputAction.ToggleDevConsole, InputBinding.FromKey(shared));

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

        bindings.SetPrimaryBinding(action, InputBinding.FromKey(chord));
        bindings.SetAltBinding(action, InputBinding.FromKey(chord));

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

    [Test]
    public void KeyBindings_FindConflicts_DetectsTwoActionsSharingAMouseGesture()
    {
        var bindings = new KeyBindings();
        var shared = InputBinding.FromGesture(new MouseGesture(MouseButtonKind.XButton1, withMotion: true));
        bindings.SetPrimaryBinding(InputAction.CameraPan, shared);
        bindings.SetPrimaryBinding(InputAction.CameraOrbit, shared);

        var conflicts = bindings.FindConflicts();

        Assert.AreEqual(1, conflicts.Count,
            "два жеста мыши сталкиваются точно так же, как два аккорда клавиш");
        Assert.AreEqual(shared, conflicts[0].Binding);
        CollectionAssert.AreEquivalent(
            new[] { InputAction.CameraPan, InputAction.CameraOrbit }, conflicts[0].Actions);
    }

    [Test]
    public void KeyBindings_FindConflicts_AKeyAndAGesture_NeverConflict_TestedFromTheKeySide()
    {
        var bindings = new KeyBindings();

        bindings.SetPrimaryBinding(InputAction.CameraFocusSelection,
            InputBinding.FromKey(new KeyChord(KeyCode.K)));
        bindings.SetPrimaryBinding(InputAction.CameraOrbit,
            InputBinding.FromGesture(new MouseGesture(MouseButtonKind.Right)));

        Assert.IsEmpty(bindings.FindConflicts(),
            "клавиша и жест мыши физически не спорят друг с другом — они никогда не конфликт, "
            + "даже когда оба заняты и ничего больше не совпадает");
    }

    [Test]
    public void KeyBindings_FindConflicts_AKeyAndAGesture_NeverConflict_TestedFromTheGestureSide()
    {
        var bindings = new KeyBindings();
        var gesture = InputBinding.FromGesture(new MouseGesture(MouseButtonKind.Left, ctrl: true));

        bindings.SetPrimaryBinding(InputAction.SelectMultiClick, gesture);
        bindings.SetPrimaryBinding(InputAction.DuplicateSelected,
            InputBinding.FromKey(new KeyChord(KeyCode.D, ctrl: true)));

        var conflicts = bindings.FindConflicts();

        Assert.IsFalse(conflicts.Any(c => c.Actions.Contains(InputAction.SelectMultiClick)
            && c.Actions.Contains(InputAction.DuplicateSelected)),
            "Ctrl+ЛКМ (жест) и Ctrl+D (клавиша) не должны попасть в один список конфликтов, "
            + "хотя оба несут модификатор Ctrl");
    }
}
