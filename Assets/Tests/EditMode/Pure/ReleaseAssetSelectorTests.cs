#nullable disable
using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Выбор установщика из ассетов релиза и перенос размера и SHA-256 из ответа GitHub в манифест.
/// Данные — обычные объекты, без JSON и сети; сам JSON разбирает ReleaseManifestParser.
/// </summary>
public class ReleaseAssetSelectorTests
{
    private static readonly string Sha = new string('c', 64);

    private static ReleaseAssetInfo Asset(string name, string url = "https://gh/x", long size = 0, string digest = "") =>
        new ReleaseAssetInfo { Name = name, DownloadUrl = url, Size = size, Digest = digest };

    private static ReleaseLookup Select(string tag, params ReleaseAssetInfo[] assets) =>
        ReleaseAssetSelector.Select(tag, assets);

    [Test]
    public void Found_CarriesVersionUrlNameSizeAndDigest()
    {
        var lookup = Select("v0.700",
            Asset("KitchenDesigner-Setup-0.700-x64.exe", "https://gh/i.exe", 60_000_000, "sha256:" + Sha.ToUpperInvariant()));

        Assert.AreEqual(ReleaseLookupStatus.Found, lookup.Status);
        Assert.AreEqual("0.700", lookup.Version);
        Assert.AreEqual("0.700", lookup.Manifest.Version);
        Assert.AreEqual("KitchenDesigner-Setup-0.700-x64.exe", lookup.Manifest.FileName);
        Assert.AreEqual("https://gh/i.exe", lookup.Manifest.DownloadUrl);
        Assert.AreEqual(60_000_000, lookup.Manifest.Size);
        Assert.AreEqual(Sha, lookup.Manifest.Sha256);
    }

    [Test]
    public void AnAssetWithoutDigest_YieldsAnEmptySha()
    {
        var lookup = Select("v0.700", Asset("KitchenDesigner-Setup-0.700-x64.exe", size: 5));

        Assert.AreEqual(string.Empty, lookup.Manifest.Sha256);
        Assert.AreEqual(5, lookup.Manifest.Size);
    }

    [Test]
    public void AnUnsupportedDigestAlgorithm_YieldsAnEmptySha()
    {
        var lookup = Select("v0.700", Asset("KitchenDesigner-Setup-0.700-x64.exe", digest: "sha512:" + Sha));

        Assert.AreEqual(string.Empty, lookup.Manifest.Sha256);
    }

    [Test]
    public void NoTag_IsNoRelease()
    {
        Assert.AreEqual(ReleaseLookupStatus.NoRelease, Select("", Asset("KitchenDesigner-Setup-0.700-x64.exe")).Status);
        Assert.AreEqual(ReleaseLookupStatus.NoRelease, Select(null).Status);
    }

    [Test]
    public void AnUnparsableTag_IsFailed_NamingTheTag()
    {
        var lookup = Select("nightly", Asset("KitchenDesigner-Setup-0.700-x64.exe"));

        Assert.AreEqual(ReleaseLookupStatus.Failed, lookup.Status);
        StringAssert.Contains("nightly", lookup.Reason);
    }

    [Test]
    public void NullAssetList_MeansNoInstaller_ButKeepsTheVersion()
    {
        var lookup = ReleaseAssetSelector.Select("v0.700", null);

        Assert.AreEqual(ReleaseLookupStatus.NoInstallerAsset, lookup.Status);
        Assert.AreEqual("0.700", lookup.Version, "версия нужна и без ассета: по ней решается «приложение уже последнее»");
        StringAssert.Contains("0.700", lookup.Reason);
    }

    [Test]
    public void AnAssetOfAnotherVersion_IsNotTaken()
    {
        var lookup = Select("v0.700", Asset("KitchenDesigner-Setup-0.662-x64.exe"));

        Assert.AreEqual(ReleaseLookupStatus.NoInstallerAsset, lookup.Status);
    }

    [Test]
    public void AnAssetOfALongerVersion_DoesNotPassForItsPrefix()
    {
        var lookup = Select("v0.70", Asset("KitchenDesigner-Setup-0.700-x64.exe"));

        Assert.AreEqual(ReleaseLookupStatus.NoInstallerAsset, lookup.Status);
    }

    [TestCase("icon.png")]
    [TestCase("KitchenDesigner-Setup-0.700-x64.zip")]
    [TestCase("KitchenDesigner-Setup-0.700-x86.exe")]
    [TestCase("KitchenDesigner-0.700-x64.exe")]
    public void NonSetupAssets_AreIgnored(string name)
    {
        Assert.AreEqual(ReleaseLookupStatus.NoInstallerAsset, Select("v0.700", Asset(name)).Status);
    }

    [Test]
    public void AssetsWithoutNameOrUrl_AreSkipped()
    {
        var lookup = Select("v0.700",
            null,
            Asset("", "u"),
            Asset("KitchenDesigner-Setup-0.700-x64.exe", ""),
            Asset("KitchenDesigner-Setup-0.700-x64.exe", "https://gh/ok"));

        Assert.AreEqual("https://gh/ok", lookup.Manifest.DownloadUrl);
    }

    [Test]
    public void TheMatchingAsset_BeatsEarlierLeftovers()
    {
        var lookup = Select("v0.700",
            Asset("KitchenDesigner-Setup-0.699-x64.exe", "a"),
            Asset("KitchenDesigner-Setup-0.700-x64.exe", "b"));

        Assert.AreEqual("b", lookup.Manifest.DownloadUrl);
    }
}
