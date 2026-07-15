namespace KitchenServer.Web.Services;

/// <summary>
/// Circuit-scoped bridge that lets the (interactive) editor page publish the
/// currently-open project — its name/id, the per-tab MCP project key, and whether
/// an agent is connected — to the (interactive) nav-bar island in MainLayout. Both
/// live in the same Blazor Server circuit, so they share this one scoped instance.
/// </summary>
public class EditorNavState
{
    public string? ProjectName { get; private set; }
    public Guid? ProjectId { get; private set; }
    public bool HasProject => ProjectId.HasValue;

    /// <summary>Per-tab MCP project key the user copies to the agent (null off the editor page).</summary>
    public string? McpKey { get; private set; }

    /// <summary>True once an agent has authenticated against this tab (green light).</summary>
    public bool AgentConnected { get; private set; }

    /// <summary>True when another tab has taken the project lock — changes will not be saved.</summary>
    public bool LockLost { get; private set; }

    /// <summary>True when the current editor tab is the demo/example project.</summary>
    public bool IsDemo { get; private set; }

    /// <summary>Raised whenever anything above changes.</summary>
    public event Action? Changed;

    public void Set(string name, Guid id, string mcpKey)
    {
        ProjectName = name;
        ProjectId = id;
        McpKey = mcpKey;
        AgentConnected = false;
        Changed?.Invoke();
    }

    public void SetAgentConnected(bool connected)
    {
        if (AgentConnected == connected) return;
        AgentConnected = connected;
        Changed?.Invoke();
    }

    public void SetLockLost(bool lost)
    {
        if (LockLost == lost) return;
        LockLost = lost;
        Changed?.Invoke();
    }

    public void SetDemo(bool demo)
    {
        if (IsDemo == demo) return;
        IsDemo = demo;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (ProjectName is null && ProjectId is null && McpKey is null && !IsDemo) return;
        ProjectName = null;
        ProjectId = null;
        McpKey = null;
        AgentConnected = false;
        IsDemo = false;
        Changed?.Invoke();
    }
}
