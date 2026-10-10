using System;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

public class UpdateStringsTests
{
    private const string SomeVersion = "0.700";

    [Test]
    public void UpdateMessage_CarriesExactlyOnePlaceholder_AndItIsTheVersionNumber()
    {
        string formatted = string.Format(UpdateStrings.UpdateMessage, SomeVersion);

        StringAssert.Contains(SomeVersion, formatted,
            "единственная подстановка — номер новой версии, и она обязана доехать до пользователя");
        Assert.AreEqual(-1, formatted.IndexOf('{'),
            "лишний {N} в шаблоне не подставится и уедет на экран как есть");
        Assert.AreEqual(1, CountOccurrences(formatted, SomeVersion),
            "версия называется ровно один раз — второй {0} означает, "
            + "что кто-то дописал в шаблон поле, для которого нет аргумента");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, at = 0;
        while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += needle.Length;
        }
        return count;
    }

    [Test]
    public void EveryUserFacingString_IsFilledIn()
    {
        Assert.IsNotEmpty(UpdateStrings.UpdateTitle);
        Assert.IsNotEmpty(UpdateStrings.UpdateAcceptButton);
        Assert.IsNotEmpty(UpdateStrings.UpdateCancelButton);
    }
}
