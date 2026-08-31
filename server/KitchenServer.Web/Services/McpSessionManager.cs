using System.Collections.Concurrent;
using System.Net.WebSockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KitchenServer.Web.Services;

/// <summary>
/// Tracks open editor tabs (keyed by their ephemeral <see cref="McpSession.TabKey"/>)
/// and the MCP agent sessions bound to them. The agent authenticates with a tab key
/// (first tool call); after that its MCP session forwards commands to that tab.
/// </summary>
public class McpSessionManager : IHostedService, IDisposable
{
    private Timer? _cleanupTimer;
    private readonly ConcurrentDictionary<string, McpSession> _byKey = new();
    // MCP server instance (one per Streamable-HTTP session) -> the tab it is bound to.
    // In Streamable-HTTP transport ctx.Server is a different object per request, so
    // the authoritative binding is the Mcp-Session-Id header value (see _sessionBindings).
    private readonly ConcurrentDictionary<object, McpSession> _agentBindings = new();
    private readonly ConcurrentDictionary<string, McpSession> _sessionBindings = new();
    private readonly ConcurrentDictionary<string, McpSession> _endpointBindings = new();
    private readonly ConcurrentDictionary<string, McpSession> _ipBindings = new();
    private readonly ConcurrentDictionary<string, McpSession> _mcpConnections = new();

    public McpSessionManager() { }

    public McpSessionManager(IOptions<McpOptions> options)
    {
        SessionTtl = TimeSpan.FromMinutes(options.Value.SessionTtlMinutes);
    }

    public TimeSpan SessionTtl { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Raised when a tab's connection state changes (browser attach/detach,
    /// agent bind/unbind). The editor nav island uses it to flip the status light.</summary>
    public event Action<McpSession>? SessionStateChanged;

    public void RaiseSessionStateChanged(McpSession session) =>
        SessionStateChanged?.Invoke(session);

    /// <summary>Create (register) a tab session for a freshly generated key.</summary>
    public McpSession CreateSession(string userId, string? projectId = null)
    {
        var session = new McpSession { UserId = userId, ProjectId = projectId, SessionTtl = SessionTtl };
        _byKey[session.TabKey] = session;
        return session;
    }

    /// <summary>
    /// Create an anonymous MCP session for a Streamable-HTTP session id that has not
    /// authenticated yet. The session will be bound to a real project tab in
    /// <see cref="BindAgentToSessionId"/>.
    /// </summary>
    public McpSession CreateAnonymousSession(string sessionId)
    {
        var session = new McpSession
        {
            TabKey = sessionId,
            UserId = "agent",
            ProjectId = null,
            SessionTtl = SessionTtl,
        };
        _byKey[sessionId] = session;
        return session;
    }

    public McpSession? GetByKey(string key) =>
        string.IsNullOrEmpty(key) ? null : _byKey.GetValueOrDefault(key);

    /// <summary>Used by the project-lock takeover path to reach the previous tab.</summary>
    public McpSession? GetSessionByProjectId(string projectId) =>
        _byKey.Values.FirstOrDefault(s => s.ProjectId == projectId);

    /// <summary>Attach the browser WebSocket for a (pre-registered) tab key.</summary>
    public McpSession? AttachBrowser(string key, WebSocket ws)
    {
        var session = GetByKey(key);
        if (session == null) return null;
        session.BrowserWebSocket = ws;
        session.Touch();
        SessionStateChanged?.Invoke(session);
        return session;
    }

    public void DetachBrowser(McpSession session)
    {
        session.BrowserWebSocket = null;
        session.FailAllPending("Browser disconnected.");
        SessionStateChanged?.Invoke(session);
    }

    /// <summary>
    /// Bind an authenticated agent to a tab. Returns a stable session id the client must
    /// send in the <c>Mcp-Session-Id</c> header on subsequent tool calls.
    /// </summary>
    public string BindAgent(object mcpServer, McpSession session)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        _agentBindings[mcpServer] = session;
        _sessionBindings[sessionId] = session;
        session.AgentBound = true;
        session.Touch();
        SessionStateChanged?.Invoke(session);
        return sessionId;
    }

    /// <summary>
    /// Bind an already-known Streamable-HTTP session id to a real project tab.
    /// Used when the client obtained its session id during the MCP initialize handshake.
    /// </summary>
    public void BindAgentToSessionId(string sessionId, McpSession session)
    {
        _sessionBindings[sessionId] = session;
        session.AgentBound = true;
        session.Touch();

        // Remove the temporary anonymous placeholder, if any.
        _byKey.TryRemove(sessionId, out _);

        SessionStateChanged?.Invoke(session);
    }

    /// <summary>
    /// Bind an authenticated agent to its remote endpoint (IP:port). Used as the primary
    /// fallback for clients (like opencode) that operate in stateless Streamable-HTTP mode
    /// and do not send a session id header. Different browser tabs / opencode windows from
    /// the same public IP usually use different source ports, so this keeps their sessions
    /// separate. If only the IP is known, it falls back to IP-only binding.
    /// </summary>
    public void BindEndpoint(string? endpoint, string? ipAddress, McpSession session)
    {
        session.AgentBound = true;
        session.Touch();

        if (!string.IsNullOrEmpty(endpoint))
            _endpointBindings[endpoint] = session;

        if (!string.IsNullOrEmpty(ipAddress))
            _ipBindings[ipAddress] = session;

        SessionStateChanged?.Invoke(session);
    }

    /// <summary>
    /// Look up the tab bound to this agent. Prefer the <paramref name="sessionId"/> header
    /// value because <paramref name="mcpServer"/> is not stable across Streamable-HTTP requests.
    /// Falls back to the remote endpoint (IP:port) and then to the IP address for stateless clients.
    /// </summary>
    public McpSession? GetBoundSession(
        object mcpServer,
        string? sessionId = null,
        string? endpoint = null,
        string? ipAddress = null)
    {
        if (!string.IsNullOrEmpty(sessionId) && _sessionBindings.TryGetValue(sessionId, out var byHeader))
            return byHeader;

        var byAgent = _agentBindings.GetValueOrDefault(mcpServer);
        if (byAgent != null) return byAgent;

        if (!string.IsNullOrEmpty(endpoint) && _endpointBindings.TryGetValue(endpoint, out var byEndpoint))
            return byEndpoint;

        if (!string.IsNullOrEmpty(ipAddress) && _ipBindings.TryGetValue(ipAddress, out var byIp))
            return byIp;

        return null;
    }

    public void UnbindAgent(object mcpServer)
    {
        if (_agentBindings.TryRemove(mcpServer, out var session))
        {
            session.AgentBound = _agentBindings.Values.Contains(session);
            SessionStateChanged?.Invoke(session);
        }
    }

    public bool CloseSession(string key)
    {
        if (!_byKey.TryRemove(key, out var session)) return false;

        foreach (var kv in _agentBindings.Where(kv => kv.Value == session).ToList())
            _agentBindings.TryRemove(kv.Key, out _);

        foreach (var kv in _sessionBindings.Where(kv => kv.Value == session).ToList())
            _sessionBindings.TryRemove(kv.Key, out _);

        foreach (var kv in _mcpConnections.Where(kv => kv.Value == session).ToList())
            _mcpConnections.TryRemove(kv.Key, out _);

        session.FailAllPending("Session closed.");
        var ws = session.BrowserWebSocket;
        if (ws != null && (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived))
        {
            try { ws.Abort(); } catch { }
        }
        session.BrowserWebSocket = null;
        SessionStateChanged?.Invoke(session);
        session.Dispose();
        return true;
    }

    public void CleanupExpiredSessions()
    {
        var expired = _byKey.Values
            .Where(s => !s.IsBrowserConnected && DateTime.UtcNow - s.LastActivity > s.SessionTtl)
            .Select(s => s.TabKey)
            .ToList();
        foreach (var key in expired)
            CloseSession(key);
    }

    public McpSession? ValidateAccess(string key) => GetByKey(key);

    public void RegisterMCPConnection(string sessionId, string connectionId)
    {
        var session = GetByKey(sessionId);
        if (session != null)
            _mcpConnections[connectionId] = session;
    }

    public void RemoveConnection(string connectionId) =>
        _mcpConnections.TryRemove(connectionId, out _);

    public McpSession? GetSessionByMCPConnection(string connectionId) =>
        _mcpConnections.GetValueOrDefault(connectionId);

    public void Touch(string sessionId) => GetByKey(sessionId)?.Touch();

    public List<McpSession> GetUserSessions(string userId) =>
        _byKey.Values.Where(s => s.UserId == userId).ToList();

    public McpSession? GetSession(string sessionId) => GetByKey(sessionId);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cleanupTimer = new Timer(_ => CleanupExpiredSessions(), null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Stops the cleanup timer and releases every live tab; idempotent, StopAsync routes here.</summary>
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        foreach (var key in _byKey.Keys.ToList())
            CloseSession(key);
        GC.SuppressFinalize(this);
    }
}
