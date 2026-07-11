namespace KitchenServer.Web.Services;

public class ProjectStorageOptions
{
    public const string SectionName = "ProjectStorage";

    public string RootPath { get; set; } = "data/projects";

    /// <summary>Maximum accepted size of one project JSON, bytes.</summary>
    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>How many previous versions of a save to keep as .bak files.</summary>
    public int BackupCount { get; set; } = 3;
}
