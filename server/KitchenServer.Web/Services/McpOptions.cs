namespace KitchenServer.Web.Services;

public class McpOptions
{
    public const string SectionName = "Mcp";

    /// <summary>
    /// Dedicated MCP port. When set, agent-facing MCP routes are served ONLY on this
    /// port and all other routes are rejected on it (see <see cref="McpPortGate"/>).
    /// When null, no port separation is applied (local development).
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Public base URL advertised to MCP agents, e.g. "ws://example.com:8081".
    /// When null, URLs are derived from the incoming request host and <see cref="Port"/>.
    /// </summary>
    public string? PublicUrl { get; set; }

    public int SessionTtlMinutes { get; set; } = 30;
}
