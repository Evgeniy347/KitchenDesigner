using System.Text;
using System.Text.Json;
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
            await ReplyError(json, -32600, "Command exceeds 64KB limit");
            return;
        }

        var session = _sessionManager.GetSessionByMCPConnection(Context.ConnectionId);
        if (session == null)
        {
            await ReplyError(json, -32000, "Session not found — reconnect with a valid access key");
            return;
        }

        _sessionManager.Touch(session.SessionId);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var delivered = await session.SendToBrowserAsync(json, cts.Token);
        if (!delivered)
        {
            // Without this the agent would hang until its own timeout.
            await ReplyError(json, -32001,
                "Kitchen client is not connected. Open the project in the browser (or desktop app) and retry.");
        }
    }

    /// <summary>Sends a JSON-RPC style error back to the calling agent, echoing the request id when parseable.</summary>
    private async Task ReplyError(string requestJson, int code, string message)
    {
        string id = "unknown";
        try
        {
            using var doc = JsonDocument.Parse(requestJson);
            if (doc.RootElement.TryGetProperty("id", out var idProp))
                id = idProp.ValueKind == JsonValueKind.String ? idProp.GetString() ?? "unknown" : idProp.GetRawText();
        }
        catch (JsonException) { }

        var error = JsonSerializer.Serialize(new
        {
            id,
            type = "error",
            error = new { code, message }
        });
        await Clients.Caller.SendAsync("OnResponse", error);
    }
}
