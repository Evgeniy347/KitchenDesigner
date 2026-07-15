using KitchenServer.Web.Data;
using KitchenServer.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace KitchenServer.Web.Services;

public class ExampleProjectService
{
    private string _exampleJson;
    private readonly string[] _searchPaths;
    private readonly ILogger<ExampleProjectService> _logger;

    internal static readonly string[] DefaultSearchPaths = new[]
    {
        "/app/seed/example.save.json",
        "seed/example.save.json",
        "example.save.json"
    };

    public ExampleProjectService(ILogger<ExampleProjectService> logger)
        : this(logger, DefaultSearchPaths) { }

    internal ExampleProjectService(ILogger<ExampleProjectService> logger, string[] searchPaths)
    {
        _logger = logger;
        _searchPaths = searchPaths;
        _exampleJson = LoadExampleFile();
    }

    public bool IsAvailable => _exampleJson.Length > 2;

    private string LoadExampleFile()
    {
        foreach (var path in _searchPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    if (json.Length > 2)
                    {
                        _logger.LogInformation("Example project loaded from {Path} ({Bytes} bytes)", path, json.Length);
                        return json;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read example project from {Path}", path);
                }
            }
        }
        _logger.LogInformation("No example project file found at any of: {Paths}", string.Join(", ", _searchPaths));
        return "{}";
    }

    /// <summary>
    /// Ensures the user has an example project. Creates it if missing,
    /// updates it (new version) if the example JSON has changed since last import.
    /// Returns true if a project was created or updated.
    /// </summary>
    public async Task<bool> EnsureAsync(IDbContextFactory<AppDbContext> dbFactory, ProjectStorageService storage, string userId)
    {
        if (!IsAvailable)
            return false;

        await using var db = await dbFactory.CreateDbContextAsync();

        var existing = await db.Projects
            .Where(p => p.UserId == userId && p.IsExample && p.IsLatest && !p.IsDeleted)
            .FirstOrDefaultAsync();

        if (existing is null)
            return await CreateExample(db, storage, userId);

        var storedJson = await storage.Load(existing.ProjectGroupId, userId) ?? existing.JsonData;
        if (storedJson == _exampleJson)
            return false;

        return await UpdateExample(db, storage, existing, userId);
    }

    public async Task<Guid?> GetOrCreateExampleIdAsync(IDbContextFactory<AppDbContext> dbFactory, ProjectStorageService storage, string userId)
    {
        await EnsureAsync(dbFactory, storage, userId);

        await using var db = await dbFactory.CreateDbContextAsync();
        var example = await db.Projects
            .Where(p => p.UserId == userId && p.IsExample && p.IsLatest && !p.IsDeleted)
            .Select(p => (Guid?)p.ProjectGroupId)
            .FirstOrDefaultAsync();
        return example;
    }

    private async Task<bool> CreateExample(AppDbContext db, ProjectStorageService storage, string userId)
    {
        var projectGroupId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var project = new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = projectGroupId,
            UserId = userId,
            Name = "Пример кухни",
            JsonData = _exampleJson,
            Version = 1,
            IsLatest = true,
            IsExample = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            await storage.Save(projectGroupId, userId, _exampleJson);
        }
        catch (ProjectStorageException ex)
        {
            _logger.LogWarning(ex, "Failed to save example project for user {UserId}", userId);
            return false;
        }

        db.Projects.Add(project);
        await db.SaveChangesAsync();
        _logger.LogInformation("Example project created for user {UserId}", userId);
        return true;
    }

    private async Task<bool> UpdateExample(AppDbContext db, ProjectStorageService storage, Project existing, string userId)
    {
        var now = DateTime.UtcNow;

        try
        {
            await storage.Save(existing.ProjectGroupId, userId, _exampleJson);
        }
        catch (ProjectStorageException ex)
        {
            _logger.LogWarning(ex, "Failed to update example project for user {UserId}", userId);
            return false;
        }

        var newVersion = new Project
        {
            Id = Guid.NewGuid(),
            ProjectGroupId = existing.ProjectGroupId,
            UserId = existing.UserId,
            Name = existing.Name,
            JsonData = _exampleJson,
            Version = existing.Version + 1,
            IsLatest = true,
            IsExample = true,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = now
        };

        existing.IsLatest = false;
        existing.UpdatedAt = now;

        db.Projects.Add(newVersion);
        await db.SaveChangesAsync();
        _logger.LogInformation("Example project updated to version {Version} for user {UserId}", newVersion.Version, userId);
        return true;
    }
}
