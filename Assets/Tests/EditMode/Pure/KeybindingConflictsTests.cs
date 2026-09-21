using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;

/// <summary>
/// Красный текст в ячейке решается по факту конфликта, а не по вкусу: две привязки на
/// один и тот же вход — оба конфликтуют, разные входы — не конфликтует ни одна.
/// Описание называет ИМЕННО другую сторону конфликта, а не самого себя.
///
/// С приходом мыши добавилась пара противоположных входов, которой раньше быть не могло:
/// клавиша и жест не имеют права красить друг друга, даже когда их текстовые записи
/// похожи. Вход у них разного рода, и `InputBinding` это знает.
/// </summary>
public class KeybindingConflictsTests
{
    private static InputBinding Key(KeyCode key, bool ctrl = false) =>
        InputBinding.FromKey(new KeyChord(key, ctrl: ctrl));

    private static InputBinding Gesture(MouseButtonKind button, bool withMotion = false,
        bool ctrl = false) =>
        InputBinding.FromGesture(new MouseGesture(button, withMotion, ctrl: ctrl));

    [Test]
    public void TwoActionsShareOneChord_BothReadAsInConflict()
    {
        var bindings = new KeyBindings();
        var shared = Key(KeyCode.K);
        bindings.SetPrimaryBinding(InputAction.Undo, shared);
        bindings.SetPrimaryBinding(InputAction.Redo, shared);

        var conflicts = bindings.FindConflicts();

        Assert.IsTrue(KeybindingConflicts.IsInConflict(conflicts, bindings.PrimaryBinding(InputAction.Undo)));
        Assert.IsTrue(KeybindingConflicts.IsInConflict(conflicts, bindings.PrimaryBinding(InputAction.Redo)));
    }

    [Test]
    public void TwoActionsShareOneGesture_BothReadAsInConflict()
    {
        var bindings = new KeyBindings();
        var shared = Gesture(MouseButtonKind.Right, withMotion: true);
        bindings.SetPrimaryBinding(InputAction.CameraOrbit, shared);
        bindings.SetPrimaryBinding(InputAction.CameraPan, shared);

        var conflicts = bindings.FindConflicts();

        Assert.IsTrue(KeybindingConflicts.IsInConflict(conflicts, bindings.PrimaryBinding(InputAction.CameraOrbit)));
        Assert.IsTrue(KeybindingConflicts.IsInConflict(conflicts, bindings.PrimaryBinding(InputAction.CameraPan)));
    }

    [Test]
    public void AKeyAndAGesture_NeverPaintEachOther()
    {
        var bindings = new KeyBindings();
        bindings.SetPrimaryBinding(InputAction.Undo, Key(KeyCode.Mouse0));
        bindings.SetPrimaryBinding(InputAction.SelectClick, Gesture(MouseButtonKind.Left));

        var conflicts = bindings.FindConflicts();

        Assert.IsFalse(KeybindingConflicts.IsInConflict(conflicts, bindings.PrimaryBinding(InputAction.Undo)),
            "клавиша и жест — входы разного рода: одинаково выглядящая запись не делает их "
            + "одним и тем же нажатием");
        Assert.IsFalse(KeybindingConflicts.IsInConflict(conflicts,
            bindings.PrimaryBinding(InputAction.SelectClick)));
    }

    [Test]
    public void AGestureWithMotion_DoesNotConflictWithTheSameButtonWithout()
    {
        var bindings = new KeyBindings();
        bindings.SetPrimaryBinding(InputAction.CameraOrbit, Gesture(MouseButtonKind.Right, withMotion: true));
        bindings.SetPrimaryBinding(InputAction.SelectClick, Gesture(MouseButtonKind.Right));

        var conflicts = bindings.FindConflicts();

        Assert.IsFalse(KeybindingConflicts.IsInConflict(conflicts,
            bindings.PrimaryBinding(InputAction.CameraOrbit)),
            "ПКМ с движением и ПКМ без движения — разные жесты, на том и стоит контекстное меню");
    }

    [Test]
    public void DefaultBindings_HaveNoConflictAtAll()
    {
        var bindings = new KeyBindings();

        CollectionAssert.IsEmpty(bindings.FindConflicts(),
            "заводские привязки не конфликтуют между собой — иначе вкладка краснеет у "
            + "человека, который ничего не менял");
    }

    [Test]
    public void EmptyBinding_IsNeverInConflict()
    {
        var bindings = new KeyBindings();
        var conflicts = bindings.FindConflicts();

        Assert.IsFalse(KeybindingConflicts.IsInConflict(conflicts, InputBinding.Empty),
            "пустая ячейка не занимает вход ни у кого, красной быть не может");
    }

    [Test]
    public void DescribeOthers_NamesTheOtherAction_NotItself()
    {
        var bindings = new KeyBindings();
        var shared = Key(KeyCode.K);
        bindings.SetPrimaryBinding(InputAction.Undo, shared);
        bindings.SetPrimaryBinding(InputAction.Redo, shared);
        var conflicts = bindings.FindConflicts();

        Assert.AreEqual(InputActionCatalog.DisplayNameOf(InputAction.Redo),
            KeybindingConflicts.DescribeOthers(conflicts, InputAction.Undo, shared));
        Assert.AreEqual(InputActionCatalog.DisplayNameOf(InputAction.Undo),
            KeybindingConflicts.DescribeOthers(conflicts, InputAction.Redo, shared));
    }

    [Test]
    public void DescribeOthers_WorksForGesturesToo()
    {
        var bindings = new KeyBindings();
        var shared = Gesture(MouseButtonKind.Middle, withMotion: true);
        bindings.SetPrimaryBinding(InputAction.CameraOrbit, shared);
        bindings.SetPrimaryBinding(InputAction.CameraPan, shared);
        var conflicts = bindings.FindConflicts();

        Assert.AreEqual(InputActionCatalog.DisplayNameOf(InputAction.CameraPan),
            KeybindingConflicts.DescribeOthers(conflicts, InputAction.CameraOrbit, shared));
    }

    [Test]
    public void DescribeOthers_OnANonConflictingBinding_IsEmpty()
    {
        var bindings = new KeyBindings();
        var conflicts = bindings.FindConflicts();

        Assert.AreEqual(string.Empty, KeybindingConflicts.DescribeOthers(
            conflicts, InputAction.Undo, bindings.PrimaryBinding(InputAction.Undo)));
    }

    [Test]
    public void ThreeWayConflict_ListsBothOtherActions()
    {
        var bindings = new KeyBindings();
        var shared = Key(KeyCode.K);
        bindings.SetPrimaryBinding(InputAction.Undo, shared);
        bindings.SetPrimaryBinding(InputAction.Redo, shared);
        bindings.SetPrimaryBinding(InputAction.DeleteSelected, shared);
        var conflicts = bindings.FindConflicts();

        string description = KeybindingConflicts.DescribeOthers(conflicts, InputAction.Undo, shared);

        StringAssert.Contains(InputActionCatalog.DisplayNameOf(InputAction.Redo), description);
        StringAssert.Contains(InputActionCatalog.DisplayNameOf(InputAction.DeleteSelected), description);
        StringAssert.DoesNotContain(InputActionCatalog.DisplayNameOf(InputAction.Undo), description);
    }
}
