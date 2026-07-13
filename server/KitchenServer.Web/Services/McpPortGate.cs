namespace KitchenServer.Web.Services;

/// <summary>
/// Partitions routes between the public HTTP port and the dedicated MCP port.
/// The MCP port serves only the agent-facing MCP endpoint (+ health); the HTTP
/// port serves everything else, and must NOT expose the MCP endpoint.
/// </summary>
public static class McpPortGate
{
    private static readonly string[] McpPathPrefixes =
    {
        "/mcp",     // real MCP over Streamable HTTP (agents connect here)
        "/health",
        "/alive",
    };

    /// <summary>Paths allowed on the MCP port.</summary>
    public static bool IsMcpPath(PathString path) =>
        McpPathPrefixes.Any(p => path.StartsWithSegments(p));

    /// <summary>Agent-only paths that must NOT be reachable via the public HTTP port.</summary>
    public static bool IsMcpOnlyPath(PathString path) =>
        path.StartsWithSegments("/mcp");

    /// <returns>true when the request must be rejected with 404.</returns>
    public static bool ShouldReject(int localPort, PathString path, int mcpPort)
    {
        if (localPort == mcpPort)
            return !IsMcpPath(path);
        return IsMcpOnlyPath(path);
    }
}
