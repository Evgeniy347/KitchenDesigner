using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class RecentProjectRowSourceTests
{
    private string _path = "";

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(_path) && File.Exists(_path)) File.Delete(_path);
    }

    [Test]
    public void For_MissingFile_ReturnsARowThatDoesNotExist()
    {
        var row = RecentProjectRowSource.For(Path.Combine(Application.temporaryCachePath, "does-not-exist.kdproj"));
        Assert.IsFalse(row.FileExists);
    }

    [Test]
    public void For_FileWithCurrentAppVersion_IsNotAMismatch()
    {
        _path = Path.Combine(Application.temporaryCachePath, "rprs_current.kdproj");
        File.WriteAllText(_path,
            "{\"version\":1,\"appVersion\":\"" + BuildInfo.Version +
            "\",\"createdAtUtc\":\"2026-01-01T00:00:00Z\"}");

        var row = RecentProjectRowSource.For(_path);

        Assert.IsTrue(row.FileExists);
        Assert.IsFalse(row.VersionMismatch);
        Assert.AreEqual(BuildInfo.Version, row.VersionLabel);
    }

    [Test]
    public void For_FileWithADifferentAppVersion_IsAMismatch()
    {
        _path = Path.Combine(Application.temporaryCachePath, "rprs_old.kdproj");
        File.WriteAllText(_path, "{\"version\":1,\"appVersion\":\"0.1\",\"createdAtUtc\":\"2026-01-01T00:00:00Z\"}");

        var row = RecentProjectRowSource.For(_path);

        Assert.IsTrue(row.VersionMismatch);
        Assert.AreEqual("0.1", row.VersionLabel);
    }

    [Test]
    public void For_FileWithoutAVersionField_ShowsThePlaceholder()
    {
        _path = Path.Combine(Application.temporaryCachePath, "rprs_noversion.kdproj");
        File.WriteAllText(_path, "{\"version\":1}");

        var row = RecentProjectRowSource.For(_path);

        Assert.AreEqual(RecentProjectRow.MissingVersionLabel, row.VersionLabel);
        Assert.IsTrue(row.VersionMismatch);
    }

    [Test]
    public void For_FileWithoutACreationDate_FallsBackToTheFilesystemDate()
    {
        _path = Path.Combine(Application.temporaryCachePath, "rprs_nocreated.kdproj");
        File.WriteAllText(_path, "{\"version\":1,\"appVersion\":\"" + BuildInfo.Version + "\"}");

        var row = RecentProjectRowSource.For(_path);

        Assert.AreNotEqual(RecentProjectRow.MissingVersionLabel, row.CreatedLabel,
            "у файла без createdAtUtc всё равно есть время создания на диске");
    }
}
