namespace KitchenServer.Web.Services;

/// <summary>
/// Partitions routes between the public HTTP port and the dedicated MCP port.
/// MCP port serves only agent-facing routes; the HTTP port serves everything else.
/// </summary>
public static class McpPortGate
{
    private static readonly string[] McpPathPrefixes =
    {
        "/hubs/mcp",        // SignalR hub for MCP agents
        "/api/mcp/connect", // agent handshake
        "/api/mcp/ws",      // raw WS channel (browser / desktop client)
        "/health",
        "/alive",
    };

    /// <summary>Paths allowed on the MCP port.</summary>
    public static bool IsMcpPath(PathString path) =>
        McpPathPrefixes.Any(p => path.StartsWithSegments(p));

    /// <summary>Agent-only paths that must NOT be reachable via the public HTTP port.</summary>
    public static bool IsMcpOnlyPath(PathString path) =>
        path.StartsWithSegments("/hubs/mcp");

    /// <returns>true when the request must be rejected with 404.</returns>
    public static bool ShouldReject(int localPort, PathString path, int mcpPort)
    {
        if (localPort == mcpPort)
            return !IsMcpPath(path);
        return IsMcpOnlyPath(path);
    }
}
