using NUnit.Framework;
using KitchenDesigner.Core;

public class RecentProjectRowTests
{
    private const string AppVersion = "0.500";

    [Test]
    public void Describe_MissingFile_IsFlaggedAndShowsPlaceholders()
    {
        var row = RecentProjectRow.Describe("gone.kdproj", fileExists: false,
            storedAppVersion: "0.500", currentAppVersion: AppVersion,
            createdAtUtc: "2026-01-01T00:00:00Z", fallbackCreatedAtUtc: null, modifiedAtUtc: "2026-01-01T00:00:00Z");

        Assert.IsFalse(row.FileExists);
        Assert.IsTrue(row.VersionMismatch, "отсутствующий файл красится как несовпадение версии");
    }

    [Test]
    public void Describe_MatchingVersion_IsNotAMismatch()
    {
        var row = RecentProjectRow.Describe("a.kdproj", fileExists: true,
            storedAppVersion: AppVersion, currentAppVersion: AppVersion,
            createdAtUtc: "2026-01-01T00:00:00Z", fallbackCreatedAtUtc: null, modifiedAtUtc: "2026-01-02T00:00:00Z");

        Assert.IsFalse(row.VersionMismatch);
        Assert.AreEqual(AppVersion, row.VersionLabel);
    }

    [Test]
    public void Describe_OlderVersion_IsAMismatch()
    {
        var row = RecentProjectRow.Describe("a.kdproj", fileExists: true,
            storedAppVersion: "0.100", currentAppVersion: AppVersion,
            createdAtUtc: "2026-01-01T00:00:00Z", fallbackCreatedAtUtc: null, modifiedAtUtc: "2026-01-02T00:00:00Z");

        Assert.IsTrue(row.VersionMismatch);
        Assert.AreEqual("0.100", row.VersionLabel);
    }

    [Test]
    public void Describe_NewerVersion_IsAlsoAMismatch()
    {
        var row = RecentProjectRow.Describe("a.kdproj", fileExists: true,
            storedAppVersion: "0.900", currentAppVersion: AppVersion,
            createdAtUtc: "2026-01-01T00:00:00Z", fallbackCreatedAtUtc: null, modifiedAtUtc: "2026-01-02T00:00:00Z");

        Assert.IsTrue(row.VersionMismatch, "версия отличается в ЛЮБУЮ сторону — не только 'старше'");
    }

    [Test]
    public void Describe_NoStoredVersion_ShowsPlaceholderAndIsAMismatch()
    {
        var row = RecentProjectRow.Describe("old.json", fileExists: true,
            storedAppVersion: "", currentAppVersion: AppVersion,
            createdAtUtc: "", fallbackCreatedAtUtc: "2020-01-01T00:00:00Z", modifiedAtUtc: "2026-01-02T00:00:00Z");

        Assert.AreEqual(RecentProjectRow.MissingVersionLabel, row.VersionLabel);
        Assert.IsTrue(row.VersionMismatch);
    }

    [Test]
    public void Describe_NoStoredCreationDate_FallsBackToFilesystemDate()
    {
        var row = RecentProjectRow.Describe("old.json", fileExists: true,
            storedAppVersion: AppVersion, currentAppVersion: AppVersion,
            createdAtUtc: "", fallbackCreatedAtUtc: "2020-03-04T05:06:00Z", modifiedAtUtc: "2026-01-02T00:00:00Z");

        Assert.AreNotEqual(RecentProjectRow.MissingVersionLabel, row.CreatedLabel,
            "дата из файловой системы должна отрисоваться, а не прочерк");
    }

    [Test]
    public void Describe_StoredCreationDate_WinsOverFilesystemFallback()
    {
        var withStored = RecentProjectRow.Describe("a.kdproj", fileExists: true,
            storedAppVersion: AppVersion, currentAppVersion: AppVersion,
            createdAtUtc: "2021-06-15T00:00:00Z", fallbackCreatedAtUtc: "2020-03-04T05:06:00Z",
            modifiedAtUtc: "2026-01-02T00:00:00Z");

        Assert.IsTrue(withStored.CreatedLabel.StartsWith("2021"),
            "хранимая дата создания приоритетнее даты файловой системы");
    }

    [Test]
    public void Describe_NoCreationDateAtAll_ShowsPlaceholder()
    {
        var row = RecentProjectRow.Describe("a.kdproj", fileExists: true,
            storedAppVersion: AppVersion, currentAppVersion: AppVersion,
            createdAtUtc: "", fallbackCreatedAtUtc: null, modifiedAtUtc: "2026-01-02T00:00:00Z");

        Assert.AreEqual(RecentProjectRow.MissingVersionLabel, row.CreatedLabel);
    }

    [Test]
    public void Describe_ModifiedDate_IsFormatted()
    {
        var row = RecentProjectRow.Describe("a.kdproj", fileExists: true,
            storedAppVersion: AppVersion, currentAppVersion: AppVersion,
            createdAtUtc: "2021-06-15T00:00:00Z", fallbackCreatedAtUtc: null,
            modifiedAtUtc: "2026-03-04T05:06:00Z");

        Assert.AreNotEqual(RecentProjectRow.MissingVersionLabel, row.ModifiedLabel);
    }
}
