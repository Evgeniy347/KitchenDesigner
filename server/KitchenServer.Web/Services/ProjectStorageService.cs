using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace KitchenServer.Web.Services;

/// <summary>
/// File-based storage for user project saves. Files are the source of truth for
/// project content; the database keeps metadata (name, timestamps) plus a legacy
/// jsonb copy used only as a read fallback for projects saved before this service.
///
/// Layout: {root}/{userId}/{projectId}.json
/// Backups: {root}/{userId}/{projectId}.{yyyyMMddHHmmssfff}.bak (rotated, newest kept)
/// </summary>
public class ProjectStorageService
{
    private readonly string _rootPath;
    private readonly ProjectStorageOptions _options;
    private readonly ILogger<ProjectStorageService> _logger;

    public ProjectStorageService(IOptions<ProjectStorageOptions> options, ILogger<ProjectStorageService> logger)
    {
        _logger = logger;
        _options = options.Value;
        _rootPath = _options.RootPath;

        try
        {
            Directory.CreateDirectory(_rootPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create project storage root at {Path}, falling back to current directory", _rootPath);
            _rootPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "projects");
            Directory.CreateDirectory(_rootPath);
        }
    }

    public long MaxSizeBytes => _options.MaxSizeBytes;

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c) && c != '.').ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    private string GetUserDir(string userId) =>
        Path.Combine(_rootPath, SanitizeName(userId));

    private string GetFilePath(Guid projectId, string userId) =>
        Path.Combine(GetUserDir(userId), $"{projectId}.json");

    /// <summary>Validates and persists a save. Throws <see cref="ProjectStorageException"/> on rejected input.</summary>
    public async Task Save(Guid projectId, string userId, string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > _options.MaxSizeBytes)
            throw new ProjectStorageException($"Save rejected: exceeds {_options.MaxSizeBytes / 1048576} MB limit");

        try
        {
            using var _ = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ProjectStorageException($"Save rejected: invalid JSON ({ex.Message})");
        }

        var dir = GetUserDir(userId);
        Directory.CreateDirectory(dir);
        var targetPath = GetFilePath(projectId, userId);
        var tempPath = targetPath + ".tmp";

        await File.WriteAllTextAsync(tempPath, json);

        if (File.Exists(targetPath) && _options.BackupCount > 0)
            RotateBackups(targetPath, projectId, dir);

        File.Move(tempPath, targetPath, overwrite: true);
    }

    private void RotateBackups(string currentPath, Guid projectId, string dir)
    {
        try
        {
            var backupPath = Path.Combine(dir, $"{projectId}.{DateTime.UtcNow:yyyyMMddHHmmssfff}.bak");
            File.Copy(currentPath, backupPath, overwrite: true);

            var backups = Directory.GetFiles(dir, $"{projectId}.*.bak")
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .Skip(_options.BackupCount)
                .ToList();
            foreach (var stale in backups)
                File.Delete(stale);
        }
        catch (IOException ex)
        {
            // Backups are best-effort; the save itself must not fail because of them.
            _logger.LogWarning(ex, "Backup rotation failed for {ProjectId}", projectId);
        }
    }

    public async Task<string?> Load(Guid projectId, string userId)
    {
        var filePath = GetFilePath(projectId, userId);
        if (!File.Exists(filePath))
            return null;
        return await File.ReadAllTextAsync(filePath);
    }

    public List<(Guid Id, DateTime Updated)> List(string userId)
    {
        var dir = GetUserDir(userId);
        if (!Directory.Exists(dir))
            return new List<(Guid, DateTime)>();

        var files = Directory.GetFiles(dir, "*.json");
        var result = new List<(Guid, DateTime)>();
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (Guid.TryParse(name, out var id))
            {
                result.Add((id, File.GetLastWriteTimeUtc(file)));
            }
        }
        return result;
    }

    /// <summary>Total bytes used by a user's saves including backups.</summary>
    public long GetUsageBytes(string userId)
    {
        var dir = GetUserDir(userId);
        if (!Directory.Exists(dir))
            return 0;
        return Directory.GetFiles(dir).Sum(f => new FileInfo(f).Length);
    }

    /// <summary>Deletes a save and its backups.</summary>
    public void Delete(Guid projectId, string userId)
    {
        var dir = GetUserDir(userId);
        if (!Directory.Exists(dir))
            return;

        var targets = new List<string> { GetFilePath(projectId, userId) };
        targets.AddRange(Directory.GetFiles(dir, $"{projectId}.*.bak"));

        foreach (var filePath in targets)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Failed to delete project file {FilePath} due to I/O error, retrying once", filePath);
                try { File.Delete(filePath); } catch (IOException retryEx)
                {
                    _logger.LogWarning(retryEx, "Retry also failed to delete project file {FilePath}", filePath);
                }
            }
        }
    }
}

/// <summary>Thrown when a save is rejected (too large, invalid JSON). Message is safe to show the client.</summary>
public class ProjectStorageException : Exception
{
    public ProjectStorageException(string message) : base(message) { }
}
