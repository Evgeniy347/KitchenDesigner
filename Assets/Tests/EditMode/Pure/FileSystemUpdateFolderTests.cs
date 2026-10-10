#nullable disable
using System.IO;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>
/// Папка обновлений на настоящем диске: перечисление, удаление и переименование .part в готовый
/// файл. Главное требование — ничего вне этой папки и ничего, что не называется как наш
/// установщик, не удаляется и не двигается ни при каких именах.
/// </summary>
public class FileSystemUpdateFolderTests
{
    private static readonly string Installer = InstallerFileName.For("0.2100");
    private static readonly string Part = InstallerFileName.PartFor("0.2100");

    private TempUpdateFolder _temp;

    [SetUp]
    public void SetUp() => _temp = new TempUpdateFolder();

    [TearDown]
    public void TearDown() => _temp.Dispose();

    [Test]
    public void List_OfAFolderThatDoesNotExistYet_IsEmpty()
    {
        Assert.IsEmpty(_temp.Folder.List());
    }

    [Test]
    public void List_ReturnsFilesWithTheirSizes_AndSkipsDirectories()
    {
        _temp.Write(Installer, new byte[123]);
        _temp.Write("notes.txt", new byte[5]);
        Directory.CreateDirectory(_temp.PathOf(InstallerFileName.For("0.1000")));

        var entries = _temp.Folder.List();

        CollectionAssert.AreEquivalent(new[] { Installer, "notes.txt" }, entries.Select(e => e.Name).ToArray());
        Assert.AreEqual(123, entries.Single(e => e.Name == Installer).Size);
    }

    [Test]
    public void EnsureExists_CreatesTheFolder_AndIsRepeatable()
    {
        _temp.Folder.EnsureExists();
        _temp.Folder.EnsureExists();

        Assert.IsTrue(Directory.Exists(_temp.Root));
    }

    [Test]
    public void PathOf_IsInsideTheRoot()
    {
        Assert.AreEqual(Path.Combine(_temp.Folder.Root, Installer), _temp.Folder.PathOf(Installer));
        Assert.AreEqual(Path.GetFullPath(_temp.Root), _temp.Folder.Root);
    }

    [Test]
    public void TryDelete_RemovesOurInstallerAndOurPart()
    {
        _temp.Write(Installer, new byte[1]);
        _temp.Write(Part, new byte[1]);

        Assert.IsTrue(_temp.Folder.TryDelete(Installer, out var e1), e1);
        Assert.IsTrue(_temp.Folder.TryDelete(Part, out var e2), e2);

        Assert.IsEmpty(_temp.Names());
    }

    [Test]
    public void TryDelete_OfAFileThatIsAlreadyGone_Succeeds()
    {
        _temp.Folder.EnsureExists();

        Assert.IsTrue(_temp.Folder.TryDelete(Installer, out var error), error);
    }

    [TestCase("notes.txt")]
    [TestCase("KitchenDesigner-Setup-0.2100-x64.exe.bak")]
    [TestCase("KitchenDesigner-Setup-0.2100-x64.log")]
    [TestCase("kitchendesigner-setup-0.2100-x64.exe")]
    [TestCase("KitchenDesigner-Setup-0.2100-x86.exe")]
    public void TryDelete_RefusesEveryNameThatIsNotOurs_EvenWhenTheFileIsInTheFolder(string name)
    {
        _temp.Write(name, new byte[3]);

        Assert.IsFalse(_temp.Folder.TryDelete(name, out var error));

        Assert.IsTrue(_temp.Has(name), "чужой файл в папке обновлений остаётся на месте");
        StringAssert.Contains(name, error);
    }

    [TestCase("..\\KitchenDesigner-Setup-0.2100-x64.exe")]
    [TestCase("../KitchenDesigner-Setup-0.2100-x64.exe")]
    [TestCase("..\\..\\KitchenDesigner-Setup-0.2100-x64.exe.part")]
    [TestCase("")]
    [TestCase(null)]
    public void TryDelete_NeverReachesOutsideTheFolder(string name)
    {
        _temp.Folder.EnsureExists();
        _temp.WriteOutside(Installer, new byte[3]);
        _temp.WriteOutside(Part, new byte[3]);

        Assert.IsFalse(_temp.Folder.TryDelete(name, out _));

        Assert.IsTrue(File.Exists(_temp.OutsidePathOf(Installer)));
        Assert.IsTrue(File.Exists(_temp.OutsidePathOf(Part)));
    }

    [Test]
    public void TryDelete_OfAFolderNamedLikeAnInstaller_LeavesTheFolderAlone()
    {
        var dir = _temp.PathOf(Installer);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "inner.txt"), "x");

        _temp.Folder.TryDelete(Installer, out _);

        Assert.IsTrue(File.Exists(Path.Combine(dir, "inner.txt")), "каталог с похожим именем не файл и не удаляется");
    }

    [Test]
    public void TryDelete_OfALockedFile_ReportsFailure_NotAnException()
    {
        _temp.Write(Installer, new byte[3]);

        using (new FileStream(_temp.PathOf(Installer), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.IsFalse(_temp.Folder.TryDelete(Installer, out var error));
            Assert.IsNotEmpty(error);
        }

        Assert.IsTrue(_temp.Has(Installer));
    }

    [Test]
    public void TryPromote_MovesThePartToTheFinalName_KeepingEveryByte()
    {
        var bytes = Payload.Bytes(4096);
        _temp.Write(Part, bytes);

        Assert.IsTrue(_temp.Folder.TryPromote(Part, Installer, out var error), error);

        Assert.IsFalse(_temp.Has(Part), "после переименования .part не остаётся");
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(_temp.PathOf(Installer)));
    }

    [Test]
    public void TryPromote_ReplacesAnOlderFinalFile()
    {
        _temp.Write(Installer, new byte[] { 1, 2, 3 });
        _temp.Write(Part, new byte[] { 9, 9 });

        Assert.IsTrue(_temp.Folder.TryPromote(Part, Installer, out var error), error);

        CollectionAssert.AreEqual(new byte[] { 9, 9 }, File.ReadAllBytes(_temp.PathOf(Installer)));
    }

    [Test]
    public void TryPromote_WithoutAPart_Fails_AndNamesIt()
    {
        _temp.Folder.EnsureExists();

        Assert.IsFalse(_temp.Folder.TryPromote(Part, Installer, out var error));
        StringAssert.Contains(Part, error);
        Assert.IsFalse(_temp.Has(Installer));
    }

    [Test]
    public void TryPromote_RefusesNamesOfDifferentVersions()
    {
        var otherFinal = InstallerFileName.For("0.2200");
        _temp.Write(Part, new byte[2]);

        Assert.IsFalse(_temp.Folder.TryPromote(Part, otherFinal, out _));

        Assert.IsTrue(_temp.Has(Part));
        Assert.IsFalse(_temp.Has(otherFinal));
    }

    [TestCase("notes.txt", "KitchenDesigner-Setup-0.2100-x64.exe")]
    [TestCase("KitchenDesigner-Setup-0.2100-x64.exe.part", "notes.txt")]
    [TestCase("KitchenDesigner-Setup-0.2100-x64.exe", "KitchenDesigner-Setup-0.2100-x64.exe")]
    [TestCase("..\\KitchenDesigner-Setup-0.2100-x64.exe.part", "KitchenDesigner-Setup-0.2100-x64.exe")]
    public void TryPromote_RefusesNamesThatAreNotAPartAndAnInstaller(string part, string final)
    {
        _temp.Write("notes.txt", new byte[1]);
        _temp.Write(Installer, new byte[1]);

        Assert.IsFalse(_temp.Folder.TryPromote(part, final, out var error));
        Assert.IsNotEmpty(error);
        Assert.IsTrue(_temp.Has("notes.txt"));
    }

    [Test]
    public void TryPromote_OfALockedPart_ReportsFailure_AndKeepsThePart()
    {
        _temp.Write(Part, new byte[5]);

        using (new FileStream(_temp.PathOf(Part), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.IsFalse(_temp.Folder.TryPromote(Part, Installer, out var error));
            Assert.IsNotEmpty(error);
        }

        Assert.IsTrue(_temp.Has(Part));
        Assert.IsFalse(_temp.Has(Installer));
    }

    [Test]
    public void Constructor_RejectsAnEmptyRoot()
    {
        Assert.Throws<System.ArgumentException>(() => new FileSystemUpdateFolder(" "));
    }
}
