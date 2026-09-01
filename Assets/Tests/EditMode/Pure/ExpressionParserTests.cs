using System.Globalization;
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

    // ── EvaluateInt: умножение и деление ────────────────────────

    [Test]
    public void EvaluateInt_SimpleMultiplication()
    {
        Assert.AreEqual(50, ExpressionParser.EvaluateInt("10*5"));
    }

    [Test]
    public void EvaluateInt_SimpleDivision()
    {
        Assert.AreEqual(5, ExpressionParser.EvaluateInt("20/4"));
    }

    [Test]
    public void EvaluateInt_MixedAllFourOperators()
    {
        Assert.AreEqual(18, ExpressionParser.EvaluateInt("2+3*4-2"));
    }

    [Test]
    public void EvaluateInt_MultiplyByZero()
    {
        Assert.AreEqual(0, ExpressionParser.EvaluateInt("100*0"));
    }

    [Test]
    public void EvaluateInt_DivideThenMultiply()
    {
        Assert.AreEqual(60, ExpressionParser.EvaluateInt("100/5*3"));
    }

    [Test]
    public void EvaluateInt_LeadingMultiply_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateInt("*100"));
    }

    [Test]
    public void EvaluateInt_LeadingSlash_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateInt("/100"));
    }

    [Test]
    public void EvaluateInt_TrailingMultiply_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateInt("100*"));
    }

    [Test]
    public void EvaluateInt_TrailingSlash_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateInt("100/"));
    }

    [Test]
    public void EvaluateInt_DivideByZero_ReturnsZero()
    {
        Assert.AreEqual(ExpressionParser.DivisionByZeroResultInt, ExpressionParser.EvaluateInt("10/0"),
            "поле размера не имеет права выбросить DivideByZeroException в лицо пользователю: "
            + "деление на ноль даёт оговорённый результат, а не отказ");
    }

    [Test]
    public void EvaluateInt_MultiplyWithSpaces()
    {
        Assert.AreEqual(120, ExpressionParser.EvaluateInt(" 3 * 40 "));
    }

    // ── EvaluateFloat: умножение и деление ───────────────────────

    [Test]
    public void EvaluateFloat_SimpleMultiplication()
    {
        Assert.AreEqual(10.0f, ExpressionParser.EvaluateFloat("2.5*4"), 0.001f);
    }

    [Test]
    public void EvaluateFloat_SimpleDivision()
    {
        Assert.AreEqual(5.0f, ExpressionParser.EvaluateFloat("15.0/3"), 0.001f);
    }

    [Test]
    public void EvaluateFloat_MixedAllFourOperators()
    {
        Assert.AreEqual(16.0f, ExpressionParser.EvaluateFloat("10.0/2*3+1"), 0.001f);
    }

    [Test]
    public void EvaluateFloat_MultiplyWithSpaces()
    {
        Assert.AreEqual(12.0f, ExpressionParser.EvaluateFloat(" 3.0 * 4 "), 0.001f);
    }

    [Test]
    public void EvaluateFloat_LeadingMultiply_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateFloat("*5.0"));
    }

    [Test]
    public void EvaluateFloat_LeadingSlash_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateFloat("/5.0"));
    }

    [Test]
    public void EvaluateFloat_DivideByZero_ReturnsZero()
    {
        Assert.AreEqual(ExpressionParser.DivisionByZeroResultFloat,
            ExpressionParser.EvaluateFloat("10.0/0"), 0.001f,
            "дробная ветка обязана вести себя так же, как целая: тот же оговорённый результат");
    }

    [Test]
    public void EvaluateFloat_IntDivisionTruncates()
    {
        Assert.AreEqual(3, ExpressionParser.EvaluateInt("7/2"));
    }

    [Test]
    public void EvaluateFloat_FloatDivision()
    {
        Assert.AreEqual(3.5f, ExpressionParser.EvaluateFloat("7.0/2"), 0.001f);
    }

    [Test]
    public void EvaluateFloat_DoubleDot_ReturnsNull()
    {
        Assert.IsNull(ExpressionParser.EvaluateFloat("1.2.3"));
    }

    // ── IsValidDimensionChar: фильтр ввода UI ────────────────────

    [Test]
    public void IsValidDimensionChar_AllowsDigit()
    {
        Assert.IsTrue(ExpressionParser.IsValidDimensionChar('5'));
    }

    [Test]
    public void IsValidDimensionChar_AllowsPlus()
    {
        Assert.IsTrue(ExpressionParser.IsValidDimensionChar('+'));
    }

    [Test]
    public void IsValidDimensionChar_AllowsMinus()
    {
        Assert.IsTrue(ExpressionParser.IsValidDimensionChar('-'));
    }

    [Test]
    public void IsValidDimensionChar_AllowsMultiply()
    {
        Assert.IsTrue(ExpressionParser.IsValidDimensionChar('*'));
    }

    [Test]
    public void IsValidDimensionChar_AllowsDivide()
    {
        Assert.IsTrue(ExpressionParser.IsValidDimensionChar('/'));
    }

    [Test]
    public void IsValidDimensionChar_AllowsSpace()
    {
        Assert.IsTrue(ExpressionParser.IsValidDimensionChar(' '));
    }

    [Test]
    public void IsValidDimensionChar_RejectsDotByDefault()
    {
        Assert.IsFalse(ExpressionParser.IsValidDimensionChar('.'));
    }

    [Test]
    public void IsValidDimensionChar_AllowsDotWhenDecimal()
    {
        Assert.IsTrue(ExpressionParser.IsValidDimensionChar('.', allowDecimal: true));
    }

    [Test]
    public void IsValidDimensionChar_RejectsLetter()
    {
        Assert.IsFalse(ExpressionParser.IsValidDimensionChar('a'));
        Assert.IsFalse(ExpressionParser.IsValidDimensionChar('Z'));
    }

    [Test]
    public void EvaluateFloat_UnderARussianLocale_StillReadsTheDotAsADecimalSeparator()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            Assert.AreEqual(2.5f, ExpressionParser.EvaluateFloat("2.5"), 0.001f,
                "поле UI принимает точку на любой машине: если разбор пойдёт по текущей "
                + "культуре, под русской локалью 2.5 прочитается как 25");
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
