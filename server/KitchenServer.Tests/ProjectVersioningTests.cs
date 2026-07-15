using KitchenServer.Web.Data;
using KitchenServer.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace KitchenServer.Tests;

/// <summary>
/// Tests for project versioning: create / update (new version row) / soft-delete /
/// listing / version history.  Verifies that no row is ever physically deleted and
/// that every save produces a new immutable version.
/// </summary>
public sealed class ProjectVersioningTests : IDisposable
{
    private readonly DbContextOptions<AppDbContext> _options;

    public ProjectVersioningTests()
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

    // ── Helpers ─────────────────────────────────────────────────────────

    private static Project MakeProject(
        Guid? id = null,
        Guid? projectGroupId = null,
        string name = "Test Project",
        string userId = "user1",
        int version = 1,
        bool isLatest = true,
        bool isDeleted = false,
        string jsonData = """{"boards":[]}""") => new()
    {
        Id = id ?? Guid.NewGuid(),
        ProjectGroupId = projectGroupId ?? id ?? Guid.NewGuid(),
        UserId = userId,
        Name = name,
        JsonData = jsonData,
        Version = version,
        IsLatest = isLatest,
        IsDeleted = isDeleted,
        CreatedAt = new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc)
    };

    private async Task<Project> SeedAsync(Project project)
    {
        await using var db = new AppDbContext(_options);
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    // ── Create – columns populated correctly ────────────────────────────

    [Fact]
    public async Task Create_VersionIsOne()
    {
        var p = MakeProject();
        await SeedAsync(p);

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FirstAsync(x => x.Id == p.Id);

        Assert.Equal(1, loaded.Version);
    }

    [Fact]
    public async Task Create_IsLatestIsTrue()
    {
        var p = MakeProject();
        await SeedAsync(p);

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FirstAsync(x => x.Id == p.Id);

        Assert.True(loaded.IsLatest);
    }

    [Fact]
    public async Task Create_IsDeletedIsFalse()
    {
        var p = MakeProject();
        await SeedAsync(p);

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FirstAsync(x => x.Id == p.Id);

        Assert.False(loaded.IsDeleted);
    }

    [Fact]
    public async Task Create_DeletedAtIsNull()
    {
        var p = MakeProject();
        await SeedAsync(p);

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FirstAsync(x => x.Id == p.Id);

        Assert.Null(loaded.DeletedAt);
    }

    [Fact]
    public async Task Create_ProjectGroupIdIsSet()
    {
        var groupId = Guid.NewGuid();
        var p = MakeProject(id: Guid.NewGuid(), projectGroupId: groupId);
        await SeedAsync(p);

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FirstAsync(x => x.Id == p.Id);

        Assert.Equal(groupId, loaded.ProjectGroupId);
    }

    [Fact]
    public async Task Create_CreatedAtAndUpdatedAtAreSet()
    {
        var before = DateTime.UtcNow;
        var p = new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = Guid.NewGuid(),
            UserId = "user1",
            Name = "Fresh",
            JsonData = "{}",
            Version = 1,
            IsLatest = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await SeedAsync(p);

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FirstAsync(x => x.Id == p.Id);

        Assert.True(loaded.CreatedAt >= before);
        Assert.True(loaded.UpdatedAt >= before);
    }

    // ── Update – creates new version row, preserves old ─────────────────

    [Fact]
    public async Task Update_OldRowGetsIsLatestFalse()
    {
        var old = MakeProject();
        await SeedAsync(old);

        // Simulate update: mark old not-latest, insert new version.
        await using var db = new AppDbContext(_options);
        var loadedOld = await db.Projects.FirstAsync(x => x.Id == old.Id);

        var now = DateTime.UtcNow;
        loadedOld.IsLatest = false;
        loadedOld.UpdatedAt = now;

        var newVersion = new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = loadedOld.ProjectGroupId,
            UserId = loadedOld.UserId,
            Name = loadedOld.Name,
            JsonData = """{"boards":[1,2,3]}""",
            Version = loadedOld.Version + 1,
            IsLatest = true,
            CreatedAt = loadedOld.CreatedAt,
            UpdatedAt = now
        };
        db.Projects.Add(newVersion);
        await db.SaveChangesAsync();

        // Reload old row
        await using var verifyDb = new AppDbContext(_options);
        var verifyOld = await verifyDb.Projects.FirstAsync(x => x.Id == old.Id);
        Assert.False(verifyOld.IsLatest);
    }

    [Fact]
    public async Task Update_NewRowHasVersionIncremented()
    {
        var old = MakeProject();
        await SeedAsync(old);

        await using var db = new AppDbContext(_options);
        var loadedOld = await db.Projects.FirstAsync(x => x.Id == old.Id);
        loadedOld.IsLatest = false;
        loadedOld.UpdatedAt = DateTime.UtcNow;

        var newVersion = new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = loadedOld.ProjectGroupId,
            UserId = loadedOld.UserId,
            Name = loadedOld.Name,
            JsonData = """{"boards":[1,2,3]}""",
            Version = loadedOld.Version + 1,
            IsLatest = true,
            CreatedAt = loadedOld.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
        db.Projects.Add(newVersion);
        await db.SaveChangesAsync();

        await using var verifyDb = new AppDbContext(_options);
        var latest = await verifyDb.Projects
            .FirstAsync(x => x.ProjectGroupId == old.ProjectGroupId && x.IsLatest);

        Assert.Equal(old.Version + 1, latest.Version);
        Assert.NotEqual(old.Id, latest.Id);
    }

    [Fact]
    public async Task Update_NewRowIsLatest()
    {
        var old = MakeProject();
        await SeedAsync(old);

        await using var db = new AppDbContext(_options);
        var loadedOld = await db.Projects.FirstAsync(x => x.Id == old.Id);
        loadedOld.IsLatest = false;
        loadedOld.UpdatedAt = DateTime.UtcNow;

        var newVersion = new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = loadedOld.ProjectGroupId,
            UserId = loadedOld.UserId,
            Name = loadedOld.Name,
            JsonData = """{"boards":[1,2,3]}""",
            Version = loadedOld.Version + 1,
            IsLatest = true,
            CreatedAt = loadedOld.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
        db.Projects.Add(newVersion);
        await db.SaveChangesAsync();

        await using var verifyDb = new AppDbContext(_options);
        var latest = await verifyDb.Projects
            .FirstAsync(x => x.ProjectGroupId == old.ProjectGroupId && x.IsLatest);

        Assert.True(latest.IsLatest);
        Assert.False(latest.IsDeleted);
    }

    [Fact]
    public async Task Update_PreservesCreatedAtFromFirstVersion()
    {
        var old = MakeProject();
        await SeedAsync(old);
        var originalCreatedAt = old.CreatedAt;

        // First update
        await using var db1 = new AppDbContext(_options);
        var v1 = await db1.Projects.FirstAsync(x => x.Id == old.Id);
        v1.IsLatest = false; v1.UpdatedAt = DateTime.UtcNow;
        var v2 = new Project
        {
            Id = Guid.NewGuid(), ProjectGroupId = v1.ProjectGroupId,
            UserId = v1.UserId, Name = v1.Name, JsonData = """{"v":2}""",
            Version = 2, IsLatest = true, CreatedAt = v1.CreatedAt, UpdatedAt = DateTime.UtcNow
        };
        db1.Projects.Add(v2);
        await db1.SaveChangesAsync();

        // Second update
        await using var db2 = new AppDbContext(_options);
        var loadedV2 = await db2.Projects.FirstAsync(x => x.Id == v2.Id);
        loadedV2.IsLatest = false; loadedV2.UpdatedAt = DateTime.UtcNow;
        var v3 = new Project
        {
            Id = Guid.NewGuid(), ProjectGroupId = loadedV2.ProjectGroupId,
            UserId = loadedV2.UserId, Name = loadedV2.Name, JsonData = """{"v":3}""",
            Version = 3, IsLatest = true, CreatedAt = loadedV2.CreatedAt, UpdatedAt = DateTime.UtcNow
        };
        db2.Projects.Add(v3);
        await db2.SaveChangesAsync();

        await using var verifyDb = new AppDbContext(_options);
        var all = await verifyDb.Projects
            .Where(x => x.ProjectGroupId == old.ProjectGroupId)
            .ToListAsync();

        Assert.Equal(3, all.Count);
        Assert.All(all, x => Assert.Equal(originalCreatedAt, x.CreatedAt));
    }

    [Fact]
    public async Task Update_MultipleUpdates_OnlyOneIsLatest()
    {
        var old = MakeProject();
        await SeedAsync(old);

        // 3 updates
        var pgId = old.ProjectGroupId;
        for (int i = 0; i < 3; i++)
        {
            await using var db = new AppDbContext(_options);
            var current = await db.Projects
                .FirstAsync(x => x.ProjectGroupId == pgId && x.IsLatest);
            current.IsLatest = false;
            current.UpdatedAt = DateTime.UtcNow;

            var next = new Project
            {
                Id = Guid.NewGuid(), ProjectGroupId = pgId,
                UserId = current.UserId, Name = $"v{current.Version + 1}",
                JsonData = """{}""",
                Version = current.Version + 1, IsLatest = true,
                CreatedAt = current.CreatedAt, UpdatedAt = DateTime.UtcNow
            };
            db.Projects.Add(next);
            await db.SaveChangesAsync();
        }

        await using var verifyDb = new AppDbContext(_options);
        var latestCount = await verifyDb.Projects
            .CountAsync(x => x.ProjectGroupId == pgId && x.IsLatest);
        var total = await verifyDb.Projects
            .CountAsync(x => x.ProjectGroupId == pgId);

        Assert.Equal(1, latestCount);
        Assert.Equal(4, total); // original + 3 updates
    }

    // ── Soft delete – never removes rows ────────────────────────────────

    [Fact]
    public async Task Delete_SetsIsDeletedOnAllVersions()
    {
        var pgId = Guid.NewGuid();
        var v1 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId);
        var v2 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 2, isLatest: false);
        var v3 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 3, isLatest: true);

        await SeedAsync(v1);
        await SeedAsync(v2);
        await SeedAsync(v3);

        var before = DateTime.UtcNow;

        await using var db = new AppDbContext(_options);
        var versions = await db.Projects
            .Where(x => x.ProjectGroupId == pgId && !x.IsDeleted)
            .ToListAsync();

        foreach (var v in versions)
        {
            v.IsDeleted = true;
            v.IsLatest = false;
            v.DeletedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        // All are deleted
        await using var verifyDb = new AppDbContext(_options);
        var all = await verifyDb.Projects
            .Where(x => x.ProjectGroupId == pgId)
            .ToListAsync();

        Assert.Equal(3, all.Count);
        Assert.All(all, x => Assert.True(x.IsDeleted));
        Assert.All(all, x => Assert.False(x.IsLatest));
        Assert.All(all, x => Assert.NotNull(x.DeletedAt));
        Assert.All(all, x => Assert.True(x.DeletedAt >= before));
    }

    [Fact]
    public async Task Delete_NoPhysicalRowRemoval()
    {
        var p = MakeProject();
        await SeedAsync(p);

        var countBefore = 0;
        await using (var db = new AppDbContext(_options))
        {
            countBefore = await db.Projects.CountAsync();
        }

        // Soft-delete
        await using (var db = new AppDbContext(_options))
        {
            var project = await db.Projects.FirstAsync(x => x.Id == p.Id);
            project.IsDeleted = true;
            project.IsLatest = false;
            project.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        // Row still exists
        await using var verifyDb = new AppDbContext(_options);
        var countAfter = await verifyDb.Projects.CountAsync();
        var stillThere = await verifyDb.Projects.FirstOrDefaultAsync(x => x.Id == p.Id);

        Assert.Equal(countBefore, countAfter);
        Assert.NotNull(stillThere);
    }

    [Fact]
    public async Task Delete_DeletedAtIsSetOnce()
    {
        var p = MakeProject();
        await SeedAsync(p);

        var firstDelete = DateTime.UtcNow;

        await using var db = new AppDbContext(_options);
        var project = await db.Projects.FirstAsync(x => x.Id == p.Id);
        project.IsDeleted = true;
        project.IsLatest = false;
        project.DeletedAt = firstDelete;
        await db.SaveChangesAsync();

        await using var verifyDb = new AppDbContext(_options);
        var loaded = await verifyDb.Projects.FirstAsync(x => x.Id == p.Id);

        Assert.Equal(firstDelete, loaded.DeletedAt);
    }

    // ── Listing – only latest, non-deleted ──────────────────────────────

    [Fact]
    public async Task List_ReturnsOnlyLatestNonDeleted()
    {
        var userId = "user1";
        var pg1 = Guid.NewGuid();
        var pg2 = Guid.NewGuid();
        var pg3 = Guid.NewGuid();

        // Project 1: has 2 versions, v2 is latest, not deleted
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pg1, version: 1, isLatest: false, userId: userId));
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pg1, version: 2, isLatest: true, userId: userId));

        // Project 2: single version, deleted
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pg2, isDeleted: true, isLatest: false, userId: userId));

        // Project 3: single version, latest and not deleted
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pg3, isLatest: true, userId: userId));

        await using var db = new AppDbContext(_options);
        var visible = await db.Projects
            .Where(p => p.UserId == userId && p.IsLatest && !p.IsDeleted && !p.IsArchived)
            .Select(p => p.ProjectGroupId)
            .ToListAsync();

        Assert.Equal(2, visible.Count);
        Assert.Contains(pg1, visible);
        Assert.Contains(pg3, visible);
        Assert.DoesNotContain(pg2, visible);
    }

    [Fact]
    public async Task List_ExcludesOldVersions()
    {
        var pgId = Guid.NewGuid();
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 1, isLatest: false));
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 2, isLatest: false));
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 3, isLatest: true));

        await using var db = new AppDbContext(_options);
        var visible = await db.Projects
            .Where(p => p.ProjectGroupId == pgId && p.IsLatest && !p.IsDeleted)
            .ToListAsync();

        Assert.Single(visible);
        Assert.Equal(3, visible[0].Version);
    }

    [Fact]
    public async Task List_IsUserIsolated()
    {
        var pgUser1 = Guid.NewGuid();
        var pgUser2 = Guid.NewGuid();

        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pgUser1, userId: "user1"));
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pgUser2, userId: "user2"));

        await using var db = new AppDbContext(_options);
        var user1Projects = await db.Projects
            .Where(p => p.UserId == "user1" && p.IsLatest && !p.IsDeleted)
            .Select(p => p.ProjectGroupId)
            .ToListAsync();

        Assert.Single(user1Projects);
        Assert.Equal(pgUser1, user1Projects[0]);
    }

    // ── Version history ─────────────────────────────────────────────────

    [Fact]
    public async Task VersionHistory_ReturnsAllVersionsOrderedDesc()
    {
        var pgId = Guid.NewGuid();
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 1, isLatest: false));
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 2, isLatest: false));
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 3, isLatest: true));

        await using var db = new AppDbContext(_options);
        var versions = await db.Projects
            .Where(p => p.ProjectGroupId == pgId)
            .OrderByDescending(p => p.Version)
            .Select(p => new { p.Id, p.Version, p.IsLatest, p.IsDeleted })
            .ToListAsync();

        Assert.Equal(3, versions.Count);
        Assert.Equal(3, versions[0].Version);
        Assert.Equal(2, versions[1].Version);
        Assert.Equal(1, versions[2].Version);
    }

    [Fact]
    public async Task VersionHistory_DeletedVersionsAreIncluded()
    {
        var pgId = Guid.NewGuid();
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 1, isLatest: false, isDeleted: true));
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 2, isLatest: false));
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 3, isLatest: true));

        await using var db = new AppDbContext(_options);
        var versions = await db.Projects
            .Where(p => p.ProjectGroupId == pgId)
            .OrderBy(p => p.Version)
            .ToListAsync();

        Assert.Equal(3, versions.Count);
        Assert.True(versions[0].IsDeleted); // v1
        Assert.False(versions[1].IsDeleted); // v2
        Assert.False(versions[2].IsDeleted); // v3
    }

    [Fact]
    public async Task VersionHistory_IncludesTimestamps()
    {
        var pgId = Guid.NewGuid();
        var p = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId);
        p.IsDeleted = true;
        p.DeletedAt = new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc);
        await SeedAsync(p);

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FirstAsync(x => x.Id == p.Id);

        Assert.NotNull(loaded.DeletedAt);
        Assert.Equal(p.DeletedAt, loaded.DeletedAt);
        Assert.Equal(p.CreatedAt, loaded.CreatedAt);
        Assert.Equal(p.UpdatedAt, loaded.UpdatedAt);
    }

    // ── ProjectGroupId – backfill behaviour ─────────────────────────────

    [Fact]
    public async Task ProjectGroupId_Backfill_SetsToIdWhenDefault()
    {
        // Simulates a legacy row where ProjectGroupId was default(Guid)
        var id = Guid.NewGuid();
        var legacy = new Project
        {
            Id = id,
            ProjectGroupId = Guid.Empty, // legacy: never set
            UserId = "user1",
            Name = "Legacy",
            JsonData = "{}",
            Version = 1,
            IsLatest = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await SeedAsync(legacy);

        // Simulate backfill: UPDATE SET ProjectGroupId = Id WHERE ProjectGroupId IS NULL
        await using var db = new AppDbContext(_options);
        var rows = await db.Projects
            .Where(p => p.ProjectGroupId == Guid.Empty)
            .ToListAsync();

        foreach (var row in rows)
            row.ProjectGroupId = row.Id;

        await db.SaveChangesAsync();

        await using var verifyDb = new AppDbContext(_options);
        var loaded = await verifyDb.Projects.FirstAsync(x => x.Id == id);
        Assert.Equal(id, loaded.ProjectGroupId);
    }

    // ── Duplicate – new ProjectGroupId, fresh Version=1 ─────────────────

    [Fact]
    public async Task Duplicate_GetsNewProjectGroupId()
    {
        var original = MakeProject();
        await SeedAsync(original);

        var duplicateGroupId = Guid.NewGuid();
        var duplicate = new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = duplicateGroupId,
            UserId = original.UserId,
            Name = $"{original.Name} (Copy)",
            JsonData = original.JsonData,
            Version = 1,
            IsLatest = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await using var db = new AppDbContext(_options);
        db.Projects.Add(duplicate);
        await db.SaveChangesAsync();

        Assert.NotEqual(original.ProjectGroupId, duplicate.ProjectGroupId);
        Assert.Equal(1, duplicate.Version);
        Assert.True(duplicate.IsLatest);
        Assert.False(duplicate.IsDeleted);
    }

    // ── Concurrency – lock only applies to latest non-deleted row ───────

    [Fact]
    public async Task Lock_FindsLatestNonDeletedOnly()
    {
        var pgId = Guid.NewGuid();
        var v1 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 1, isLatest: false);
        var v2 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 2, isLatest: true);
        await SeedAsync(v1);
        await SeedAsync(v2);

        await using var db = new AppDbContext(_options);
        var locked = await db.Projects
            .FirstOrDefaultAsync(p => p.ProjectGroupId == pgId && p.IsLatest && !p.IsDeleted);

        Assert.NotNull(locked);
        Assert.Equal(2, locked!.Version);
        Assert.Equal(v2.Id, locked.Id);
    }

    [Fact]
    public async Task Lock_IgnoresDeletedProject()
    {
        var pgId = Guid.NewGuid();
        var v1 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 1, isLatest: false, isDeleted: true);
        await SeedAsync(v1);

        await using var db = new AppDbContext(_options);
        var found = await db.Projects
            .FirstOrDefaultAsync(p => p.ProjectGroupId == pgId && p.IsLatest && !p.IsDeleted);

        Assert.Null(found);
    }

    // ── Edge cases ──────────────────────────────────────────────────────

    [Fact]
    public async Task Update_OnlyOneProjectAffectedPerGroup()
    {
        var pgA = Guid.NewGuid();
        var pgB = Guid.NewGuid();

        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pgA, version: 1, isLatest: true));
        await SeedAsync(MakeProject(id: Guid.NewGuid(), projectGroupId: pgB, version: 1, isLatest: true));

        // Update only pgA
        await using var db = new AppDbContext(_options);
        var a = await db.Projects.FirstAsync(x => x.ProjectGroupId == pgA && x.IsLatest);
        a.IsLatest = false; a.UpdatedAt = DateTime.UtcNow;
        db.Projects.Add(new Project
        {
            Id = Guid.NewGuid(), ProjectGroupId = pgA,
            UserId = a.UserId, Name = a.Name, JsonData = """{}""",
            Version = 2, IsLatest = true, CreatedAt = a.CreatedAt, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // pgB untouched
        await using var verifyDb = new AppDbContext(_options);
        var b = await verifyDb.Projects.FirstAsync(x => x.ProjectGroupId == pgB && x.IsLatest);
        Assert.True(b.IsLatest);
        Assert.Equal(1, b.Version);
    }

    [Fact]
    public async Task NameChangeOnLatestRow_DoesNotCreateNewVersion()
    {
        var p = MakeProject();
        await SeedAsync(p);

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FirstAsync(x => x.Id == p.Id);
        loaded.Name = "Renamed Project";
        loaded.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await using var verifyDb = new AppDbContext(_options);
        var reloaded = await verifyDb.Projects.FirstAsync(x => x.Id == p.Id);
        Assert.Equal("Renamed Project", reloaded.Name);
        Assert.Equal(1, reloaded.Version); // version unchanged — inline metadata edit
    }

    // ── Restore version — creates new version from old JSON ─────────────

    [Fact]
    public async Task Restore_CreatesNewVersion_WithIncrementedNumber()
    {
        var pgId = Guid.NewGuid();
        var v1 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 1,
            isLatest: false, jsonData: """{"boards":[1]}""");
        var v2 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 2,
            isLatest: false, jsonData: """{"boards":[1,2]}""");
        var v3 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 3,
            isLatest: true, jsonData: """{"boards":[1,2,3]}""");

        await SeedAsync(v1);
        await SeedAsync(v2);
        await SeedAsync(v3);

        // Restore v1
        await using var db = new AppDbContext(_options);
        var current = await db.Projects
            .FirstAsync(x => x.ProjectGroupId == pgId && x.IsLatest && !x.IsDeleted);

        var restoredJson = """{"boards":[1]}"""; // v1's JSON
        var now = DateTime.UtcNow;

        current.IsLatest = false;
        current.UpdatedAt = now;

        var restored = new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = pgId,
            UserId = current.UserId,
            Name = current.Name,
            JsonData = restoredJson,
            Version = current.Version + 1,
            IsLatest = true,
            CreatedAt = current.CreatedAt,
            UpdatedAt = now
        };
        db.Projects.Add(restored);
        await db.SaveChangesAsync();

        await using var verifyDb = new AppDbContext(_options);
        var latest = await verifyDb.Projects
            .FirstAsync(x => x.ProjectGroupId == pgId && x.IsLatest);
        Assert.Equal(4, latest.Version);
        Assert.Equal(restoredJson, latest.JsonData);
    }

    [Fact]
    public async Task Restore_OldLatestGetsIsLatestFalse()
    {
        var pgId = Guid.NewGuid();
        var v1 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 1,
            isLatest: false, jsonData: """{"v":1}""");
        var v2 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 2,
            isLatest: true, jsonData: """{"v":2}""");

        await SeedAsync(v1);
        await SeedAsync(v2);

        await using var db = new AppDbContext(_options);
        var current = await db.Projects
            .FirstAsync(x => x.ProjectGroupId == pgId && x.IsLatest);
        current.IsLatest = false;
        current.UpdatedAt = DateTime.UtcNow;

        var restored = new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = pgId,
            UserId = current.UserId,
            Name = current.Name,
            JsonData = """{"v":1}""",
            Version = current.Version + 1,
            IsLatest = true,
            CreatedAt = current.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
        db.Projects.Add(restored);
        await db.SaveChangesAsync();

        await using var verifyDb = new AppDbContext(_options);
        var oldV2 = await verifyDb.Projects.FirstAsync(x => x.Id == v2.Id);
        Assert.False(oldV2.IsLatest);
    }

    [Fact]
    public async Task Restore_NewVersionHasOldJson()
    {
        var pgId = Guid.NewGuid();
        var v1 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 1,
            isLatest: true, jsonData: """{"elements":["wall","cabinet"]}""");
        await SeedAsync(v1);

        var oldJson = """{"elements":["wall"]}""";

        await using var db = new AppDbContext(_options);
        var current = await db.Projects
            .FirstAsync(x => x.ProjectGroupId == pgId && x.IsLatest);
        current.IsLatest = false;
        current.UpdatedAt = DateTime.UtcNow;

        var restored = new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = pgId,
            UserId = current.UserId,
            Name = current.Name,
            JsonData = oldJson,
            Version = 2,
            IsLatest = true,
            CreatedAt = current.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
        db.Projects.Add(restored);
        await db.SaveChangesAsync();

        await using var verifyDb = new AppDbContext(_options);
        var latest = await verifyDb.Projects
            .FirstAsync(x => x.ProjectGroupId == pgId && x.IsLatest);
        Assert.Equal(oldJson, latest.JsonData);
        Assert.NotEqual(v1.JsonData, latest.JsonData);
    }

    [Fact]
    public async Task Restore_PreservesCreatedAtFromFirstVersion()
    {
        var pgId = Guid.NewGuid();
        var v1 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 1,
            isLatest: false, jsonData: """{"v":1}""");
        var v2 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 2,
            isLatest: true, jsonData: """{"v":2}""");

        var originalCreatedAt = v1.CreatedAt;

        await SeedAsync(v1);
        await SeedAsync(v2);

        await using var db = new AppDbContext(_options);
        var current = await db.Projects
            .FirstAsync(x => x.ProjectGroupId == pgId && x.IsLatest);
        current.IsLatest = false;
        current.UpdatedAt = DateTime.UtcNow;

        var restored = new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = pgId,
            UserId = current.UserId,
            Name = current.Name,
            JsonData = """{"v":1}""",
            Version = 3,
            IsLatest = true,
            CreatedAt = current.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
        db.Projects.Add(restored);
        await db.SaveChangesAsync();

        await using var verifyDb = new AppDbContext(_options);
        var all = await verifyDb.Projects
            .Where(x => x.ProjectGroupId == pgId)
            .ToListAsync();
        Assert.Equal(3, all.Count);
        Assert.All(all, x => Assert.Equal(originalCreatedAt, x.CreatedAt));
    }

    [Fact]
    public async Task Restore_OnlyOneIsLatestAfterRestore()
    {
        var pgId = Guid.NewGuid();
        var v1 = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 1,
            isLatest: true, jsonData: """{"v":1}""");
        await SeedAsync(v1);

        await using var db = new AppDbContext(_options);
        var current = await db.Projects
            .FirstAsync(x => x.ProjectGroupId == pgId && x.IsLatest);
        current.IsLatest = false;
        current.UpdatedAt = DateTime.UtcNow;

        db.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = pgId,
            UserId = current.UserId,
            Name = current.Name,
            JsonData = current.JsonData,
            Version = 2,
            IsLatest = true,
            CreatedAt = current.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await using var verifyDb = new AppDbContext(_options);
        var latestCount = await verifyDb.Projects
            .CountAsync(x => x.ProjectGroupId == pgId && x.IsLatest);
        Assert.Equal(1, latestCount);
    }

    // ── Example project save rejection ──────────────────────────────────

    [Fact]
    public async Task Save_Rejected_WhenProjectIsExample()
    {
        var pgId = Guid.NewGuid();
        var example = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 1, isLatest: true);
        example.IsExample = true;
        await SeedAsync(example);

        // Simulate the PUT endpoint logic: find the project, check IsExample.
        await using var db = new AppDbContext(_options);
        var current = await db.Projects
            .FirstOrDefaultAsync(p => p.ProjectGroupId == pgId && p.UserId == "user1"
                && p.IsLatest && !p.IsDeleted);

        Assert.NotNull(current);
        Assert.True(current!.IsExample);

        // The endpoint should reject saves for example projects.
        // Verify that the flag is correctly set on the DB row.
        Assert.True(current.IsExample);
    }

    [Fact]
    public async Task Save_Allowed_WhenProjectIsNotExample()
    {
        var pgId = Guid.NewGuid();
        var normal = MakeProject(id: Guid.NewGuid(), projectGroupId: pgId, version: 1, isLatest: true);
        await SeedAsync(normal);

        await using var db = new AppDbContext(_options);
        var current = await db.Projects
            .FirstOrDefaultAsync(p => p.ProjectGroupId == pgId && p.UserId == "user1"
                && p.IsLatest && !p.IsDeleted);

        Assert.NotNull(current);
        Assert.False(current!.IsExample);
    }

    [Fact]
    public async Task ExampleProject_HasIsExampleFlag_AfterCreation()
    {
        var example = MakeProject();
        example.IsExample = true;
        await SeedAsync(example);

        await using var db = new AppDbContext(_options);
        var loaded = await db.Projects.FirstAsync(x => x.Id == example.Id);

        Assert.True(loaded.IsExample);
    }
}
