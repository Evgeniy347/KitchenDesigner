using KitchenServer.Web.Data;
using KitchenServer.Web.Data.Entities;
using KitchenServer.Web.Endpoints;
using KitchenServer.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace KitchenServer.Tests;

/// <summary>
/// Tests for <see cref="ProjectLockHelper"/>: acquire / release project locks.
/// Uses EF Core InMemory provider to avoid PostgreSQL dependency.
/// </summary>
public sealed class ProjectLockHelperTests : IDisposable
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly McpSessionManager _sessions = new();

    public ProjectLockHelperTests()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    public void Dispose()
    {
        using var db = new AppDbContext(_options);
        db.Database.EnsureDeleted();
    }

    private static Project MakeProject(string? lockGuid = null, DateTime? lockAcquiredAt = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = "user1",
        Name = "Test Project",
        LockGuid = lockGuid,
        LockAcquiredAt = lockAcquiredAt
    };

    private async Task<Project> SeedProjectAsync(Project? project = null)
    {
        project ??= MakeProject();
        await using var db = new AppDbContext(_options);
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    // ── AcquireLockAsync ────────────────────────────────────────────────

    [Fact]
    public async Task AcquireLock_SetsNewGuidOnProject()
    {
        var project = await SeedProjectAsync();

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FindAsync(project.Id)!;

        var result = await ProjectLockHelper.AcquireLockAsync(db, _sessions, loaded!);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(result, loaded!.LockGuid);
    }

    [Fact]
    public async Task AcquireLock_SetsTimestamp()
    {
        var project = await SeedProjectAsync();
        var before = DateTime.UtcNow;

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FindAsync(project.Id)!;

        await ProjectLockHelper.AcquireLockAsync(db, _sessions, loaded!);

        Assert.NotNull(loaded!.LockAcquiredAt);
        Assert.True(loaded.LockAcquiredAt >= before);
        Assert.True(loaded.LockAcquiredAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task AcquireLock_OnUnlockedProject_SetsGuid()
    {
        var project = await SeedProjectAsync(MakeProject());

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FindAsync(project.Id)!;

        Assert.Null(loaded!.LockGuid);

        var result = await ProjectLockHelper.AcquireLockAsync(db, _sessions, loaded);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task AcquireLock_OnAlreadyLockedProject_OverwritesGuid()
    {
        var oldGuid = "old-lock-guid-123";
        var project = await SeedProjectAsync(MakeProject(lockGuid: oldGuid, lockAcquiredAt: DateTime.UtcNow.AddMinutes(-5)));

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FindAsync(project.Id)!;

        var newGuid = await ProjectLockHelper.AcquireLockAsync(db, _sessions, loaded!);

        Assert.NotEqual(oldGuid, newGuid);
        Assert.Equal(newGuid, loaded!.LockGuid);
    }

    [Fact]
    public async Task AcquireLock_Twice_ReturnsDifferentGuids()
    {
        var project = await SeedProjectAsync();

        await using var db1 = new AppDbContext(_options);
        var loaded1 = await db1.Projects.FindAsync(project.Id)!;
        var guid1 = await ProjectLockHelper.AcquireLockAsync(db1, _sessions, loaded1!);

        await using var db2 = new AppDbContext(_options);
        var loaded2 = await db2.Projects.FindAsync(project.Id)!;
        var guid2 = await ProjectLockHelper.AcquireLockAsync(db2, _sessions, loaded2!);

        Assert.NotEqual(guid1, guid2);
    }

    [Fact]
    public async Task AcquireLock_PersistsToDatabase()
    {
        var project = await SeedProjectAsync();

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FindAsync(project.Id)!;
        var result = await ProjectLockHelper.AcquireLockAsync(db, _sessions, loaded!);

        // Re-read from a fresh context to confirm persistence.
        await using var verify = new AppDbContext(_options);
        var fresh = await verify.Projects.FindAsync(project.Id);

        Assert.Equal(result, fresh!.LockGuid);
        Assert.NotNull(fresh.LockAcquiredAt);
    }

    // ── ReleaseLockAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ReleaseLock_CorrectGuid_ReleasesAndReturnsTrue()
    {
        var project = await SeedProjectAsync();
        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FindAsync(project.Id)!;

        var lockGuid = await ProjectLockHelper.AcquireLockAsync(db, _sessions, loaded!);
        var released = await ProjectLockHelper.ReleaseLockAsync(db, loaded!, lockGuid!);

        Assert.True(released);
        Assert.Null(loaded!.LockGuid);
        Assert.Null(loaded.LockAcquiredAt);
    }

    [Fact]
    public async Task ReleaseLock_WrongGuid_ReturnsFalse()
    {
        var project = await SeedProjectAsync(MakeProject(lockGuid: "real-guid", lockAcquiredAt: DateTime.UtcNow));

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FindAsync(project.Id)!;

        var released = await ProjectLockHelper.ReleaseLockAsync(db, loaded!, "wrong-guid");

        Assert.False(released);
        Assert.Equal("real-guid", loaded!.LockGuid);
    }

    [Fact]
    public async Task ReleaseLock_AlreadyReleased_ReturnsFalse()
    {
        var project = await SeedProjectAsync(MakeProject());

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FindAsync(project.Id)!;

        // Project has no lock — releasing should fail.
        var released = await ProjectLockHelper.ReleaseLockAsync(db, loaded!, "any-guid");

        Assert.False(released);
    }

    [Fact]
    public async Task ReleaseLock_Twice_SecondFails()
    {
        var project = await SeedProjectAsync();
        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FindAsync(project.Id)!;

        var lockGuid = await ProjectLockHelper.AcquireLockAsync(db, _sessions, loaded!);
        Assert.True(await ProjectLockHelper.ReleaseLockAsync(db, loaded!, lockGuid!));

        // Reload to reset state
        var reloaded = await db.Projects.FindAsync(project.Id)!;
        var second = await ProjectLockHelper.ReleaseLockAsync(db, reloaded!, "stale-guid");

        Assert.False(second);
    }

    [Fact]
    public async Task ReleaseLock_PersistsToDatabase()
    {
        var project = await SeedProjectAsync();
        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FindAsync(project.Id)!;

        var lockGuid = await ProjectLockHelper.AcquireLockAsync(db, _sessions, loaded!);
        await ProjectLockHelper.ReleaseLockAsync(db, loaded!, lockGuid!);

        // Re-read from a fresh context.
        await using var verify = new AppDbContext(_options);
        var fresh = await verify.Projects.FindAsync(project.Id);

        Assert.Null(fresh!.LockGuid);
        Assert.Null(fresh.LockAcquiredAt);
    }

    // ── WebSocket notification (no real socket, just verify no crash) ───

    [Fact]
    public async Task AcquireLock_WithNoPreviousHolder_DoesNotThrow()
    {
        var project = await SeedProjectAsync(MakeProject());

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FindAsync(project.Id)!;

        // No session registered — should just acquire without notification.
        var result = await ProjectLockHelper.AcquireLockAsync(db, _sessions, loaded!);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task AcquireLock_WithPreviousSessionButNoSocket_DoesNotThrow()
    {
        var projectId = Guid.NewGuid();
        var session = _sessions.CreateSession("user1", projectId.ToString());

        var project = new Project
        {
            Id = projectId,
            UserId = "user1",
            Name = "Test",
            LockGuid = "old-guid",
            LockAcquiredAt = DateTime.UtcNow.AddMinutes(-1)
        };

        await using var db = new AppDbContext(_options);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var loaded = await db.Projects.FindAsync(projectId)!;

        // Session exists but BrowserWebSocket is null — should not throw.
        var result = await ProjectLockHelper.AcquireLockAsync(db, _sessions, loaded!);

        Assert.NotNull(result);
        Assert.NotEqual("old-guid", result);
    }
}
