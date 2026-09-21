using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Keybinding;

/// <summary>
/// b39be086 fixed the mute contour and, in the same commit, gave every InputMap.Held call a
/// silent ExactModifiers default it inherited from InputMap.Down without a separate decision.
/// That default made HandleWASD's Shift-ramp dead on arrival: the default W/A/S/D chords carry
/// no modifier, so a held Shift (needed for the ramp itself, read one line below the InputMap
/// call in CameraController.HandleWASD) made ExactModifiers refuse to fire at all. Nothing in
/// the b39be086 suite could have caught it - ChordMatchTests exercises ChordMatch directly with
/// an explicit mode argument, never through InputMap.Held's own default.
///
/// Held now defaults to RequiredModifiersOnly: a chord fires as long as the modifiers IT asks
/// for are held, and an unrelated extra modifier (Shift, ridden by WASD/arrows/zoom for other
/// gestures) does not block it. Down keeps ExactModifiers - D and Ctrl+D (DuplicateSelected)
/// still have to stay distinct discrete events.
/// </summary>
public class InputMapChordModeTests
{
    [TearDown]
    public void TearDown() => KitchenSettings.Instance.KeyBindings.ClearOverrides();

    [Test]
    public void Held_ShiftHeldWithWasd_StillFires_TheRegressionFromB39be086()
    {
        Assert.IsTrue(
            InputMap.HeldWithSimulatedKeyForTests(InputAction.CameraMoveForward, KeyCode.W, shift: true),
            "Shift+W обязан двигать камеру вперёд с разгоном - на b39be086 Held по умолчанию "
            + "требовал ТОЧНОГО совпадения модификаторов, а дефолтный аккорд W их не несёт, "
            + "поэтому лишний зажатый Shift блокировал W целиком: камера не ехала вообще");
    }

    [TestCase(InputAction.CameraMoveForward, KeyCode.W)]
    [TestCase(InputAction.CameraMoveBack, KeyCode.S)]
    [TestCase(InputAction.CameraMoveLeft, KeyCode.A)]
    [TestCase(InputAction.CameraMoveRight, KeyCode.D)]
    [TestCase(InputAction.CameraRotateLeft, KeyCode.LeftArrow)]
    [TestCase(InputAction.CameraRotateRight, KeyCode.RightArrow)]
    [TestCase(InputAction.CameraRotateUp, KeyCode.UpArrow)]
    [TestCase(InputAction.CameraRotateDown, KeyCode.DownArrow)]
    [TestCase(InputAction.CameraZoomIn, KeyCode.Equals)]
    [TestCase(InputAction.CameraZoomOut, KeyCode.Minus)]
    public void Held_EveryContinuousCameraAction_StillFires_WithAnUnrelatedShiftHeld(InputAction action, KeyCode key)
    {
        Assert.IsTrue(InputMap.HeldWithSimulatedKeyForTests(action, key, shift: true),
            $"{action}: непрерывное действие обязано отвечать на свою клавишу, пока лишний "
            + "Shift занят чем-то другим (разгон WASD, жест мыши) - иначе действие мертво "
            + "ровно так же, как WASD на b39be086");
    }

    [TestCase(InputAction.CameraMoveForward, KeyCode.W)]
    [TestCase(InputAction.CameraRotateLeft, KeyCode.LeftArrow)]
    [TestCase(InputAction.CameraZoomIn, KeyCode.Equals)]
    public void Held_SameActions_StillFire_WithAnUnrelatedCtrlHeld(InputAction action, KeyCode key)
    {
        Assert.IsTrue(InputMap.HeldWithSimulatedKeyForTests(action, key, ctrl: true));
    }

    [Test]
    public void Held_UnderTheOldExactModifiersMode_TheSameChordWouldHaveBeenBlocked()
    {
        Assert.IsFalse(
            ChordMatch.Fires(new KeyChord(KeyCode.W), keyEvent: true,
                ctrlHeld: false, altHeld: false, shiftHeld: true, mode: ChordMatchMode.ExactModifiers),
            "контрольный расчёт: именно это возвращал ChordMatch, когда InputMap.Held (до этого "
            + "коммита) передавал ExactModifiers по умолчанию - лишний Shift не совпадал с "
            + "пустым набором модификаторов дефолтного W");
    }

    [Test]
    public void Held_UserRebindsActionOntoAChordWithAModifier_ThatModifierBecomesRequired()
    {
        KitchenSettings.Instance.KeyBindings.SetPrimary(
            InputAction.CameraMoveForward, new KeyChord(KeyCode.W, ctrl: true));

        Assert.IsFalse(
            InputMap.HeldWithSimulatedKeyForTests(InputAction.CameraMoveForward, KeyCode.W, ctrl: false),
            "пользователь явно назначил Ctrl+W - без Ctrl это уже не тот аккорд, который он "
            + "попросил, и RequiredModifiersOnly обязан это соблюдать, а не только игнорировать "
            + "модификаторы поголовно");
        Assert.IsTrue(
            InputMap.HeldWithSimulatedKeyForTests(InputAction.CameraMoveForward, KeyCode.W, ctrl: true));
    }

    [Test]
    public void Down_DefaultChordWithoutModifier_StillDoesNotFire_WithAnUnrelatedModifierHeld()
    {
        Assert.IsFalse(
            InputMap.DownWithSimulatedKeyForTests(InputAction.CameraFocusSelection, KeyCode.F, ctrl: true),
            "Down остаётся строгим по умолчанию: F и гипотетический Ctrl+F обязаны различаться, "
            + "как D (CameraMoveRight) и Ctrl+D (DuplicateSelected) - RequiredModifiersOnly для "
            + "Held не должен был тайком смягчить и Down тоже");
    }

    [Test]
    public void Down_DuplicateVsBareD_StayDistinctUnderTheNewHeldDefault()
    {
        Assert.IsFalse(
            InputMap.DownWithSimulatedKeyForTests(InputAction.DuplicateSelected, KeyCode.D, ctrl: false),
            "без Ctrl нажатие D не обязано дублировать выделение");
        Assert.IsTrue(
            InputMap.DownWithSimulatedKeyForTests(InputAction.DuplicateSelected, KeyCode.D, ctrl: true));
    }
}
