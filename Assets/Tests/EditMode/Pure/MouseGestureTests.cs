using NUnit.Framework;
using KitchenDesigner.Core.Keybinding;

public class MouseGestureTests
{
    [Test]
    public void MouseGesture_Format_PlainClick_IsJustTheButtonToken()
    {
        Assert.AreEqual("LMB", MouseGesture.Format(new MouseGesture(MouseButtonKind.Left)));
        Assert.AreEqual("RMB", MouseGesture.Format(new MouseGesture(MouseButtonKind.Right)));
        Assert.AreEqual("MMB", MouseGesture.Format(new MouseGesture(MouseButtonKind.Middle)));
        Assert.AreEqual("MB4", MouseGesture.Format(new MouseGesture(MouseButtonKind.XButton1)));
        Assert.AreEqual("MB5", MouseGesture.Format(new MouseGesture(MouseButtonKind.XButton2)));
    }

    [Test]
    public void MouseGesture_Format_WithMotion_AppendsMoveSuffix()
    {
        var rmbDrag = new MouseGesture(MouseButtonKind.Right, withMotion: true);
        Assert.AreEqual("RMB+Move", MouseGesture.Format(rmbDrag));
    }

    [Test]
    public void MouseGesture_Format_Wheel_HasNoButtonAndIgnoresMotion()
    {
        Assert.AreEqual("Wheel", MouseGesture.Format(MouseGesture.Wheel()));
    }

    [Test]
    public void MouseGesture_Format_ModifiersComeBeforeTheBase_InFixedOrder()
    {
        var chord = new MouseGesture(MouseButtonKind.Left, ctrl: true, alt: true, shift: true);
        Assert.AreEqual("Ctrl+Alt+Shift+LMB", MouseGesture.Format(chord));
    }

    [Test]
    public void MouseGesture_Format_CtrlLeftClick_MatchesTheMultiSelectDefault()
    {
        var gesture = new MouseGesture(MouseButtonKind.Left, ctrl: true);
        Assert.AreEqual("Ctrl+LMB", MouseGesture.Format(gesture));
    }

    [Test]
    public void MouseGesture_Format_Empty_IsEmptyString()
    {
        Assert.AreEqual("", MouseGesture.Format(MouseGesture.Empty));
    }

    [TestCase("LMB")]
    [TestCase("RMB")]
    [TestCase("MMB")]
    [TestCase("MB4")]
    [TestCase("MB5")]
    [TestCase("Wheel")]
    [TestCase("RMB+Move")]
    [TestCase("MMB+Move")]
    [TestCase("Ctrl+LMB")]
    [TestCase("Ctrl+Alt+Shift+LMB")]
    [TestCase("Ctrl+RMB+Move")]
    public void MouseGesture_Format_RoundTripsThroughParse(string canonicalText)
    {
        var parsed = MouseGesture.Parse(canonicalText);
        Assert.AreEqual(canonicalText, MouseGesture.Format(parsed),
            $"текст «{canonicalText}» обязан пережить разбор и обратную печать без изменений");
    }

    [Test]
    public void MouseGesture_Parse_Empty_ReturnsEmptyGesture()
    {
        Assert.IsTrue(MouseGesture.TryParse("", out var gesture));
        Assert.IsTrue(gesture.IsEmpty);

        Assert.IsTrue(MouseGesture.TryParse(null, out var fromNull));
        Assert.IsTrue(fromNull.IsEmpty);
    }

    [Test]
    public void MouseGesture_Parse_UnknownButtonToken_Fails()
    {
        Assert.IsFalse(MouseGesture.TryParse("MB9", out _));
    }

    [Test]
    public void MouseGesture_Parse_WheelWithMove_Fails()
    {
        Assert.IsFalse(MouseGesture.TryParse("Wheel+Move", out _),
            "у колёсика нет отдельного «с движением» — это бессмысленное сочетание");
    }

    [Test]
    public void MouseGesture_Equality_DistinguishesClickFromDrag_OnTheSameButton()
    {
        var click = new MouseGesture(MouseButtonKind.Right);
        var drag = new MouseGesture(MouseButtonKind.Right, withMotion: true);

        Assert.AreNotEqual(click, drag,
            "ПКМ-клик и ПКМ+движение — разные жесты, это и есть то самое различие "
            + "между просто ПКМ и «ПКМ + движение — поворот камеры»");
    }

    [Test]
    public void MouseGesture_IsEmpty_TrueOnlyForTheDefaultValue()
    {
        Assert.IsTrue(MouseGesture.Empty.IsEmpty);
        Assert.IsFalse(new MouseGesture(MouseButtonKind.Left).IsEmpty);
        Assert.IsFalse(MouseGesture.Wheel().IsEmpty);
    }
}
