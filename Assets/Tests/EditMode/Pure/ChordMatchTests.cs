using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;

public class ChordMatchTests
{
    [Test]
    public void Fires_ChordWithoutModifier_DoesNotFire_WhenCtrlHeld()
    {
        var chord = new KeyChord(KeyCode.D);

        Assert.IsFalse(ChordMatch.Fires(chord, keyEvent: true, ctrlHeld: true, altHeld: false, shiftHeld: false),
            "D без модификатора не должен срабатывать, пока зажат Ctrl — иначе Ctrl+D "
            + "(дублирование) заодно двигает камеру вправо");
    }

    [Test]
    public void Fires_ChordWithoutModifier_Fires_WhenNoModifierHeld()
    {
        var chord = new KeyChord(KeyCode.D);

        Assert.IsTrue(ChordMatch.Fires(chord, keyEvent: true, ctrlHeld: false, altHeld: false, shiftHeld: false));
    }

    [Test]
    public void Fires_ChordWithCtrl_DoesNotFire_WhenCtrlNotHeld()
    {
        var chord = new KeyChord(KeyCode.D, ctrl: true);

        Assert.IsFalse(ChordMatch.Fires(chord, keyEvent: true, ctrlHeld: false, altHeld: false, shiftHeld: false),
            "Ctrl+D не должен срабатывать на голое D — иначе дублирование сработает без Ctrl");
    }

    [Test]
    public void Fires_ChordWithCtrl_Fires_WhenCtrlHeld()
    {
        var chord = new KeyChord(KeyCode.D, ctrl: true);

        Assert.IsTrue(ChordMatch.Fires(chord, keyEvent: true, ctrlHeld: true, altHeld: false, shiftHeld: false));
    }

    [Test]
    public void Fires_RequiresTheKeyEventItself_NotOnlyMatchingModifiers()
    {
        var chord = new KeyChord(KeyCode.D);

        Assert.IsFalse(ChordMatch.Fires(chord, keyEvent: false, ctrlHeld: false, altHeld: false, shiftHeld: false),
            "модификаторы могут совпасть без того, чтобы сама клавиша была нажата в этом кадре");
    }

    [Test]
    public void Fires_EmptyChord_NeverFires()
    {
        Assert.IsFalse(ChordMatch.Fires(KeyChord.Empty, keyEvent: true, ctrlHeld: false, altHeld: false, shiftHeld: false),
            "пустая (не заданная) привязка не должна срабатывать ни на что");
    }

    [Test]
    public void Fires_ExtraModifierBlocksMatch_EvenWhenRequiredOnesAreHeld()
    {
        var chord = new KeyChord(KeyCode.Z, ctrl: true);

        Assert.IsFalse(ChordMatch.Fires(chord, keyEvent: true, ctrlHeld: true, altHeld: false, shiftHeld: true),
            "Ctrl+Z не должен срабатывать вместе с лишним Shift — это другой аккорд (Ctrl+Shift+Z)");
    }

    [Test]
    public void PrimaryAndAltChord_FireEquivalently_ForTheSameModifierState()
    {
        var primary = new KeyChord(KeyCode.Y, ctrl: true);
        var alt = new KeyChord(KeyCode.Z, ctrl: true, shift: true);

        bool primaryFires = ChordMatch.Fires(primary, keyEvent: true, ctrlHeld: true, altHeld: false, shiftHeld: false);
        bool altFires = ChordMatch.Fires(alt, keyEvent: true, ctrlHeld: true, altHeld: false, shiftHeld: true);

        Assert.IsTrue(primaryFires, "основная привязка Redo (Ctrl+Y) обязана срабатывать");
        Assert.IsTrue(altFires, "альтернативная привязка Redo (Ctrl+Shift+Z) обязана срабатывать наравне с основной");
    }

    [Test]
    public void ModifiersMatch_RequiresExactEquality_NotJustASubset()
    {
        var chord = new KeyChord(KeyCode.S, ctrl: true);

        Assert.IsFalse(ChordMatch.ModifiersMatch(chord, ctrlHeld: true, altHeld: true, shiftHeld: false),
            "лишний зажатый Alt делает это другим аккордом, а не тем же Ctrl+S с довеском");
    }
}
