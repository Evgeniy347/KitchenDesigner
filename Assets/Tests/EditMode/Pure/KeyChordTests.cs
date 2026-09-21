using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;

public class KeyChordTests
{
    [Test]
    public void KeyChord_Format_PlainLetter_HasNoModifierPrefix()
    {
        var chord = new KeyChord(KeyCode.W);
        Assert.AreEqual("W", KeyChord.Format(chord));
    }

    [Test]
    public void KeyChord_Format_CtrlShiftZ_MatchesFixedModifierOrder()
    {
        var chord = new KeyChord(KeyCode.Z, ctrl: true, shift: true);
        Assert.AreEqual("Ctrl+Shift+Z", KeyChord.Format(chord),
            "порядок модификаторов в тексте — часть формата, а не случайность enum");
    }

    [Test]
    public void KeyChord_Format_FunctionKey_UsesItsOwnName()
    {
        Assert.AreEqual("F1", KeyChord.Format(new KeyChord(KeyCode.F1)));
    }

    [Test]
    public void KeyChord_Format_ArrowKeys_UseArrowGlyphs()
    {
        Assert.AreEqual("←", KeyChord.Format(new KeyChord(KeyCode.LeftArrow)));
        Assert.AreEqual("→", KeyChord.Format(new KeyChord(KeyCode.RightArrow)));
        Assert.AreEqual("↑", KeyChord.Format(new KeyChord(KeyCode.UpArrow)));
        Assert.AreEqual("↓", KeyChord.Format(new KeyChord(KeyCode.DownArrow)));
    }

    [Test]
    public void KeyChord_Format_Empty_IsEmptyString()
    {
        Assert.AreEqual("", KeyChord.Format(KeyChord.Empty));
    }

    [Test]
    public void KeyChord_Parse_Empty_ReturnsEmptyChord()
    {
        Assert.IsTrue(KeyChord.TryParse("", out var chord));
        Assert.IsTrue(chord.IsEmpty);

        Assert.IsTrue(KeyChord.TryParse(null, out var fromNull));
        Assert.IsTrue(fromNull.IsEmpty);
    }

    [TestCase("W")]
    [TestCase("F1")]
    [TestCase("F9")]
    [TestCase("Delete")]
    [TestCase("Ctrl+D")]
    [TestCase("Ctrl+Shift+Z")]
    [TestCase("Ctrl+Y")]
    [TestCase("←")]
    [TestCase("→")]
    [TestCase("↑")]
    [TestCase("↓")]
    [TestCase("1")]
    [TestCase("=")]
    [TestCase("-")]
    [TestCase("`")]
    [TestCase("/")]
    public void KeyChord_Format_RoundTripsThroughParse(string canonicalText)
    {
        var parsed = KeyChord.Parse(canonicalText);
        Assert.AreEqual(canonicalText, KeyChord.Format(parsed),
            $"текст «{canonicalText}» обязан пережить разбор и обратную печать без изменений");
    }

    [Test]
    public void KeyChord_Parse_RoundTripsThroughFormat_ForEveryDefaultKeyBinding()
    {
        foreach (var action in InputActionCatalog.All)
        {
            var primary = KeyBindingDefaults.PrimaryOf(action);
            if (!primary.IsKey) continue;

            var text = KeyChord.Format(primary.Key);
            var reparsed = KeyChord.Parse(text);

            Assert.AreEqual(primary.Key, reparsed,
                $"привязка по умолчанию для {action} обязана пережить печать в текст и разбор обратно");
        }
    }

    [Test]
    public void KeyChord_Parse_UnknownModifierToken_Fails()
    {
        Assert.IsFalse(KeyChord.TryParse("Meta+W", out _),
            "неизвестный модификатор не должен молча превращаться в пустую привязку");
    }

    [Test]
    public void KeyChord_Equality_IgnoresNothingButComparesAllFields()
    {
        var a = new KeyChord(KeyCode.Z, ctrl: true);
        var b = new KeyChord(KeyCode.Z, ctrl: true);
        var c = new KeyChord(KeyCode.Z, ctrl: true, shift: true);

        Assert.AreEqual(a, b);
        Assert.AreNotEqual(a, c, "тот же ключ с другим модификатором — другая привязка");
    }

    [Test]
    public void KeyChord_IsEmpty_TrueOnlyWhenKeyIsNone()
    {
        Assert.IsTrue(KeyChord.Empty.IsEmpty);
        Assert.IsFalse(new KeyChord(KeyCode.W).IsEmpty);
    }
}
