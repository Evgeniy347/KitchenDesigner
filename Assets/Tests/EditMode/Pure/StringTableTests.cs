using System;
using KitchenDesigner.Core;
using NUnit.Framework;

public class StringTableTests
{
    [Test]
    public void Parse_ReadsFlatStringsAndUnescapes()
    {
        var table = StringTable.Parse("ru", "{ \"a.b\": \"Строка \\\"в кавычках\\\"\\nвторая\", \"c\": \"\\u00ab\\u00bb\" }");

        Assert.IsTrue(table.TryGet("a.b", out var text));
        Assert.AreEqual("Строка \"в кавычках\"\nвторая", text);
        Assert.IsTrue(table.TryGet("c", out var quotes));
        Assert.AreEqual("«»", quotes);
    }

    [Test]
    public void Parse_MetaKeys_AreNotStrings()
    {
        var table = StringTable.Parse("ar", "{ \"@name\": \"العربية\", \"@rtl\": true, \"k\": \"v\" }");

        Assert.AreEqual("العربية", table.NativeName);
        Assert.IsTrue(table.IsRightToLeft);
        Assert.AreEqual(1, table.Count, "мета-ключи @name/@rtl описывают язык, а не переводятся");
        Assert.IsFalse(table.TryGet("@name", out _));
    }

    [Test]
    public void NativeName_DefaultsToTheCode()
    {
        Assert.AreEqual("de", StringTable.Parse("de", "{}").NativeName);
    }

    [Test]
    public void Parse_NotAnObject_Throws()
    {
        Assert.Throws<FormatException>(() => StringTable.Parse("ru", "[1, 2]"));
    }

    [Test]
    public void TryGetPlural_FallsBackToOtherThenToTheBareKey()
    {
        var table = StringTable.Parse("ru", "{ \"x\": \"bare\", \"y#other\": \"other\" }");

        Assert.IsTrue(table.TryGetPlural("y", 3, out var other));
        Assert.AreEqual("other", other);
        Assert.IsTrue(table.TryGetPlural("x", 3, out var bare));
        Assert.AreEqual("bare", bare);
    }

    [Test]
    public void BaseKey_StripsThePluralSuffix()
    {
        Assert.AreEqual("errors.count", StringTable.BaseKey("errors.count#few"));
        Assert.AreEqual("errors.count", StringTable.BaseKey("errors.count"));
    }
}
