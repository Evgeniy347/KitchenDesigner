#nullable disable
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>Парсинг и сравнение версий. Чистые функции — без юнити-объектов и I/O.</summary>
public class VersionUtilTests
{
    [Test]
    public void Parse_ThreeParts()
    {
        Assert.IsTrue(VersionUtil.TryParse("1.2.3", out int ma, out int mi, out int b));
        Assert.AreEqual(1, ma); Assert.AreEqual(2, mi); Assert.AreEqual(3, b);
    }

    [Test]
    public void Parse_TwoParts_BuildIsZero()
    {
        Assert.IsTrue(VersionUtil.TryParse("0.632", out int ma, out int mi, out int b));
        Assert.AreEqual(0, ma); Assert.AreEqual(632, mi); Assert.AreEqual(0, b);
    }

    [Test]
    public void Parse_StripsLeadingV_CaseInsensitive()
    {
        Assert.IsTrue(VersionUtil.TryParse("v0.700", out int ma, out int mi, out _));
        Assert.AreEqual(0, ma); Assert.AreEqual(700, mi);
        Assert.IsTrue(VersionUtil.TryParse("V0.700", out _, out int mi2, out _));
        Assert.AreEqual(700, mi2);
    }

    [Test]
    public void Parse_TrimsWhitespace()
    {
        Assert.IsTrue(VersionUtil.TryParse("  0.632  ", out _, out int mi, out _));
        Assert.AreEqual(632, mi);
    }

    [Test]
    public void Parse_MissingTrailingPart_IsZero()
    {
        Assert.IsTrue(VersionUtil.TryParse("2", out int ma, out int mi, out int b));
        Assert.AreEqual(2, ma); Assert.AreEqual(0, mi); Assert.AreEqual(0, b);
    }

    [Test]
    public void Parse_JunkWithNoDigits_Fails()
    {
        Assert.IsFalse(VersionUtil.TryParse("abc", out _, out _, out _));
        Assert.IsFalse(VersionUtil.TryParse("", out _, out _, out _));
        Assert.IsFalse(VersionUtil.TryParse("   ", out _, out _, out _));
    }

    [Test]
    public void Parse_LeadingNumericThenSuffix_AcceptsLeadingNumber()
    {
        // "0.700-beta" -> 0.700 (суффикс отбрасывается, старшие части читаются).
        Assert.IsTrue(VersionUtil.TryParse("0.700-beta", out int ma, out int mi, out _));
        Assert.AreEqual(0, ma); Assert.AreEqual(700, mi);
    }

    [Test]
    public void Compare_Basic()
    {
        Assert.AreEqual(0, VersionUtil.Compare("0.632", "0.632"));
        Assert.AreEqual(1, VersionUtil.Compare("0.700", "0.632"));
        Assert.AreEqual(-1, VersionUtil.Compare("0.632", "0.700"));
        Assert.AreEqual(1, VersionUtil.Compare("1.0.0", "0.999"));
    }

    [Test]
    public void Compare_TreatsVAndZeroPaddingEqual()
    {
        Assert.AreEqual(0, VersionUtil.Compare("v0.700", "0.700"));
        Assert.AreEqual(1, VersionUtil.Compare("v1", "0.999.999"));
    }

    [Test]
    public void Compare_UnparseableBoth_IsEqual_Zero()
    {
        // Обе нераспознаны -> сводятся к 0.0.0 -> 0 (никакого ложного «есть обновление»).
        Assert.AreEqual(0, VersionUtil.Compare("abc", "xyz"));
    }

    [Test]
    public void Parse_NonNumericFirstPart_Fails_SoJunkIsNeverReadAsAVersion()
    {
        Assert.IsFalse(VersionUtil.TryParse("release.5", out _, out _, out _),
            "первая же нечисловая часть — это не версия: иначе «release.5» стало бы "
            + "0.5.0 и сравнение начало бы отвечать всерьёз на мусор");
    }

    [Test]
    public void Compare_UnreadableManifestAgainstReal_IsNeverNewer_SoNoFalseUpdateIsOffered()
    {
        Assert.AreEqual(-1, VersionUtil.Compare("сломанный ответ сервера", "0.662"),
            "нераспознанная строка сводится к 0.0.0 и потому НЕ старше установленной "
            + "версии: битый ответ GitHub не имеет права предложить «обновление» "
            + "на пустое место");
        Assert.AreEqual(1, VersionUtil.Compare("0.662", "сломанный ответ сервера"),
            "равными нераспознанные строки считаются только между собой; "
            + "против настоящего номера они просто нули");
    }
}
