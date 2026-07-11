using System.Net.WebSockets;
using System.Text;
using KitchenServer.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace KitchenServer.Web.Hubs;

public class McpHub : Hub
{
    private readonly McpSessionManager _sessionManager;
    private readonly ILogger<McpHub> _logger;

    public McpHub(McpSessionManager sessionManager, ILogger<McpHub> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var accessKey = httpContext?.Request.Query["access_key"].ToString() ?? "";

        var session = string.IsNullOrEmpty(accessKey)
            ? null
            : _sessionManager.ValidateAccess(accessKey);

        if (session == null)
        {
            _logger.LogWarning("MCP auth failed for connection {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{session.UserId}");
        _sessionManager.RegisterMCPConnection(session.SessionId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _sessionManager.RemoveConnection(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendCommand(string json)
    {
        const int maxSize = 64 * 1024;
        if (Encoding.UTF8.GetByteCount(json) > maxSize)
        {
            _logger.LogWarning("MCP command from {ConnectionId} exceeds 64KB limit", Context.ConnectionId);
            return;
        }

        var session = _sessionManager.GetSessionByMCPConnection(Context.ConnectionId);
        if (session?.BrowserWebSocket == null) return;

        _sessionManager.Touch(session.SessionId);

        var ws = session.BrowserWebSocket;
        if (ws.State != WebSocketState.Open) return;

        var data = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
