using KitchenDesigner.Core;
using NUnit.Framework;

public class PluralRulesTests
{
    [TestCase(0, PluralCategory.Many)]
    [TestCase(1, PluralCategory.One)]
    [TestCase(2, PluralCategory.Few)]
    [TestCase(4, PluralCategory.Few)]
    [TestCase(5, PluralCategory.Many)]
    [TestCase(11, PluralCategory.Many)]
    [TestCase(12, PluralCategory.Many)]
    [TestCase(14, PluralCategory.Many)]
    [TestCase(21, PluralCategory.One)]
    [TestCase(22, PluralCategory.Few)]
    [TestCase(111, PluralCategory.Many)]
    [TestCase(101, PluralCategory.One)]
    public void For_Russian_FollowsCldr(long n, PluralCategory expected)
    {
        Assert.AreEqual(expected, PluralRules.For("ru", n));
    }

    [TestCase(0, PluralCategory.Zero)]
    [TestCase(1, PluralCategory.One)]
    [TestCase(2, PluralCategory.Two)]
    [TestCase(3, PluralCategory.Few)]
    [TestCase(10, PluralCategory.Few)]
    [TestCase(11, PluralCategory.Many)]
    [TestCase(99, PluralCategory.Many)]
    [TestCase(100, PluralCategory.Other)]
    [TestCase(102, PluralCategory.Other)]
    [TestCase(103, PluralCategory.Few)]
    public void For_Arabic_FollowsCldr(long n, PluralCategory expected)
    {
        Assert.AreEqual(expected, PluralRules.For("ar", n));
    }

    [TestCase(0, PluralCategory.Other)]
    [TestCase(1, PluralCategory.One)]
    [TestCase(2, PluralCategory.Other)]
    public void For_English_IsOneOrOther(long n, PluralCategory expected)
    {
        Assert.AreEqual(expected, PluralRules.For("en", n));
    }

    [Test]
    public void For_NegativeCount_UsesTheMagnitude()
    {
        Assert.AreEqual(PluralCategory.One, PluralRules.For("ru", -21));
    }
}
