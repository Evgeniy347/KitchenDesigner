using NUnit.Framework;
using KitchenDesigner.Core.MCP;

/// <summary>Одно правило сокращения имени для картинки render_plan и любой другой строки, где имени тесно. Правило
/// сохраняет начало (по нему имя узнают как префикс из describe_scene) и ХВОСТ — номер или последние буквы, а не середину.
/// Тест на несимметричных именах: «Cab_Left_Tall_Section_01» и «Cab_Right_Tall_Section_02» совпадают посередине и в голове.</summary>
public class McpNameShorteningTests
{
    [TestCase("Cab01", 5, "Cab01")]
    [TestCase("Cab01", 12, "Cab01")]
    [TestCase("Cabinet_Left_A1", 8, "Cabi..A1")]
    [TestCase("Cab_Left_Tall_Section_01", 12, "Cab_Lef.._01")]
    [TestCase("Cab_Left_Tall_Section", 10, "Cab_Le..on")]
    [TestCase("Module2_Part17", 9, "Modu..t17")]
    public void Fit_KeepsTheHeadAndTheTail(string name, int max, string expected)
    {
        Assert.AreEqual(expected, McpNameShortening.Fit(name, max));
    }

    [Test]
    public void Fit_TheTailIsAlwaysTheEndOfTheName_AndTheHeadTheStart()
    {
        foreach (var name in new[] { "Cab_Left_Tall_Section_01", "Wall_North_Segment_7", "Upper_Cabinet_Left" })
            for (int max = McpNameShortening.MinChars; max < name.Length; max++)
            {
                var shortened = McpNameShortening.Fit(name, max);
                int gap = shortened.IndexOf(McpNameShortening.Gap, System.StringComparison.Ordinal);
                Assert.Greater(gap, 0, shortened);
                StringAssert.StartsWith(shortened.Substring(0, gap), name, "голова — префикс имени из describe_scene");
                StringAssert.EndsWith(shortened.Substring(gap + McpNameShortening.Gap.Length), name, "хвост — суффикс имени");
                Assert.LessOrEqual(shortened.Length, max);
            }
    }

    [Test]
    public void Fit_TheTrailingNumberSurvivesWhatTheBudget()
    {
        for (int max = 8; max < 20; max++)
        {
            StringAssert.EndsWith("_01", McpNameShortening.Fit("Cab_Left_Tall_Section_01", max), "бюджет " + max);
            StringAssert.EndsWith("_02", McpNameShortening.Fit("Cab_Right_Tall_Section_02", max), "бюджет " + max);
        }
    }

    [Test]
    public void Fit_AVeryLongNumber_DoesNotEatTheWholeHead()
    {
        var shortened = McpNameShortening.Fit("Panel_123456789", 8);

        StringAssert.StartsWith("Pa", shortened);
        Assert.LessOrEqual(shortened.Length, 8);
    }

    [Test]
    public void Fit_ANameOfDigitsOnly_DoesNotThrow()
    {
        Assert.AreEqual("12..89", McpNameShortening.Fit("123456789", 6));
    }

    [Test]
    public void Fit_BelowTheMinimum_StillReturnsAtLeastTheMinimum()
    {
        Assert.AreEqual(McpNameShortening.MinChars, McpNameShortening.Fit("Cabinet_Left_A1", 3).Length);
    }
}
