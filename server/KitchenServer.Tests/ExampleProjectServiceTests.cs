using KitchenServer.Web.Data;
using KitchenServer.Web.Data.Entities;
using KitchenServer.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KitchenServer.Tests;

public sealed class ExampleProjectServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storageDir;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public ExampleProjectServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "kitchen-example-tests", Guid.NewGuid().ToString("N"));
        _storageDir = Path.Combine(_tempDir, "storage");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_storageDir);

        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
        using var db = new AppDbContext(_dbOptions);
        db.Database.EnsureDeleted();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private IDbContextFactory<AppDbContext> CreateDbFactory()
    {
        return new TestDbContextFactory(_dbOptions);
    }

    private ProjectStorageService CreateStorage(int backups = 3) =>
        new(Options.Create(new ProjectStorageOptions
        {
            RootPath = _storageDir,
            MaxSizeBytes = 10 * 1024 * 1024,
            BackupCount = backups
        }), NullLogger<ProjectStorageService>.Instance);

    private ExampleProjectService CreateExampleService(string exampleJson = """{"boards":[1,2,3]}""")
    {
        var seedPath = Path.Combine(_tempDir, "example.save.json");
        File.WriteAllText(seedPath, exampleJson);
        return new ExampleProjectService(
            NullLogger<ExampleProjectService>.Instance,
            new[] { seedPath });
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public TestDbContextFactory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
    }

    // ── Create ──────────────────────────────────────────────────────────

    [Fact]
    public async Task EnsureAsync_CreatesExample_WhenNoneExists()
    {
        var svc = CreateExampleService();
        var dbFactory = CreateDbFactory();
        var storage = CreateStorage();

        var created = await svc.EnsureAsync(dbFactory, storage, "user1");

        Assert.True(created);
        await using var db = await dbFactory.CreateDbContextAsync();
        var project = await db.Projects
            .FirstOrDefaultAsync(p => p.UserId == "user1" && p.IsExample && p.IsLatest);
        Assert.NotNull(project);
    }

    [Fact]
    public async Task EnsureAsync_CreatesWithIsExampleTrue()
    {
        var svc = CreateExampleService();
        var dbFactory = CreateDbFactory();
        var storage = CreateStorage();

        await svc.EnsureAsync(dbFactory, storage, "user1");

        await using var db = await dbFactory.CreateDbContextAsync();
        var project = await db.Projects
            .FirstAsync(p => p.UserId == "user1" && p.IsLatest);
        Assert.True(project.IsExample);
    }

    [Fact]
    public async Task EnsureAsync_CreatedProjectHasVersionOne()
    {
        var svc = CreateExampleService();
        var dbFactory = CreateDbFactory();
        var storage = CreateStorage();

        await svc.EnsureAsync(dbFactory, storage, "user1");

        await using var db = await dbFactory.CreateDbContextAsync();
        var project = await db.Projects
            .FirstAsync(p => p.UserId == "user1" && p.IsLatest);
        Assert.Equal(1, project.Version);
    }

    [Fact]
    public async Task EnsureAsync_CreatedProjectNameIsSet()
    {
        var svc = CreateExampleService();
        var dbFactory = CreateDbFactory();
        var storage = CreateStorage();

        await svc.EnsureAsync(dbFactory, storage, "user1");

        await using var db = await dbFactory.CreateDbContextAsync();
        var project = await db.Projects
            .FirstAsync(p => p.UserId == "user1" && p.IsLatest);
        Assert.Equal("Пример кухни", project.Name);
    }

    [Fact]
    public async Task EnsureAsync_JsonIsSavedToFileStorage()
    {
        var json = """{"boards":[1,2,3]}""";
        var svc = CreateExampleService(json);
        var dbFactory = CreateDbFactory();
        var storage = CreateStorage();

        await svc.EnsureAsync(dbFactory, storage, "user1");

        await using var db = await dbFactory.CreateDbContextAsync();
        var project = await db.Projects
            .FirstAsync(p => p.UserId == "user1" && p.IsLatest);
        var loaded = await storage.Load(project.ProjectGroupId, "user1");
        Assert.Equal(json, loaded);
    }

    [Fact]
    public async Task EnsureAsync_CreatedAtEqualsUpdatedAt()
    {
        var svc = CreateExampleService();
        var dbFactory = CreateDbFactory();
        var storage = CreateStorage();

        await svc.EnsureAsync(dbFactory, storage, "user1");

        await using var db = await dbFactory.CreateDbContextAsync();
        var project = await db.Projects
            .FirstAsync(p => p.UserId == "user1" && p.IsLatest);
        Assert.Equal(project.CreatedAt, project.UpdatedAt);
    }

    // ── No-op when nothing changed ──────────────────────────────────────

    [Fact]
    public async Task EnsureAsync_ReturnsFalse_WhenAlreadyExistsAndMatches()
    {
        var json = """{"boards":[1,2,3]}""";
        var svc = CreateExampleService(json);
        var dbFactory = CreateDbFactory();
        var storage = CreateStorage();

        await svc.EnsureAsync(dbFactory, storage, "user1"); // create
        var result = await svc.EnsureAsync(dbFactory, storage, "user1"); // should be no-op

        Assert.False(result);
        await using var db = await dbFactory.CreateDbContextAsync();
        var count = await db.Projects
            .CountAsync(p => p.UserId == "user1" && p.IsExample);
        Assert.Equal(1, count); // still only one row
    }

    // ── Update when JSON differs ────────────────────────────────────────

    [Fact]
    public async Task EnsureAsync_CreatesNewVersion_WhenJsonChanged()
    {
        var svcV1 = CreateExampleService("""{"boards":[1]}""");
        var svcV2 = CreateExampleService("""{"boards":[1,2,3]}""");
        var dbFactory = CreateDbFactory();
        var storage = CreateStorage();

        await svcV1.EnsureAsync(dbFactory, storage, "user1");
        var updated = await svcV2.EnsureAsync(dbFactory, storage, "user1");

        Assert.True(updated);
        await using var db = await dbFactory.CreateDbContextAsync();
        var all = await db.Projects
            .Where(p => p.UserId == "user1" && p.IsExample)
            .OrderBy(p => p.Version)
            .ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.False(all[0].IsLatest); // old version
        Assert.True(all[1].IsLatest);  // new version
    }

    [Fact]
    public async Task EnsureAsync_NewVersionHasVersionIncremented()
    {
        var svcV1 = CreateExampleService("""{"boards":[1]}""");
        var svcV2 = CreateExampleService("""{"boards":[1,2]}""");
        var dbFactory = CreateDbFactory();
        var storage = CreateStorage();

        await svcV1.EnsureAsync(dbFactory, storage, "user1");
        await svcV2.EnsureAsync(dbFactory, storage, "user1");

        await using var db = await dbFactory.CreateDbContextAsync();
        var latest = await db.Projects
            .FirstAsync(p => p.UserId == "user1" && p.IsLatest && p.IsExample);
        Assert.Equal(2, latest.Version);
    }

    [Fact]
    public async Task EnsureAsync_UpdatedJsonIsCorrectInNewVersion()
    {
        var oldJson = """{"boards":[1]}""";
        var newJson = """{"boards":[1,2,3]}""";
        var svcV1 = CreateExampleService(oldJson);
        var svcV2 = CreateExampleService(newJson);
        var dbFactory = CreateDbFactory();
        var storage = CreateStorage();

        await svcV1.EnsureAsync(dbFactory, storage, "user1");
        await svcV2.EnsureAsync(dbFactory, storage, "user1");

        await using var db = await dbFactory.CreateDbContextAsync();
        var latest = await db.Projects
            .FirstAsync(p => p.UserId == "user1" && p.IsLatest);
        Assert.Equal(newJson, latest.JsonData);

        // File storage also updated
        var loaded = await storage.Load(latest.ProjectGroupId, "user1");
        Assert.Equal(newJson, loaded);
    }

    [Fact]
    public async Task EnsureAsync_PreservesCreatedAtOnUpdate()
    {
        var svcV1 = CreateExampleService("""{"v":1}""");
        var svcV2 = CreateExampleService("""{"v":2}""");
        var dbFactory = CreateDbFactory();
        var storage = CreateStorage();

        await svcV1.EnsureAsync(dbFactory, storage, "user1");

        await using var db1 = await dbFactory.CreateDbContextAsync();
        var originalCreatedAt = (await db1.Projects
            .FirstAsync(p => p.UserId == "user1" && p.IsLatest)).CreatedAt;

        await svcV2.EnsureAsync(dbFactory, storage, "user1");

        await using var db2 = await dbFactory.CreateDbContextAsync();
        var latest = await db2.Projects
            .FirstAsync(p => p.UserId == "user1" && p.IsLatest);
        Assert.Equal(originalCreatedAt, latest.CreatedAt);
    }

    // ── User isolation ──────────────────────────────────────────────────

    [Fact]
    public async Task EnsureAsync_IsUserIsolated()
    {
        var svc = CreateExampleService();
        var dbFactory = CreateDbFactory();
        var storage = CreateStorage();

        await svc.EnsureAsync(dbFactory, storage, "user1");
        await svc.EnsureAsync(dbFactory, storage, "user2");

        await using var db = await dbFactory.CreateDbContextAsync();
        var user1Projects = await db.Projects
            .Where(p => p.UserId == "user1" && p.IsExample && p.IsLatest)
            .ToListAsync();
        var user2Projects = await db.Projects
            .Where(p => p.UserId == "user2" && p.IsExample && p.IsLatest)
            .ToListAsync();

        Assert.Single(user1Projects);
        Assert.Single(user2Projects);
        Assert.NotEqual(user1Projects[0].ProjectGroupId, user2Projects[0].ProjectGroupId);
    }

    // ── Unavailable file ────────────────────────────────────────────────

    [Fact]
    public async Task EnsureAsync_ReturnsFalse_WhenFileNotAvailable()
    {
        var svc = new ExampleProjectService(
            NullLogger<ExampleProjectService>.Instance,
            new[] { Path.Combine(_tempDir, "nonexistent.json") });
        var dbFactory = CreateDbFactory();
        var storage = CreateStorage();

        var result = await svc.EnsureAsync(dbFactory, storage, "user1");

        Assert.False(result);
        Assert.False(svc.IsAvailable);
    }
}
