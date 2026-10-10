#nullable disable
using System.IO;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

public class UpdateFolderLocationTests
{
    [Test]
    public void Root_IsKitchenDesignerUpdates_UnderTheGivenTempPath()
    {
        var temp = Path.Combine("C:", "Users", "x", "AppData", "Local", "Temp");

        var root = UpdateFolderLocation.RootUnder(temp);

        Assert.AreEqual(Path.Combine(temp, "KitchenDesigner", "Updates"), root);
        StringAssert.EndsWith(Path.Combine("KitchenDesigner", "Updates"), root);
    }

    [Test]
    public void Root_ContainsNoVersion_SoEveryVersionLandsInTheSameFolder()
    {
        var a = UpdateFolderLocation.RootUnder(@"C:\Temp");
        var b = UpdateFolderLocation.RootUnder(@"C:\Temp\");

        Assert.AreEqual(Path.GetFullPath(a), Path.GetFullPath(b));
        StringAssert.DoesNotContain("0.", Path.GetFileName(a));
    }

    [Test]
    public void InstallerPath_IsRootPlusTheVersionedName()
    {
        var path = Path.Combine(UpdateFolderLocation.RootUnder(@"C:\Temp"), InstallerFileName.For("0.2100"));

        StringAssert.EndsWith(Path.Combine("KitchenDesigner", "Updates", "KitchenDesigner-Setup-0.2100-x64.exe"), path);
    }
}
