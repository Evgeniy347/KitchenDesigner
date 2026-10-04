using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class ClockTimeTests
{
    [TestCase(12f, "12:00")]
    [TestCase(0f, "00:00")]
    [TestCase(6.5f, "06:30")]
    [TestCase(24f, "24:00")]
    [TestCase(13.999f, "14:00")]
    public void Format_WritesHoursAndMinutes_WithLeadingZeros(float hours, string expected)
    {
        Assert.AreEqual(expected, ClockTime.Format(hours),
            "поле времени «День / Ночь» показывает ЧЧ:ММ, а не десятичные часы: так человек вводит время");
    }

    [Test]
    public void Format_NeverShows60Minutes()
    {
        Assert.AreEqual("14:00", ClockTime.Format(13.9999f),
            "округление минут до 60 переносится в час, иначе на экране «13:60»");
    }

    [TestCase("12:00", 12f)]
    [TestCase("6:30", 6.5f)]
    [TestCase("06:30", 6.5f)]
    [TestCase("18", 18f)]
    [TestCase(" 7:15 ", 7.25f)]
    [TestCase("24:00", 24f)]
    [TestCase("0:00", 0f)]
    public void TryParse_ReadsHoursWithOptionalMinutes(string text, float expected)
    {
        Assert.IsTrue(ClockTime.TryParse(text, out float hours), $"«{text}» — допустимое время");
        Assert.AreEqual(expected, hours, 1e-4f);
    }

    [TestCase("12.30", 12.5f)]
    [TestCase("12,30", 12.5f)]
    public void TryParse_TakesADotOrACommaAsTheMinutesSeparatorToo(string text, float expected)
    {
        Assert.IsTrue(ClockTime.TryParse(text, out float hours));
        Assert.AreEqual(expected, hours, 1e-4f,
            "«12.30» на клавиатуре с точкой — это половина первого, а не двенадцать с третью часа");
    }

    [TestCase("")]
    [TestCase("  ")]
    [TestCase("abc")]
    [TestCase("25:00")]
    [TestCase("24:30")]
    [TestCase("12:60")]
    [TestCase("12:5x")]
    [TestCase("-1:00")]
    [TestCase("1:2:3")]
    public void TryParse_RejectsWhatIsNotATimeOfDay(string text)
    {
        Assert.IsFalse(ClockTime.TryParse(text, out _),
            $"«{text}» вне суток или не время — поле подсвечивает ошибку и возвращает прежнее");
    }

    [Test]
    public void Format_ThenParse_RoundTripsEveryMinuteOfTheDay()
    {
        for (int minute = 0; minute <= 24 * 60; minute++)
        {
            float hours = minute / 60f;
            Assert.IsTrue(ClockTime.TryParse(ClockTime.Format(hours), out float back), $"минута {minute}");
            Assert.AreEqual(hours, back, 1e-3f, $"минута {minute}");
        }
    }
}
