#nullable disable
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Приговор файлу: SHA-256, если релиз его публикует; иначе точный размер; иначе файл
/// непроверяем, и запускать его нельзя. Здесь же — разбор поля digest из ответа GitHub.
/// </summary>
public class InstallerExpectationTests
{
    private static readonly string Sha = new string('a', 64);
    private static readonly string Other = new string('b', 64);

    [Test]
    public void Method_PrefersTheDigest_ThenSize_ThenNothing()
    {
        Assert.AreEqual(IntegrityMethod.Sha256, new InstallerExpectation(Sha, 10).Method);
        Assert.AreEqual(IntegrityMethod.Sha256, new InstallerExpectation(Sha, 0).Method);
        Assert.AreEqual(IntegrityMethod.Size, new InstallerExpectation("", 10).Method);
        Assert.AreEqual(IntegrityMethod.None, new InstallerExpectation("", 0).Method);
        Assert.AreEqual(IntegrityMethod.None, new InstallerExpectation(null, -5).Method);
    }

    [TestCase("abc")]
    [TestCase("zz")]
    public void ADigestOfTheWrongShape_IsNotADigest_SoSizeTakesOver(string broken)
    {
        var expectation = new InstallerExpectation(broken, 10);

        Assert.AreEqual(IntegrityMethod.Size, expectation.Method);
    }

    [Test]
    public void NeedsHash_OnlyForTheDigestMethod()
    {
        Assert.IsTrue(new InstallerExpectation(Sha, 10).NeedsHash);
        Assert.IsFalse(new InstallerExpectation("", 10).NeedsHash);
        Assert.IsFalse(new InstallerExpectation("", 0).NeedsHash);
    }

    [Test]
    public void Judge_Digest_EqualHash_IsOk_RegardlessOfCase()
    {
        var expectation = new InstallerExpectation(Sha.ToUpperInvariant(), 10);

        Assert.AreEqual(IntegrityVerdict.Ok, expectation.Judge(new FileFacts(true, 999, Sha)));
        Assert.AreEqual(IntegrityVerdict.Ok, expectation.Judge(new FileFacts(true, 999, Sha.ToUpperInvariant())));
    }

    [Test]
    public void Judge_Digest_DifferentHash_IsMismatch()
    {
        var expectation = new InstallerExpectation(Sha, 10);

        Assert.AreEqual(IntegrityVerdict.Mismatch, expectation.Judge(new FileFacts(true, 10, Other)));
    }

    [Test]
    public void Judge_Digest_NoHashComputed_IsMismatch_NotOk()
    {
        var expectation = new InstallerExpectation(Sha, 10);

        Assert.AreEqual(IntegrityVerdict.Mismatch, expectation.Judge(new FileFacts(true, 10, null)),
            "файл, который не удалось прочитать, не считается проверенным, даже если размер сошёлся");
    }

    [Test]
    public void Judge_Digest_AbsentFile_IsMissing()
    {
        Assert.AreEqual(IntegrityVerdict.Missing, new InstallerExpectation(Sha, 10).Judge(FileFacts.Absent));
    }

    [Test]
    public void Judge_Size_ExactSizeIsOk_AnyOtherIsMismatch()
    {
        var expectation = new InstallerExpectation("", 500);

        Assert.AreEqual(IntegrityVerdict.Ok, expectation.Judge(new FileFacts(true, 500, null)));
        Assert.AreEqual(IntegrityVerdict.Mismatch, expectation.Judge(new FileFacts(true, 499, null)));
        Assert.AreEqual(IntegrityVerdict.Mismatch, expectation.Judge(new FileFacts(true, 501, null)));
    }

    [Test]
    public void Judge_Size_AbsentFile_IsMissing()
    {
        Assert.AreEqual(IntegrityVerdict.Missing, new InstallerExpectation("", 500).Judge(FileFacts.Absent));
    }

    [Test]
    public void Judge_Nothing_IsNeverOk_EvenForAFileThatExists()
    {
        var expectation = new InstallerExpectation("", 0);

        Assert.AreEqual(IntegrityVerdict.CannotVerify, expectation.Judge(new FileFacts(true, 500, Sha)));
        Assert.AreEqual(IntegrityVerdict.CannotVerify, expectation.Judge(FileFacts.Absent));
    }

    [Test]
    public void FromGitHubDigest_ReadsTheSha256Form_AndLowercasesIt()
    {
        Assert.AreEqual(Sha, Sha256Digest.FromGitHubDigest("sha256:" + Sha));
        Assert.AreEqual(Sha, Sha256Digest.FromGitHubDigest("SHA256:" + Sha.ToUpperInvariant()));
        Assert.AreEqual(Sha, Sha256Digest.FromGitHubDigest("  sha256:" + Sha + "  "));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("sha256:")]
    [TestCase("sha256:abc")]
    [TestCase("sha512:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [TestCase("sha256:gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void FromGitHubDigest_AnythingElse_IsNoDigest(string digest)
    {
        Assert.AreEqual(string.Empty, Sha256Digest.FromGitHubDigest(digest));
    }

    [Test]
    public void ToHex_IsLowercase_AndTwoCharactersPerByte()
    {
        Assert.AreEqual("00ff10ab", Sha256Digest.ToHex(new byte[] { 0x00, 0xFF, 0x10, 0xAB }));
        Assert.AreEqual(string.Empty, Sha256Digest.ToHex(new byte[0]));
    }
}
