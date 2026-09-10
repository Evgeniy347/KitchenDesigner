using System;
using System.IO;
using System.Runtime.InteropServices;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>save_project must not be able to write ANYWHERE the caller names — see
/// AGENTS.md/readme-mcp.md on the '-mcpSaveDir' guard. These tests never touch
/// docs/example.save.json; every fixture lives under a fresh temp directory.
/// Each escape test is paired with the ordinary case it must not break — the
/// "opposite input" agents/TEST-DESIGN.md asks for.</summary>
public class McpSaveDirectoryGuardTests
{
#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateSymbolicLinkW(string symlink, string target, uint flags);
#pragma warning restore SYSLIB1054

    private const uint SYMBOLIC_LINK_FLAG_DIRECTORY = 0x1;
    private const uint SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE = 0x2;

    private string _allowedDir = string.Empty;
    private string _outsideDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        var root = Path.Combine(Path.GetTempPath(), "kd-mcp-guard-" + Guid.NewGuid().ToString("N"));
        _allowedDir = Path.Combine(root, "allowed");
        _outsideDir = Path.Combine(root, "outside");
        Directory.CreateDirectory(_allowedDir);
        Directory.CreateDirectory(_outsideDir);
    }

    [TearDown]
    public void TearDown()
    {
        var root = Directory.GetParent(_allowedDir)!.FullName;
        try { Directory.Delete(root, recursive: true); } catch (Exception) { /* best-effort cleanup */ }
    }

    [Test]
    public void Evaluate_Refuses_WhenNoDirectoryConfigured()
    {
        var result = McpSaveDirectoryGuard.Evaluate(null, Path.Combine(_allowedDir, "project.json"));

        Assert.IsFalse(result.Allowed,
            "без -mcpSaveDir save_project обязан отказывать всем путям, а не тихо разрешать первый попавшийся");
        Assert.IsNotNull(result.Reason);
        StringAssert.Contains(McpSaveDirectoryArgument.Name, result.Reason,
            "отказ обязан называть параметр, который нужно включить, а не просто молчать");
    }

    [Test]
    public void Evaluate_Refuses_WhenDirectoryConfiguredAsEmptyString()
    {
        var result = McpSaveDirectoryGuard.Evaluate("", Path.Combine(_allowedDir, "project.json"));

        Assert.IsFalse(result.Allowed);
    }

    [Test]
    public void Evaluate_Refuses_WhenRequestedPathIsEmpty()
    {
        var result = McpSaveDirectoryGuard.Evaluate(_allowedDir, "");

        Assert.IsFalse(result.Allowed);
    }

    [Test]
    public void Evaluate_Allows_WhenPathIsDirectlyInsideAllowedDirectory()
    {
        var result = McpSaveDirectoryGuard.Evaluate(_allowedDir, Path.Combine(_allowedDir, "project.json"));

        Assert.IsTrue(result.Allowed, result.Reason);
    }

    [Test]
    public void Evaluate_Allows_WhenNestedSubdirectoryDoesNotExistYet()
    {
        var target = Path.Combine(_allowedDir, "sub", "deeper", "project.json");

        var result = McpSaveDirectoryGuard.Evaluate(_allowedDir, target);

        Assert.IsTrue(result.Allowed, result.Reason);
    }

    [Test]
    public void Evaluate_Refuses_WhenPathIsASiblingDirectory()
    {
        var result = McpSaveDirectoryGuard.Evaluate(_allowedDir, Path.Combine(_outsideDir, "project.json"));

        Assert.IsFalse(result.Allowed,
            "директория рядом с разрешённой — не внутри неё, только префикс строки этого не видит");
    }

    [Test]
    public void Evaluate_RefusesEscapeViaDotDot_ButAllowsTheEquivalentPathInside()
    {
        // opposite pair: the same file, named the honest way, must still be allowed
        var honest = Path.Combine(_allowedDir, "project.json");
        Assert.IsTrue(McpSaveDirectoryGuard.Evaluate(_allowedDir, honest).Allowed);

        var viaDotDot = Path.Combine(_allowedDir, "..", "outside", "project.json");
        var result = McpSaveDirectoryGuard.Evaluate(_allowedDir, viaDotDot);

        Assert.IsFalse(result.Allowed, "'..' обязан разворачиваться до сравнения, а не читаться как есть");
    }

    [Test]
    public void Evaluate_Refuses_WhenPathIsAnUnrelatedAbsolutePath()
    {
        var unrelated = Path.Combine(Path.GetTempPath(), "kd-mcp-guard-unrelated-" + Guid.NewGuid().ToString("N") + ".json");

        var result = McpSaveDirectoryGuard.Evaluate(_allowedDir, unrelated);

        Assert.IsFalse(result.Allowed);
    }

    [Test]
    public void Evaluate_Allows_WhenCaseDiffersFromTheConfiguredDirectory()
    {
        var upper = _allowedDir.ToUpperInvariant();

        var result = McpSaveDirectoryGuard.Evaluate(upper, Path.Combine(_allowedDir, "project.json"));

        Assert.IsTrue(result.Allowed, result.Reason);
    }

    [Test]
    public void Evaluate_Allows_WhenTheRequestedPathUsesForwardSlashes()
    {
        var withForwardSlashes = _allowedDir.Replace('\\', '/') + "/project.json";

        var result = McpSaveDirectoryGuard.Evaluate(_allowedDir, withForwardSlashes);

        Assert.IsTrue(result.Allowed, result.Reason);
    }

    [Test]
    public void Evaluate_Refuses_WhenASymlinkInsideTheAllowedDirectoryPointsOutside()
    {
        var linkPath = Path.Combine(_allowedDir, "escape-link");
        CreateSymbolicLinkW(linkPath, _outsideDir,
            SYMBOLIC_LINK_FLAG_DIRECTORY | SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE);
        bool created = Marshal.GetLastWin32Error() == 0 && Directory.Exists(linkPath);
        if (!created)
        {
            Assert.Ignore("Не удалось создать символическую ссылку в этом окружении "
                + "(нужны права администратора либо включённый Режим разработчика Windows — "
                + "CreateSymbolicLinkW возвращает true даже при отказе по привилегиям).");
            return;
        }

        // opposite pair: a real subdirectory (not a link) is still allowed
        var realSub = Path.Combine(_allowedDir, "real-sub", "project.json");
        Assert.IsTrue(McpSaveDirectoryGuard.Evaluate(_allowedDir, realSub).Allowed);

        var throughTheLink = Path.Combine(linkPath, "sneaky.json");
        var result = McpSaveDirectoryGuard.Evaluate(_allowedDir, throughTheLink);

        Assert.IsFalse(result.Allowed,
            "ссылка внутри разрешённого каталога, указывающая наружу, не должна выводить сохранение за его пределы");
    }
}
