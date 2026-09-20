using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

/// <summary>
/// Захват привязки не имеет права зафиксировать голый модификатор: пользователь держит
/// Ctrl, поднимает палец — и если бы это уже был аккорд, привязка записалась бы на клавишу,
/// которую он ещё не нажал. Аккорд рождается только вместе с обычной клавишей.
/// </summary>
public class KeyCaptureResolutionTests
{
    [Test]
    public void OrdinaryKey_ResolvesToAChord()
    {
        var chord = KeyCaptureResolution.Resolve(KeyCode.W, ctrl: false, alt: false, shift: false);

        Assert.IsTrue(chord.HasValue);
        Assert.AreEqual(KeyCode.W, chord!.Value.Key);
        Assert.IsFalse(chord.Value.Ctrl);
    }

    [Test]
    public void OrdinaryKeyWithModifiers_CarriesThemIntoTheChord()
    {
        var chord = KeyCaptureResolution.Resolve(KeyCode.D, ctrl: true, alt: false, shift: true);

        Assert.IsTrue(chord.HasValue);
        Assert.AreEqual(KeyCode.D, chord!.Value.Key);
        Assert.IsTrue(chord.Value.Ctrl);
        Assert.IsFalse(chord.Value.Alt);
        Assert.IsTrue(chord.Value.Shift);
    }

    [TestCase(KeyCode.LeftControl)]
    [TestCase(KeyCode.RightControl)]
    [TestCase(KeyCode.LeftAlt)]
    [TestCase(KeyCode.RightAlt)]
    [TestCase(KeyCode.LeftShift)]
    [TestCase(KeyCode.RightShift)]
    public void BareModifierKey_DoesNotResolve(KeyCode modifier)
    {
        var chord = KeyCaptureResolution.Resolve(modifier, ctrl: modifier == KeyCode.LeftControl,
            alt: false, shift: false);

        Assert.IsFalse(chord.HasValue,
            "модификатор один, без обычной клавиши, не обязан становиться привязкой");
    }

    [Test]
    public void NoneKey_DoesNotResolve()
    {
        var chord = KeyCaptureResolution.Resolve(KeyCode.None, false, false, false);
        Assert.IsFalse(chord.HasValue);
    }

    [Test]
    public void IsModifierKey_IsTrueOnlyForModifiers()
    {
        Assert.IsTrue(KeyCaptureResolution.IsModifierKey(KeyCode.LeftControl));
        Assert.IsFalse(KeyCaptureResolution.IsModifierKey(KeyCode.F1));
        Assert.IsFalse(KeyCaptureResolution.IsModifierKey(KeyCode.Escape));
    }
}
