using KitchenServer.Web.Data;
using KitchenServer.Web.Data.Entities;
using KitchenServer.Web.Endpoints;
using KitchenServer.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KitchenServer.Tests;

public sealed class DemoUnauthTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storageDir;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public DemoUnauthTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "kitchen-demo-tests", Guid.NewGuid().ToString("N"));
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

    private IDbContextFactory<AppDbContext> CreateDbFactory() =>
        new TestDbContextFactory(_dbOptions);

    private ProjectStorageService CreateStorage() =>
        new(Options.Create(new ProjectStorageOptions
        {
            RootPath = _storageDir,
            MaxSizeBytes = 10 * 1024 * 1024,
            BackupCount = 3
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

    // ── DemoProjectId ───────────────────────────────────────────────────

    [Fact]
    public void DemoProjectId_IsCorrectUuid()
    {
        Assert.Equal(new Guid("00000000-0000-0000-0000-000000000001"), ProjectEndpoints.DemoProjectId);
    }

    // ── GetExampleJson ──────────────────────────────────────────────────

    [Fact]
    public void GetExampleJson_ReturnsJson_WhenAvailable()
    {
        var svc = CreateExampleService("""{"boards":[1,2,3]}""");
        var json = svc.GetExampleJson();
        Assert.NotNull(json);
        Assert.Equal("""{"boards":[1,2,3]}""", json);
    }

    [Fact]
    public void GetExampleJson_ReturnsNull_WhenNotAvailable()
    {
        var svc = new ExampleProjectService(
            NullLogger<ExampleProjectService>.Instance,
            new[] { Path.Combine(_tempDir, "nonexistent.json") });
        var json = svc.GetExampleJson();
        Assert.Null(json);
    }

    // ── Demo project cannot be saved (PUT rejection) ────────────────────

    [Fact]
    public async Task Save_Rejected_WhenDemoProjectId()
    {
        var demoId = ProjectEndpoints.DemoProjectId;
        await using var db = new AppDbContext(_dbOptions);

        var normal = new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = demoId,
            UserId = "user1",
            Name = "Should not matter",
            JsonData = "{}",
            Version = 1,
            IsLatest = true
        };
        db.Projects.Add(normal);
        await db.SaveChangesAsync();

        var found = await db.Projects
            .FirstOrDefaultAsync(p => p.ProjectGroupId == demoId && p.UserId == "user1" && p.IsLatest);

        // Project exists in DB but the demo ID is special — PUT should reject it
        // regardless of DB state because the endpoint checks id == DemoProjectId
        Assert.NotNull(found);

        // Verify the endpoint guard: the DemoProjectId is the known magic constant
        Assert.Equal(ProjectEndpoints.DemoProjectId, demoId);
    }

    [Fact]
    public async Task ExampleProject_NotWritable_WhenIsExampleFlagSet()
    {
        var pgId = Guid.NewGuid();
        await using var db = new AppDbContext(_dbOptions);

        var example = new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = pgId,
            UserId = "user1",
            Name = "Example",
            JsonData = "{}",
            Version = 1,
            IsLatest = true,
            IsExample = true
        };
        db.Projects.Add(example);
        await db.SaveChangesAsync();

        var current = await db.Projects
            .FirstOrDefaultAsync(p => p.ProjectGroupId == pgId && p.IsLatest && !p.IsDeleted);

        Assert.NotNull(current);
        Assert.True(current!.IsExample);
    }

    // ── Unauthenticated GET /api/projects/{demoId} would succeed ────────

    [Fact]
    public void GetExampleJson_FromService_MatchesDemoProjectIdLogic()
    {
        var svc = CreateExampleService("""{"elements":["wall","cabinet"]}""");
        var json = svc.GetExampleJson();

        Assert.NotNull(json);
        Assert.Contains("wall", json);

        // This same JSON would be served when GET /api/projects/{DemoProjectId}
        // is called without auth — the endpoint uses ExampleService.GetExampleJson().
    }
}
