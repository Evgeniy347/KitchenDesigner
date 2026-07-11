using System.Net.WebSockets;
using System.Text;

namespace KitchenServer.Web.Services;

public class McpSession
{
    private readonly object _lock = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private string? _mcpConnectionId;
    private string? _browserConnectionId;
    private WebSocket? _browserWebSocket;
    private DateTime _lastActivity = DateTime.UtcNow;

    public string SessionId { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public string UserId { get; init; } = "";
    public string AccessKey { get; init; } = Guid.NewGuid().ToString("N");
    public string? ProjectId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan SessionTtl { get; set; } = TimeSpan.FromMinutes(30);

    public DateTime LastActivity
    {
        get { lock (_lock) return _lastActivity; }
    }

    public string? MCPConnectionId
    {
        get { lock (_lock) return _mcpConnectionId; }
        set { lock (_lock) _mcpConnectionId = value; }
    }

    public string? BrowserConnectionId
    {
        get { lock (_lock) return _browserConnectionId; }
        set { lock (_lock) _browserConnectionId = value; }
    }

    public WebSocket? BrowserWebSocket
    {
        get { lock (_lock) return _browserWebSocket; }
        set { lock (_lock) _browserWebSocket = value; }
    }

    public bool IsActive
    {
        get
        {
            lock (_lock)
            {
                bool isConnected = _mcpConnectionId != null || _browserWebSocket?.State == WebSocketState.Open;
                bool isExpired = DateTime.UtcNow - _lastActivity > SessionTtl;
                return isConnected && !isExpired;
            }
        }
    }

    public void Touch()
    {
        lock (_lock) _lastActivity = DateTime.UtcNow;
    }

    /// <summary>
    /// Serialized send to the browser socket. WebSocket allows only one outstanding
    /// SendAsync; concurrent hub commands must queue here, not race.
    /// </summary>
    /// <returns>false when the browser socket is missing or closed.</returns>
    public async Task<bool> SendToBrowserAsync(string json, CancellationToken ct)
    {
        var ws = BrowserWebSocket;
        if (ws is null || ws.State != WebSocketState.Open)
            return false;

        var data = Encoding.UTF8.GetBytes(json);
        await _sendLock.WaitAsync(ct);
        try
        {
            if (ws.State != WebSocketState.Open)
                return false;
            await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, ct);
            return true;
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
