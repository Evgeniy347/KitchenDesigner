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

    private static string BaseUrl(HttpContext ctx) =>
        $"{ctx.Request.Scheme}://{ctx.Request.Host}";

    private static SessionResponse ToResponse(McpSession s, McpUrlBuilder urls, HttpContext ctx)
    {
        var baseUrl = BaseUrl(ctx);
        return new SessionResponse(
            s.SessionId, s.AccessKey, s.ProjectId, s.CreatedAt,
            urls.HubUrl(baseUrl, s.AccessKey),
            urls.BrowserWsUrl(baseUrl, s.AccessKey),
            s.IsActive);
    }

    public static void MapMcpEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/mcp");

        api.MapGet("/connect", (string key, McpSessionManager sessions, McpUrlBuilder urls, HttpContext ctx) =>
        {
            var session = sessions.ValidateAccess(key);
            if (session == null)
                return Results.NotFound(new { error = "Invalid or expired access key" });

            var baseUrl = BaseUrl(ctx);
            return Results.Ok(new
            {
                sessionId = session.SessionId,
                userId = session.UserId,
                projectId = session.ProjectId,
                mcpUrl = urls.HubUrl(baseUrl, session.AccessKey),
                wsUrl = urls.BrowserWsUrl(baseUrl, session.AccessKey),
                accessKey = session.AccessKey,
                createdAt = session.CreatedAt
            });
        });

        var auth = api.RequireAuthorization();

        auth.MapGet("/sessions", (McpSessionManager sessions, McpUrlBuilder urls, HttpContext ctx, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Results.Unauthorized();

            var list = sessions.GetUserSessions(userId).Select(s => ToResponse(s, urls, ctx));
            return Results.Ok(list);
        });

        auth.MapPost("/sessions", (CreateSessionRequest req, McpSessionManager sessions, McpUrlBuilder urls, HttpContext ctx, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Results.Unauthorized();

            var session = sessions.CreateSession(userId, req.ProjectId);
            return Results.Created($"/api/mcp/sessions/{session.SessionId}", ToResponse(session, urls, ctx));
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
