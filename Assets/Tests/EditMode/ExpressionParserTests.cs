using NUnit.Framework;
using KitchenDesigner.Core.UI;

/// <summary>
/// Тесты ExpressionParser: простая арифметика (+ и -) в числовых полях UI.
/// Поддержка пробелов, унарных знаков, цепочек операторов.
/// </summary>
public class ExpressionParserTests
{
    // ── EvaluateInt: базовые случаи ──────────────────────────────

    [Test]
    public void EvaluateInt_SingleNumber_ReturnsNumber()
    {
        Assert.AreEqual(1234, ExpressionParser.EvaluateInt("1234"));
    }

    [Test]
    public void EvaluateInt_SimpleAddition()
    {
        Assert.AreEqual(1290, ExpressionParser.EvaluateInt("1234+56"));
    }

    [Test]
    public void EvaluateInt_SimpleSubtraction()
    {
        Assert.AreEqual(700, ExpressionParser.EvaluateInt("800-100"));
    }

    [Test]
    public void EvaluateInt_NegativeResult()
    {
        Assert.AreEqual(-200, ExpressionParser.EvaluateInt("300-500"));
    }

    // ── EvaluateInt: унарные знаки ───────────────────────────────

    [Test]
    public void EvaluateInt_LeadingPlus()
    {
        Assert.AreEqual(1234, ExpressionParser.EvaluateInt("+1234"));
    }

    [Test]
    public void EvaluateInt_LeadingMinus()
    {
        Assert.AreEqual(-50, ExpressionParser.EvaluateInt("-50"));
    }

    [Test]
    public void EvaluateInt_UnaryMinusThenAdd()
    {
        Assert.AreEqual(50, ExpressionParser.EvaluateInt("-50+100"));
    }

    [Test]
    public void EvaluateInt_UnaryPlusThenSubtract()
    {
        Assert.AreEqual(950, ExpressionParser.EvaluateInt("+1000-50"));
    }

    // ── EvaluateInt: цепочки операторов ──────────────────────────

    [Test]
    public void EvaluateInt_MultipleOps()
    {
        Assert.AreEqual(28, ExpressionParser.EvaluateInt("10+20-5+3"));
    }

    [Test]
    public void EvaluateInt_AllSubtractions()
    {
        Assert.AreEqual(970, ExpressionParser.EvaluateInt("1000-10-10-10"));
    }

    [Test]
    public void EvaluateInt_AllAdditions()
    {
        Assert.AreEqual(150, ExpressionParser.EvaluateInt("50+50+50"));
    }

    // ── EvaluateInt: пробелы ─────────────────────────────────────

    [Test]
    public void EvaluateInt_SpacesStripped()
    {
        Assert.AreEqual(1290, ExpressionParser.EvaluateInt(" 1234 + 56 "));
    }

    [Test]
    public void EvaluateInt_SpacesEverywhere()
    {
        Assert.AreEqual(28, ExpressionParser.EvaluateInt(" 10 + 20 - 5 + 3 "));
    }

    [Test]
    public void EvaluateInt_LeadingSpaceOnly()
    {
        Assert.AreEqual(100, ExpressionParser.EvaluateInt(" 100"));
    }

    // ── EvaluateInt: невалидный ввод ─────────────────────────────

    [Test]
    public void EvaluateInt_Empty_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateInt(""));
    }

    [Test]
    public void EvaluateInt_Null_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateInt(null!));
    }

    [Test]
    public void EvaluateInt_OnlyOperator_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateInt("+"));
    }

    [Test]
    public void EvaluateInt_DoubleOperatorAtEnd_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateInt("100+"));
    }

    [Test]
    public void EvaluateInt_InvalidChars_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateInt("abc"));
    }

    [Test]
    public void EvaluateInt_MixedInvalidChars_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateInt("12a34"));
    }

    // ── EvaluateInt: edge cases ──────────────────────────────────

    [Test]
    public void EvaluateInt_Zero()
    {
        Assert.AreEqual(0, ExpressionParser.EvaluateInt("0"));
    }

    [Test]
    public void EvaluateInt_LargeNumbers()
    {
        Assert.AreEqual(3000, ExpressionParser.EvaluateInt("1000+2000"));
    }

    [Test]
    public void EvaluateInt_ZeroPlusSomething()
    {
        Assert.AreEqual(42, ExpressionParser.EvaluateInt("0+42"));
    }

    [Test]
    public void EvaluateInt_OperatorOnlyTwice_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateInt("++"));
    }

    // ── EvaluateFloat: базовые случаи ────────────────────────────

    [Test]
    public void EvaluateFloat_SingleNumber()
    {
        Assert.AreEqual(45.5f, ExpressionParser.EvaluateFloat("45.5"), 0.001f);
    }

    [Test]
    public void EvaluateFloat_SimpleAddition()
    {
        Assert.AreEqual(136.0f, ExpressionParser.EvaluateFloat("45.5+90.5"), 0.001f);
    }

    [Test]
    public void EvaluateFloat_SimpleSubtraction()
    {
        Assert.AreEqual(3.7f, ExpressionParser.EvaluateFloat("5.2-1.5"), 0.001f);
    }

    [Test]
    public void EvaluateFloat_LeadingMinus()
    {
        Assert.AreEqual(-2.5f, ExpressionParser.EvaluateFloat("-2.5"), 0.001f);
    }

    [Test]
    public void EvaluateFloat_UnaryMinusThenAdd()
    {
        Assert.AreEqual(7.5f, ExpressionParser.EvaluateFloat("-2.5+10"), 0.001f);
    }

    [Test]
    public void EvaluateFloat_MultipleOps()
    {
        Assert.AreEqual(15.0f, ExpressionParser.EvaluateFloat("10.5+5.0-0.5"), 0.001f);
    }

    [Test]
    public void EvaluateFloat_SpacesStripped()
    {
        Assert.AreEqual(136.0f, ExpressionParser.EvaluateFloat(" 45.5 + 90.5 "), 0.001f);
    }

    [Test]
    public void EvaluateFloat_Empty_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateFloat(""));
    }

    [Test]
    public void EvaluateFloat_Null_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateFloat(null!));
    }

    [Test]
    public void EvaluateFloat_InvalidChars_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateFloat("abc"));
    }

    [Test]
    public void EvaluateFloat_IntegerInput()
    {
        Assert.AreEqual(100f, ExpressionParser.EvaluateFloat("100"), 0.001f);
    }

    [Test]
    public void EvaluateFloat_MixedIntAndFloat()
    {
        Assert.AreEqual(105.5f, ExpressionParser.EvaluateFloat("100+5.5"), 0.001f);
    }

    [Test]
    public void EvaluateFloat_DoubleDot_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateFloat("1.2.3"));
    }
}
