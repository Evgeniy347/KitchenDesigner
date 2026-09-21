using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;

public class KeyBindingDefaultsTests
{
    [Test]
    public void Redo_PrimaryIsCtrlY_AltIsCtrlShiftZ()
    {
        Assert.AreEqual(InputBinding.FromKey(new KeyChord(KeyCode.Y, ctrl: true)),
            KeyBindingDefaults.PrimaryOf(InputAction.Redo));
        Assert.AreEqual(InputBinding.FromKey(new KeyChord(KeyCode.Z, ctrl: true, shift: true)),
            KeyBindingDefaults.AltOf(InputAction.Redo),
            "второй зашитый ключ Redo (Ctrl+Shift+Z) обязан остаться доступен как альтернативная привязка");
    }

    [Test]
    public void CameraZoomIn_PrimaryIsEquals_AltIsKeypadPlus()
    {
        Assert.AreEqual(InputBinding.FromKey(new KeyChord(KeyCode.Equals)),
            KeyBindingDefaults.PrimaryOf(InputAction.CameraZoomIn));
        Assert.AreEqual(InputBinding.FromKey(new KeyChord(KeyCode.KeypadPlus)),
            KeyBindingDefaults.AltOf(InputAction.CameraZoomIn));
    }

    [Test]
    public void CameraZoomOut_PrimaryIsMinus_AltIsKeypadMinus()
    {
        Assert.AreEqual(InputBinding.FromKey(new KeyChord(KeyCode.Minus)),
            KeyBindingDefaults.PrimaryOf(InputAction.CameraZoomOut));
        Assert.AreEqual(InputBinding.FromKey(new KeyChord(KeyCode.KeypadMinus)),
            KeyBindingDefaults.AltOf(InputAction.CameraZoomOut));
    }

    [Test]
    public void Undo_HasNoAltByDefault()
    {
        Assert.IsTrue(KeyBindingDefaults.AltOf(InputAction.Undo).IsEmpty,
            "у Undo никогда не было второго зашитого ключа — альтернатива не должна появиться сама собой");
    }

    [Test]
    public void EveryDefaultAltChord_RoundTripsThroughFormatAndParse()
    {
        foreach (var action in InputActionCatalog.All)
        {
            var alt = KeyBindingDefaults.AltOf(action);
            var reparsed = InputBinding.Parse(InputBinding.Format(alt));

            Assert.AreEqual(alt, reparsed,
                $"альтернативная привязка по умолчанию для {action} обязана пережить печать в текст и разбор обратно");
        }
    }
}
