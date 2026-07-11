using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using KitchenServer.Web.Hubs;
using KitchenServer.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace KitchenServer.Web.Endpoints;

public static class McpEndpoints
{
    private record SessionResponse(string SessionId, string AccessKey, string? ProjectId, DateTime CreatedAt,
        string McpUrl, string WsUrl, bool IsActive);

    public static void MapMcpEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/mcp");

        api.MapGet("/connect", (string key, McpSessionManager sessions) =>
        {
            var session = sessions.ValidateAccess(key);
            if (session == null)
                return Results.NotFound(new { error = "Invalid or expired access key" });

            return Results.Ok(new
            {
                sessionId = session.SessionId,
                userId = session.UserId,
                projectId = session.ProjectId,
                mcpUrl = $"ws://localhost:5000/hubs/mcp?access_key={session.AccessKey}",
                wsUrl = $"ws://localhost:5000/api/mcp/ws?key={session.AccessKey}",
                accessKey = session.AccessKey,
                createdAt = session.CreatedAt
            });
        });

        var auth = api.RequireAuthorization();

        auth.MapGet("/sessions", (McpSessionManager sessions, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Results.Unauthorized();

            var list = sessions.GetUserSessions(userId).Select(s => new SessionResponse(
                s.SessionId, s.AccessKey, s.ProjectId, s.CreatedAt,
                $"ws://localhost:5000/hubs/mcp?access_key={s.AccessKey}",
                $"ws://localhost:5000/api/mcp/ws?key={s.AccessKey}",
                s.IsActive
            ));

            return Results.Ok(list);
        });

        auth.MapPost("/sessions", (CreateSessionRequest req, McpSessionManager sessions, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Results.Unauthorized();

            var session = sessions.CreateSession(userId, req.ProjectId);
            return Results.Created($"/api/mcp/sessions/{session.SessionId}", new SessionResponse(
                session.SessionId, session.AccessKey, session.ProjectId, session.CreatedAt,
                $"ws://localhost:5000/hubs/mcp?access_key={session.AccessKey}",
                $"ws://localhost:5000/api/mcp/ws?key={session.AccessKey}",
                session.IsActive
            ));
        });

        auth.MapDelete("/sessions/{id}", (string id, McpSessionManager sessions, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Results.Unauthorized();

            var session = sessions.GetSession(id);
            if (session == null || session.UserId != userId)
                return Results.NotFound();

            sessions.CloseSession(id);
            return Results.Ok(new { ok = true });
        });

        app.Map("/api/mcp/ws", async (HttpContext context, McpSessionManager sessions, IHubContext<McpHub> hubContext) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("WebSocket connection required");
                return;
            }

            var key = context.Request.Headers["X-MCP-Access-Key"].FirstOrDefault()
                      ?? context.Request.Query["key"].ToString();
            var session = sessions.ValidateAccess(key);
            if (session == null)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid access key");
                return;
            }

            var ws = await context.WebSockets.AcceptWebSocketAsync();
            session.BrowserWebSocket = ws;
            sessions.RegisterBrowserConnection(session.SessionId, context.Connection.Id);

            var buffer = new byte[1024 * 64];
            var messageBuilder = new StringBuilder();

            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                        if (result.EndOfMessage)
                        {
                            var json = messageBuilder.ToString().Trim();
                            messageBuilder.Clear();

                            if (!string.IsNullOrEmpty(json))
                            {
                                sessions.Touch(session.SessionId);
                                var connectionId = session.MCPConnectionId;
                                if (connectionId != null)
                                {
                                    await hubContext.Clients.Client(connectionId)
                                        .SendAsync("OnResponse", json);
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            finally
            {
                session.BrowserWebSocket = null;
                sessions.RemoveConnection(context.Connection.Id);
                sessions.RemoveSessionCancellation(session.SessionId);
                if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                {
                    try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
                    catch { }
                }
            }
        });
    }
}

public record CreateSessionRequest(string? ProjectId);
