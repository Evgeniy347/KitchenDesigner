using KitchenServer.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KitchenServer.Tests;

public sealed class ProjectStorageServiceTests : IDisposable
{
    private readonly string _root;

    public ProjectStorageServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "kitchen-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private ProjectStorageService CreateService(long maxSize = 10 * 1024 * 1024, int backups = 3) =>
        new(Options.Create(new ProjectStorageOptions
        {
            RootPath = _root,
            MaxSizeBytes = maxSize,
            BackupCount = backups
        }), NullLogger<ProjectStorageService>.Instance);

    [Fact]
    public async Task Save_Then_Load_Roundtrips()
    {
        var svc = CreateService();
        var id = Guid.NewGuid();

        await svc.Save(id, "user1", """{"boards":[1,2,3]}""");
        var loaded = await svc.Load(id, "user1");

        Assert.Equal("""{"boards":[1,2,3]}""", loaded);
    }

    [Fact]
    public async Task Load_Missing_ReturnsNull()
    {
        var svc = CreateService();
        Assert.Null(await svc.Load(Guid.NewGuid(), "user1"));
    }

    [Fact]
    public async Task Load_IsIsolatedPerUser()
    {
        var svc = CreateService();
        var id = Guid.NewGuid();

        await svc.Save(id, "user1", "{}");

        Assert.Null(await svc.Load(id, "user2"));
    }

    [Fact]
    public async Task Save_InvalidJson_Throws()
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<ProjectStorageException>(
            () => svc.Save(Guid.NewGuid(), "user1", "not json {"));
    }

    [Fact]
    public async Task Save_OverSizeLimit_Throws()
    {
        var svc = CreateService(maxSize: 100);
        var big = "{\"data\":\"" + new string('x', 200) + "\"}";
        await Assert.ThrowsAsync<ProjectStorageException>(
            () => svc.Save(Guid.NewGuid(), "user1", big));
    }

    [Fact]
    public async Task Save_RotatesBackups_AndKeepsLimit()
    {
        var svc = CreateService(backups: 2);
        var id = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
            await svc.Save(id, "user1", $"{{\"v\":{i}}}");

        var userDir = Directory.GetDirectories(_root).Single();
        var backups = Directory.GetFiles(userDir, $"{id}.*.bak");
        Assert.Equal(2, backups.Length);

        // current content is the latest version
        Assert.Equal("""{"v":4}""", await svc.Load(id, "user1"));
    }

    [Fact]
    public async Task Delete_RemovesSaveAndBackups()
    {
        var svc = CreateService(backups: 2);
        var id = Guid.NewGuid();

        await svc.Save(id, "user1", """{"v":1}""");
        await svc.Save(id, "user1", """{"v":2}""");
        svc.Delete(id, "user1");

        Assert.Null(await svc.Load(id, "user1"));
        var userDir = Directory.GetDirectories(_root).Single();
        Assert.Empty(Directory.GetFiles(userDir, $"{id}*"));
    }

    [Fact]
    public async Task UserId_PathTraversal_IsNeutralized()
    {
        var svc = CreateService();
        var id = Guid.NewGuid();

        // '.' and path separators are stripped: "../../evil" becomes a plain dir name
        await svc.Save(id, "../../evil", "{}");

        Assert.True(Directory.Exists(_root));
        var escaped = Path.GetFullPath(Path.Combine(_root, "..", "..", "evil"));
        Assert.False(Directory.Exists(escaped) && File.Exists(Path.Combine(escaped, $"{id}.json")),
            "save must not escape the storage root");
        Assert.Equal("{}", await svc.Load(id, "../../evil"));
    }

    [Fact]
    public async Task GetUsageBytes_CountsUserFiles()
    {
        var svc = CreateService();
        Assert.Equal(0, svc.GetUsageBytes("user1"));

        await svc.Save(Guid.NewGuid(), "user1", """{"a":1}""");
        Assert.True(svc.GetUsageBytes("user1") > 0);
        Assert.Equal(0, svc.GetUsageBytes("user2"));
    }

    [Fact]
    public async Task List_ReturnsSavedProjects()
    {
        var svc = CreateService();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await svc.Save(id1, "user1", "{}");
        await svc.Save(id2, "user1", "{}");

        var list = svc.List("user1");
        Assert.Equal(2, list.Count);
        Assert.Contains(list, x => x.Id == id1);
        Assert.Contains(list, x => x.Id == id2);
    }
}
