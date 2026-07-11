namespace KitchenServer.Web.Services;

public class ProjectStorageService
{
    private readonly string _rootPath;
    private readonly ILogger<ProjectStorageService> _logger;

    public ProjectStorageService(IConfiguration configuration, ILogger<ProjectStorageService> logger)
    {
        _logger = logger;
        _rootPath = configuration.GetValue<string>("ProjectStorage:RootPath") ?? "data/projects";

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

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    private string GetUserDir(string userId) =>
        Path.Combine(_rootPath, SanitizeName(userId));

    private string GetFilePath(Guid projectId, string userId) =>
        Path.Combine(GetUserDir(userId), $"{projectId}.json");

    public async Task Save(Guid projectId, string userId, string json)
    {
        var dir = GetUserDir(userId);
        Directory.CreateDirectory(dir);
        var targetPath = GetFilePath(projectId, userId);
        var tempPath = targetPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, targetPath, overwrite: true);
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

    public void Delete(Guid projectId, string userId)
    {
        var filePath = GetFilePath(projectId, userId);
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
