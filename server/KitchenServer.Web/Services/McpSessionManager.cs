using System.Collections.Concurrent;
using System.Net.WebSockets;
using Microsoft.Extensions.Hosting;

namespace KitchenServer.Web.Services;

public class McpSessionManager : IHostedService
{
    private Timer? _cleanupTimer;
    private readonly ConcurrentDictionary<string, McpSession> _byAccessKey = new();
    private readonly ConcurrentDictionary<string, McpSession> _bySessionId = new();
    private readonly ConcurrentDictionary<string, McpSession> _byMCPConnection = new();
    private readonly ConcurrentDictionary<string, McpSession> _byBrowserConnection = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new();

    public TimeSpan SessionTtl { get; set; } = TimeSpan.FromMinutes(30);

    public McpSession CreateSession(string userId, string? projectId = null)
    {
        var session = new McpSession
        {
            UserId = userId,
            ProjectId = projectId,
            SessionTtl = SessionTtl
        };

        _byAccessKey[session.AccessKey] = session;
        _bySessionId[session.SessionId] = session;
        return session;
    }

    public McpSession? ValidateAccess(string accessKey)
    {
        _byAccessKey.TryGetValue(accessKey, out var session);
        return session;
    }

    public McpSession? GetSession(string sessionId)
    {
        _bySessionId.TryGetValue(sessionId, out var session);
        return session;
    }

    public McpSession? GetSessionByMCPConnection(string connectionId)
    {
        _byMCPConnection.TryGetValue(connectionId, out var session);
        return session;
    }

    public void RegisterMCPConnection(string sessionId, string connectionId)
    {
        if (_bySessionId.TryGetValue(sessionId, out var session))
        {
            session.MCPConnectionId = connectionId;
            _byMCPConnection[connectionId] = session;
        }
    }

    public void RegisterBrowserConnection(string sessionId, string connectionId)
    {
        if (_bySessionId.TryGetValue(sessionId, out var session))
        {
            session.BrowserConnectionId = connectionId;
            _byBrowserConnection[connectionId] = session;
        }
    }

    public void RemoveConnection(string connectionId)
    {
        if (_byMCPConnection.TryRemove(connectionId, out var session))
            session.MCPConnectionId = null;
        if (_byBrowserConnection.TryRemove(connectionId, out var session2))
            session2.BrowserConnectionId = null;
    }

    public void AddSessionCancellation(string sessionId, CancellationTokenSource cts)
    {
        _cancellations[sessionId] = cts;
    }

    public List<McpSession> GetUserSessions(string userId)
    {
        return _bySessionId.Values
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToList();
    }

    public bool CloseSession(string sessionId)
    {
        if (!_bySessionId.TryRemove(sessionId, out var session))
            return false;

        _byAccessKey.TryRemove(session.AccessKey, out _);
        if (session.MCPConnectionId != null)
            _byMCPConnection.TryRemove(session.MCPConnectionId, out _);
        if (session.BrowserConnectionId != null)
            _byBrowserConnection.TryRemove(session.BrowserConnectionId, out _);

        if (_cancellations.TryRemove(sessionId, out var cts))
        {
            try { cts.Cancel(); } catch { }
            try { cts.Dispose(); } catch { }
        }

        var ws = session.BrowserWebSocket;
        if (ws != null && (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived))
        {
            try { ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).GetAwaiter().GetResult(); }
            catch { }
        }
        session.BrowserWebSocket = null;

        return true;
    }

    public void Touch(string sessionId)
    {
        if (_bySessionId.TryGetValue(sessionId, out var session))
            session.Touch();
    }

    public void CleanupExpiredSessions()
    {
        var expired = _bySessionId.Values
            .Where(s => !s.IsActive)
            .Select(s => s.SessionId)
            .ToList();
        foreach (var id in expired)
            CloseSession(id);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cleanupTimer = new Timer(
            _ => CleanupExpiredSessions(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cleanupTimer?.Dispose();
        return Task.CompletedTask;
    }
}
