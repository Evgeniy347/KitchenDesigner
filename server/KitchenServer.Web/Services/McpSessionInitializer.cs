using Microsoft.AspNetCore.Http;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;

namespace KitchenServer.Web.Services;

/// <summary>
/// Hooks into the Streamable-HTTP MCP handshake so the server creates a session record
/// and advertises the <c>Mcp-Session-Id</c> header as soon as the client initializes.
/// Without this header the official MCP clients (Python/TS SDKs, opencode) do not send
/// a session id on later requests and every call after authenticate is rejected.
/// </summary>
public class McpSessionInitializer : ISessionMigrationHandler
{
    private readonly McpSessionManager _sessions;
    private readonly ILogger<McpSessionInitializer> _logger;

    public McpSessionInitializer(McpSessionManager sessions, ILogger<McpSessionInitializer> logger)
    {
        _sessions = sessions;
        _logger = logger;
    }

    public ValueTask OnSessionInitializedAsync(
        HttpContext context,
        string sessionId,
        InitializeRequestParams initParams,
        CancellationToken cancellationToken)
    {
        _sessions.CreateAnonymousSession(sessionId);
        context.Response.Headers[McpToolHandlers.SessionIdHeader] = sessionId;
        _logger.LogDebug("MCP session initialized: {SessionId}", sessionId);
        return ValueTask.CompletedTask;
    }

    public ValueTask<InitializeRequestParams?> AllowSessionMigrationAsync(
        HttpContext context,
        string sessionId,
        CancellationToken cancellationToken)
    {
        // Single-instance deployment: cross-server migration is not supported.
        return ValueTask.FromResult<InitializeRequestParams?>(null);
    }
}
