using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Editor;

public class BuildInfoGeneratorTests
{
    private string _originalGeneratedPath;
    private string _counterPath;
    private string _generatedBackup;
    private string _counterBackup;

    [SetUp]
    public void Setup()
    {
        _originalGeneratedPath = BuildInfoGenerator.OutputPath;
        _counterPath = Path.Combine(Application.dataPath, "..", BuildInfoGenerator.CounterFile);

        if (File.Exists(_originalGeneratedPath))
            _generatedBackup = File.ReadAllText(_originalGeneratedPath);

        if (File.Exists(_counterPath))
            _counterBackup = File.ReadAllText(_counterPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (_generatedBackup != null)
            File.WriteAllText(_originalGeneratedPath, _generatedBackup);
        else if (File.Exists(_originalGeneratedPath))
            File.Delete(_originalGeneratedPath);

        if (_counterBackup != null)
            File.WriteAllText(_counterPath, _counterBackup);
        else if (File.Exists(_counterPath))
            File.Delete(_counterPath);
    }

    // ── GetVersion ──────────────────────────────────────────

    [Test]
    public void GetVersion_ReturnsExpectedFormat()
    {
        var version = BuildInfoGenerator.GetVersion();
        Assert.IsTrue(Regex.IsMatch(version, @"^0\.\d+$"),
            $"Version '{version}' should match pattern '0.X'");
    }

    [Test]
    public void GetVersion_StartsWithZero()
    {
        var version = BuildInfoGenerator.GetVersion();
        Assert.IsTrue(version.StartsWith("0."), $"Version '{version}' should start with '0.'");
    }

    [Test]
    public void GetVersion_ReturnsPositiveNumber()
    {
        var version = BuildInfoGenerator.GetVersion();
        int count = int.Parse(version.Substring(2));
        Assert.Greater(count, 0, "Version number should be positive");
    }

    [Test]
    public void GetVersion_TwoCallsReturnConsistentFormat()
    {
        var v1 = BuildInfoGenerator.GetVersion();
        var v2 = BuildInfoGenerator.GetVersion();

        Assert.That(v1, Does.Match(@"^0\.\d+$"));
        Assert.That(v2, Does.Match(@"^0\.\d+$"));
    }

    // ── WriteFile ───────────────────────────────────────────

    [Test]
    public void WriteFile_CreatesGeneratedFile()
    {
        File.Delete(_originalGeneratedPath);

        BuildInfoGenerator.WriteFile();

        Assert.IsTrue(File.Exists(_originalGeneratedPath), "Generated file should exist");
    }

    [Test]
    public void WriteFile_ContainsCorrectNamespace()
    {
        BuildInfoGenerator.WriteFile();

        string content = File.ReadAllText(_originalGeneratedPath);
        Assert.IsTrue(content.Contains("namespace KitchenDesigner.Core"),
            "Should contain correct namespace");
    }

    [Test]
    public void WriteFile_ContainsPartialClass()
    {
        BuildInfoGenerator.WriteFile();

        string content = File.ReadAllText(_originalGeneratedPath);
        Assert.IsTrue(content.Contains("public static partial class BuildInfo"),
            "Should contain partial class declaration");
    }

    [Test]
    public void WriteFile_ContainsVersionConstant()
    {
        BuildInfoGenerator.WriteFile();

        string content = File.ReadAllText(_originalGeneratedPath);
        Assert.IsTrue(content.Contains("public const string Version"),
            "Should contain Version constant");
    }

    [Test]
    public void WriteFile_ContainsBuildDateConstant()
    {
        BuildInfoGenerator.WriteFile();

        string content = File.ReadAllText(_originalGeneratedPath);
        Assert.IsTrue(content.Contains("public const string BuildDate"),
            "Should contain BuildDate constant");
    }

    [Test]
    public void WriteFile_VersionFormatInFile()
    {
        BuildInfoGenerator.WriteFile();

        string content = File.ReadAllText(_originalGeneratedPath);
        var match = Regex.Match(content, @"Version = ""(0\.\d+)""");
        Assert.IsTrue(match.Success, $"Version value not found in file content:\n{content}");
    }

    [Test]
    public void WriteFile_BuildDateFormatInFile()
    {
        BuildInfoGenerator.WriteFile();

        string content = File.ReadAllText(_originalGeneratedPath);
        var match = Regex.Match(content, @"BuildDate = ""(\d{4}-\d{2}-\d{2} \d{2}:\d{2})""");
        Assert.IsTrue(match.Success, $"BuildDate should be 'yyyy-MM-dd HH:mm', got content:\n{content}");
    }

    [Test]
    public void WriteFile_OverwritesExistingFile()
    {
        File.WriteAllText(_originalGeneratedPath, "// old content");
        long sizeBefore = new FileInfo(_originalGeneratedPath).Length;

        BuildInfoGenerator.WriteFile();

        long sizeAfter = new FileInfo(_originalGeneratedPath).Length;
        Assert.AreNotEqual(sizeBefore, sizeAfter, "File should be overwritten");

        string content = File.ReadAllText(_originalGeneratedPath);
        Assert.IsTrue(content.Contains("namespace KitchenDesigner.Core"),
            "New content should replace old");
    }

    [Test]
    public void WriteFile_ProducedContent_MatchesTemplateStructure()
    {
        BuildInfoGenerator.WriteFile();

        string content = File.ReadAllText(_originalGeneratedPath);
        Assert.IsTrue(content.Contains("namespace KitchenDesigner.Core"));
        Assert.IsTrue(content.Contains("public static partial class BuildInfo"));
        Assert.IsTrue(content.Contains("public const string Version"));
        Assert.IsTrue(content.Contains("public const string BuildDate"));

        int namespaceCount = Regex.Matches(content, "namespace").Count;
        Assert.AreEqual(1, namespaceCount, "Should have exactly one namespace declaration");
    }

    // ── ReadAndIncrementCounter ─────────────────────────────

    [Test]
    public void ReadAndIncrementCounter_StartsFromOne()
    {
        File.Delete(_counterPath);

        int result = BuildInfoGenerator.ReadAndIncrementCounter();

        Assert.AreEqual(1, result, "First call with no file should return 1");
    }

    [Test]
    public void ReadAndIncrementCounter_WritesFile()
    {
        File.Delete(_counterPath);

        BuildInfoGenerator.ReadAndIncrementCounter();

        Assert.IsTrue(File.Exists(_counterPath), "Counter file should be created");
        string content = File.ReadAllText(_counterPath).Trim();
        Assert.AreEqual("1", content);
    }

    [Test]
    public void ReadAndIncrementCounter_IncrementsExistingValue()
    {
        File.WriteAllText(_counterPath, "5");

        int result = BuildInfoGenerator.ReadAndIncrementCounter();

        Assert.AreEqual(6, result);
        Assert.AreEqual("6", File.ReadAllText(_counterPath).Trim());
    }

    [Test]
    public void ReadAndIncrementCounter_HandlesEmptyFile()
    {
        File.WriteAllText(_counterPath, "");

        int result = BuildInfoGenerator.ReadAndIncrementCounter();

        Assert.AreEqual(1, result);
    }

    [Test]
    public void ReadAndIncrementCounter_HandlesWhitespaceFile()
    {
        File.WriteAllText(_counterPath, "  \n  ");

        int result = BuildInfoGenerator.ReadAndIncrementCounter();

        Assert.AreEqual(1, result);
    }
}
