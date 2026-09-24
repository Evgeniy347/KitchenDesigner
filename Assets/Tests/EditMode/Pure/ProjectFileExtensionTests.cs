using NUnit.Framework;
using KitchenDesigner.Core;

public class ProjectFileExtensionTests
{
    [Test]
    public void IsSupported_AcceptsTheCurrentExtension()
    {
        Assert.IsTrue(ProjectFileExtension.IsSupported(@"C:\projects\kitchen.kdproj"));
    }

    [Test]
    public void IsSupported_AcceptsTheLegacyExtension()
    {
        Assert.IsTrue(ProjectFileExtension.IsSupported(@"C:\projects\kitchen.json"));
    }

    [Test]
    public void IsSupported_IsCaseInsensitive()
    {
        Assert.IsTrue(ProjectFileExtension.IsSupported(@"C:\projects\KITCHEN.KDPROJ"));
    }

    [Test]
    public void IsSupported_RejectsAnUnrelatedExtension()
    {
        Assert.IsFalse(ProjectFileExtension.IsSupported(@"C:\projects\kitchen.csv"));
    }

    [Test]
    public void IsSupported_RejectsNullOrEmpty()
    {
        Assert.IsFalse(ProjectFileExtension.IsSupported(null));
        Assert.IsFalse(ProjectFileExtension.IsSupported(""));
    }

    [Test]
    public void WithDefaultExtension_AppendsCurrentExtension_WhenNoneRecognized()
    {
        Assert.AreEqual(@"C:\projects\kitchen.kdproj",
            ProjectFileExtension.WithDefaultExtension(@"C:\projects\kitchen"));
    }

    [Test]
    public void WithDefaultExtension_LeavesTheLegacyExtensionAlone()
    {
        Assert.AreEqual(@"C:\projects\kitchen.json",
            ProjectFileExtension.WithDefaultExtension(@"C:\projects\kitchen.json"));
    }

    [Test]
    public void WithDefaultExtension_LeavesTheCurrentExtensionAlone()
    {
        Assert.AreEqual(@"C:\projects\kitchen.kdproj",
            ProjectFileExtension.WithDefaultExtension(@"C:\projects\kitchen.kdproj"));
    }
}
