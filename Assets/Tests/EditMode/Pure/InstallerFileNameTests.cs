#nullable disable
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Имя установщика в папке обновлений — единственное, по чему планировщик отличает «своё» от
/// чужого. Чистка папки опирается на разбор имени, поэтому каждое похожее имя, которое НЕ
/// должно считаться своим, разобрано здесь отдельно: удалить пользовательский файл хуже, чем
/// оставить лишний установщик.
/// </summary>
public class InstallerFileNameTests
{
    [Test]
    public void For_FollowsThePublishedAssetNaming()
    {
        Assert.AreEqual("KitchenDesigner-Setup-0.2100-x64.exe", InstallerFileName.For("0.2100"),
            "installer/PUBLISH.md: KitchenDesigner-Setup-<тег без v>-x64.exe — апдейтер отказывается от другого имени");
    }

    [Test]
    public void PartFor_IsTheInstallerNameWithAPartSuffix()
    {
        Assert.AreEqual("KitchenDesigner-Setup-0.2100-x64.exe.part", InstallerFileName.PartFor("0.2100"));
    }

    [TestCase("0.2100", true)]
    [TestCase("1", true)]
    [TestCase("1.2", true)]
    [TestCase("1.2.3", true)]
    [TestCase("123456789.0.1", true)]
    [TestCase("1.2.3.4", false)]
    [TestCase("1234567890", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    [TestCase("1..2", false)]
    [TestCase(".1", false)]
    [TestCase("1.", false)]
    [TestCase("v1.2", false)]
    [TestCase("1.2-beta", false)]
    [TestCase("1.2 ", false)]
    [TestCase("1,2", false)]
    [TestCase("-1", false)]
    public void IsValidVersion_AcceptsOnlyUpToThreeNumericGroups(string version, bool expected)
    {
        Assert.AreEqual(expected, InstallerFileName.IsValidVersion(version));
    }

    [Test]
    public void TryParseInstaller_RoundTripsWithFor()
    {
        Assert.IsTrue(InstallerFileName.TryParseInstaller(InstallerFileName.For("0.2100"), out var version));
        Assert.AreEqual("0.2100", version);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("KitchenDesigner-Setup--x64.exe")]
    [TestCase("KitchenDesigner-Setup-abc-x64.exe")]
    [TestCase("KitchenDesigner-Setup-0.2100-x86.exe")]
    [TestCase("KitchenDesigner-Setup-0.2100-x64.exe.bak")]
    [TestCase("KitchenDesigner-Setup-0.2100-x64.exe.part")]
    [TestCase("KitchenDesigner-Setup-0.2100-x64.log")]
    [TestCase("kitchendesigner-setup-0.2100-x64.exe")]
    [TestCase("KITCHENDESIGNER-SETUP-0.2100-X64.EXE")]
    [TestCase("XKitchenDesigner-Setup-0.2100-x64.exe")]
    [TestCase(" KitchenDesigner-Setup-0.2100-x64.exe")]
    [TestCase("KitchenDesigner-Setup-0.2100-x64.exe ")]
    [TestCase("KitchenDesigner-Setup-1.2.3.4-x64.exe")]
    [TestCase("KitchenDesigner-Setup-0.2100-beta-x64.exe")]
    [TestCase("..\\KitchenDesigner-Setup-0.2100-x64.exe")]
    [TestCase("sub/KitchenDesigner-Setup-0.2100-x64.exe")]
    [TestCase("Other-Setup-0.2100-x64.exe")]
    [TestCase("setup.exe")]
    public void TryParseInstaller_RejectsEveryLookalike(string name)
    {
        Assert.IsFalse(InstallerFileName.TryParseInstaller(name, out var version), name);
        Assert.AreEqual(string.Empty, version);
    }

    [Test]
    public void TryParsePart_RoundTripsWithPartFor()
    {
        Assert.IsTrue(InstallerFileName.TryParsePart(InstallerFileName.PartFor("0.2100"), out var version));
        Assert.AreEqual("0.2100", version);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("KitchenDesigner-Setup-0.2100-x64.exe")]
    [TestCase("KitchenDesigner-Setup-0.2100-x64.exe.part.bak")]
    [TestCase("KitchenDesigner-Setup-0.2100-x64.exe.PART")]
    [TestCase("KitchenDesigner-Setup-0.2100-x64.part")]
    [TestCase("KitchenDesigner-Setup--x64.exe.part")]
    [TestCase("notes.part")]
    [TestCase(".part")]
    [TestCase("..\\KitchenDesigner-Setup-0.2100-x64.exe.part")]
    public void TryParsePart_RejectsEveryLookalike(string name)
    {
        Assert.IsFalse(InstallerFileName.TryParsePart(name, out var version), name);
        Assert.AreEqual(string.Empty, version);
    }

    [Test]
    public void IsOurs_IsTheUnionOfInstallersAndParts()
    {
        Assert.IsTrue(InstallerFileName.IsOurs(InstallerFileName.For("1.0")));
        Assert.IsTrue(InstallerFileName.IsOurs(InstallerFileName.PartFor("1.0")));
        Assert.IsFalse(InstallerFileName.IsOurs("readme.txt"));
        Assert.IsFalse(InstallerFileName.IsOurs(null));
    }
}
