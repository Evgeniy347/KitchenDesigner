using System.Collections.Generic;
using System.IO;
using System.Linq;
using KitchenDesigner.Core;
using NUnit.Framework;

public class NumberFormatTests
{
    [TestCase(",", "1,0")]
    [TestCase(".", "1.0")]
    public void NumberFormat_Fixed_UsesTheSeparatorOfTheLanguage_NotOfTheMachine(string separator,
        string expected)
    {
        Assert.AreEqual(expected, NumberFormat.Fixed(1.0, 1, separator),
            "Десятичный знак берётся из языка интерфейса: на русской машине с английским "
            + "интерфейсом «1,0» рядом с английскими подписями — тот же разнобой, что был "
            + "между «1.0» кромки и «0,0» угла в одной панели (D3)");
    }

    [Test]
    public void NumberFormat_Fixed_RoundsHalfAwayFromZero_ForBothSigns()
    {
        Assert.AreEqual("26,8", NumberFormat.Fixed(26.75, 1, ","));
        Assert.AreEqual(NumberFormat.Minus + "26,8", NumberFormat.Fixed(-26.75, 1, ","),
            "отрицательное округляется зеркально положительному, иначе «−0,05» и «0,05» "
            + "расходятся на десятую");
    }

    [Test]
    public void NumberFormat_Fixed_WritesTheTypographicMinus_NotTheHyphen()
    {
        string text = NumberFormat.Fixed(-300, 0, ",");
        Assert.AreEqual(NumberFormat.Minus, text[0],
            "минус — U+2212: дефис «-» короче и висит ниже цифр, «-300» читается как тире (D3)");
        Assert.AreEqual(NumberFormat.Minus + "300", text);
    }

    [Test]
    public void NumberFormat_Fixed_ValueRoundingToZero_HasNoSign()
    {
        Assert.AreEqual("0,0", NumberFormat.Fixed(-0.04, 1, ","),
            "«−0,0» — ноль со знаком: человек ищет, откуда минус, а его нет");
        Assert.AreEqual("0", NumberFormat.Fixed(-0.4, 0, ","));
    }

    [Test]
    public void NumberFormat_Input_KeepsTheAsciiMinus_SoTheFieldStaysTypeable()
    {
        Assert.AreEqual("-12,5", NumberFormat.Input(-12.5, 1, ","),
            "В поле ввода минус — тот, что на клавиатуре: калькулятор поля (ExpressionParser) "
            + "считает «-» операцией, а «−» — посторонним символом");
        Assert.AreEqual("12,5", NumberFormat.Input(12.5, 1, ","));
    }

    [Test]
    public void NumberFormat_Compact_DropsTheTrailingZero_AndKeepsTheTenth()
    {
        Assert.AreEqual("48", NumberFormat.Compact(48.0, 1, ","),
            "«48,0» читается как точность, которой нет");
        Assert.AreEqual("26,8", NumberFormat.Compact(26.8f, 1, ","));
        Assert.AreEqual("2.8", NumberFormat.Compact(2.8f, 1, "."));
        Assert.AreEqual("100", NumberFormat.Compact(100.0, 2, ","),
            "нули целой части не обрезаются — режется только дробная");
    }

    [Test]
    public void NumberFormat_WithUnit_JoinsByANonBreakingSpace()
    {
        string text = NumberFormat.WithUnit("800", "мм");
        Assert.AreEqual("800" + NumberFormat.UnitGap + "мм", text,
            "единица через неразрывный пробел: «мм» не переносится на другую строку и не "
            + "липнет к числу («6000мм» в «Этажах»). Узкий пробел U+202F вне WGL4 — его нет в "
            + "атласе TMP, он нарисовался бы пустым квадратом");
        Assert.AreEqual("800", NumberFormat.WithUnit("800", ""));
    }

    [TestCase("12,5", 12.5)]
    [TestCase("12.5", 12.5)]
    [TestCase("−12,5", -12.5)]
    [TestCase("-12.5", -12.5)]
    [TestCase(" 7 ", 7.0)]
    public void NumberFormat_TryParse_AcceptsEitherSeparator_AndEitherMinus(string text, double expected)
    {
        Assert.IsTrue(NumberFormat.TryParse(text, out double value),
            "поле, показанное форматтером, обязано читаться обратно на любом языке: "
            + "английский интерфейс на русской машине давал «0.5», а float.TryParse по "
            + "культуре машины его отвергал");
        Assert.AreEqual(expected, value, 1e-9);
    }

    [Test]
    public void NumberFormat_TryParse_ReadsANumberThatStillCarriesTheUnitGap()
    {
        Assert.IsTrue(NumberFormat.TryParse("800" + NumberFormat.UnitGap, out double value));
        Assert.AreEqual(800.0, value, 1e-9);
    }

    [Test]
    public void NumberFormat_TryParse_RejectsText()
    {
        Assert.IsFalse(NumberFormat.TryParse("abc", out _));
        Assert.IsFalse(NumberFormat.TryParse("", out _));
        Assert.IsFalse(NumberFormat.TryParse(null, out _));
    }

    [Test]
    public void NumberFormat_RoundTrip_OfItsOwnOutput_ForEveryShippedSeparator()
    {
        foreach (var separator in new[] { ",", "." })
        foreach (var value in new[] { -1234.5, -0.5, 0.0, 0.1, 26.8, 9999.9 })
        {
            Assert.IsTrue(NumberFormat.TryParse(NumberFormat.Fixed(value, 1, separator), out double back));
            Assert.AreEqual(value, back, 1e-9, "Fixed → TryParse, separator " + separator);
            Assert.IsTrue(NumberFormat.TryParse(NumberFormat.Input(value, 1, separator), out back));
            Assert.AreEqual(value, back, 1e-9, "Input → TryParse, separator " + separator);
        }
    }

    [Test]
    public void NumberFormat_TryParseInt_ReadsTheTypographicMinus()
    {
        Assert.IsTrue(NumberFormat.TryParseInt(NumberFormat.Minus + "300", out int v));
        Assert.AreEqual(-300, v);
        Assert.IsFalse(NumberFormat.TryParseInt("1,5", out _), "дробное в целом поле — отказ, а не усечение");
    }

    [Test]
    public void StringTable_DecimalSeparator_ComesFromTheDecimalMetaKey_DotWhenAbsent()
    {
        var withComma = new StringTable("xx", new Dictionary<string, string> { ["@decimal"] = "," });
        var without = new StringTable("yy", new Dictionary<string, string>());
        Assert.AreEqual(",", withComma.DecimalSeparator);
        Assert.AreEqual(".", without.DecimalSeparator);
        Assert.IsFalse(withComma.Keys.Contains("@decimal"), "мета-ключ — не строка интерфейса");
    }

    [Test]
    public void Localizer_DecimalSeparator_FollowsTheCurrentLanguage()
    {
        var ru = new StringTable("ru", new Dictionary<string, string> { ["@decimal"] = "," });
        var en = new StringTable("en", new Dictionary<string, string> { ["@decimal"] = "." });
        var localizer = new Localizer(new[] { ru, en }, "ru");
        Assert.AreEqual(",", localizer.DecimalSeparator);
        Assert.AreEqual(".", localizer.WithLanguage("en").DecimalSeparator);
    }

    private static readonly Dictionary<string, string> ShippedSeparators = new()
    {
        ["ru"] = ",", ["en"] = ".", ["de"] = ",", ["es"] = ",", ["fr"] = ",", ["it"] = ",",
        ["pt"] = ",", ["ja"] = ".", ["zh-Hans"] = ".", ["ar-TN"] = ",",
    };

    [Test]
    public void EveryShippedLanguageFile_DeclaresItsDecimalSeparator_PerCldr()
    {
        var dir = LocalizationFiles.Resolve();
        Assert.IsNotNull(dir, "папка Localization не найдена — скан проверил бы пустоту");
        var tables = LocalizationFiles.LoadAll(dir!);
        Assert.GreaterOrEqual(tables.Count, 10, "скан обязан видеть все десять языков");

        var wrong = new List<string>();
        foreach (var table in tables)
        {
            string raw = File.ReadAllText(Path.Combine(dir!, table.Language + ".json"));
            if (!raw.Contains("\"@decimal\""))
                wrong.Add(table.Language + ": нет @decimal");
            else if (!ShippedSeparators.TryGetValue(table.Language, out var expected))
                wrong.Add(table.Language + ": новый язык — впиши его знак по CLDR сюда");
            else if (table.DecimalSeparator != expected)
                wrong.Add(table.Language + ": " + table.DecimalSeparator + " вместо " + expected);
        }

        Assert.IsEmpty(wrong,
            "Каждый файл языка объявляет свой десятичный знак (@decimal) — тихий откат на «.» "
            + "дал бы немцу и французу «1.5» в полях. Источник — CLDR: запятая у ru/de/es/fr/"
            + "it/pt/ar-TN, точка у en/ja/zh-Hans:\n" + string.Join("\n", wrong));
    }
}
