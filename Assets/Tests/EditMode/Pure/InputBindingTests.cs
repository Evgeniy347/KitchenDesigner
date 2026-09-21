using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;

public class InputBindingTests
{
    [Test]
    public void InputBinding_Format_FromKey_MatchesKeyChordFormat()
    {
        var chord = new KeyChord(KeyCode.Z, ctrl: true, shift: true);
        var binding = InputBinding.FromKey(chord);

        Assert.AreEqual(KeyChord.Format(chord), InputBinding.Format(binding));
    }

    [Test]
    public void InputBinding_Format_FromGesture_MatchesMouseGestureFormat()
    {
        var gesture = new MouseGesture(MouseButtonKind.Right, withMotion: true);
        var binding = InputBinding.FromGesture(gesture);

        Assert.AreEqual(MouseGesture.Format(gesture), InputBinding.Format(binding));
    }

    [TestCase("W")]
    [TestCase("Ctrl+Shift+Z")]
    [TestCase("F9")]
    [TestCase("RMB+Move")]
    [TestCase("Ctrl+LMB")]
    [TestCase("Wheel")]
    [TestCase("")]
    public void InputBinding_Format_RoundTripsThroughParse(string canonicalText)
    {
        var parsed = InputBinding.Parse(canonicalText);
        Assert.AreEqual(canonicalText, InputBinding.Format(parsed),
            $"привязка «{canonicalText}» обязана пережить разбор и обратную печать без изменений, "
            + "независимо от того, клавиша это или жест мыши");
    }

    [Test]
    public void InputBinding_Parse_RoundTripsThroughFormat_ForEveryDefaultBinding()
    {
        foreach (var action in InputActionCatalog.All)
        {
            var primary = KeyBindingDefaults.PrimaryOf(action);
            var reparsed = InputBinding.Parse(InputBinding.Format(primary));

            Assert.AreEqual(primary, reparsed,
                $"дефолтная привязка {action} обязана пережить печать в текст и разбор обратно, "
                + "будь она клавишей или жестом");
        }
    }

    [Test]
    public void InputBinding_Equals_AKeyBindingAndAGestureBinding_AreNeverEqual_EvenWhenBothEmpty()
    {
        var emptyKey = InputBinding.FromKey(KeyChord.Empty);
        var emptyGesture = InputBinding.FromGesture(MouseGesture.Empty);

        Assert.AreEqual(emptyKey, emptyGesture,
            "два пустых значения — одна и та же «привязки нет», вне зависимости от тега");
        Assert.AreEqual(InputBinding.Empty, emptyKey);
        Assert.AreEqual(InputBinding.Empty, emptyGesture);
    }

    [Test]
    public void InputBinding_Equals_ARealKeyAndARealGesture_AreNeverEqual()
    {
        var key = InputBinding.FromKey(new KeyChord(KeyCode.W));
        var gesture = InputBinding.FromGesture(new MouseGesture(MouseButtonKind.Left));

        Assert.AreNotEqual(key, gesture,
            "клавиша и жест физически не спорят друг с другом — они не могут быть равны");
    }

    [Test]
    public void InputBinding_IsKey_And_IsGesture_AreMutuallyExclusive()
    {
        var key = InputBinding.FromKey(new KeyChord(KeyCode.W));
        Assert.IsTrue(key.IsKey);
        Assert.IsFalse(key.IsGesture);

        var gesture = InputBinding.FromGesture(new MouseGesture(MouseButtonKind.Left));
        Assert.IsTrue(gesture.IsGesture);
        Assert.IsFalse(gesture.IsKey);

        Assert.IsFalse(InputBinding.Empty.IsKey);
        Assert.IsFalse(InputBinding.Empty.IsGesture);
    }

    [Test]
    public void InputBinding_Parse_PrefersAGestureToken_OverAnAmbiguousLookingKeyName()
    {
        var binding = InputBinding.Parse("Wheel");
        Assert.IsTrue(binding.IsGesture, "«Wheel» не совпадает ни с одним KeyCode, разбирается как жест");
    }
}
