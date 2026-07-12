namespace KitchenServer.Web.Services;

/// <summary>
/// Circuit-scoped bridge that lets the (interactive) editor page publish the
/// currently-open project to the (interactive) nav-bar island hosted in the
/// otherwise-static MainLayout. Both components live in the same Blazor Server
/// circuit, so they share this one scoped instance. The editor page clears it
/// on dispose, so other pages show an empty slot.
/// </summary>
public class EditorNavState
{
    public string? ProjectName { get; private set; }
    public Guid? ProjectId { get; private set; }
    public bool HasProject => ProjectId.HasValue;

    /// <summary>Raised whenever the current project changes (set or cleared).</summary>
    public event Action? Changed;

    public void Set(string name, Guid id)
    {
        ProjectName = name;
        ProjectId = id;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (ProjectName is null && ProjectId is null) return;
        ProjectName = null;
        ProjectId = null;
        Changed?.Invoke();
    }
}
