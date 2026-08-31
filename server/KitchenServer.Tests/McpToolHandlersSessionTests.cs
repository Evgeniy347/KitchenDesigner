using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using KitchenServer.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace KitchenServer.Tests;

/// <summary>
/// Streamable-HTTP session persistence: the binding must survive across separate HTTP
/// requests even though <see cref="RequestContext{T}.Server"/> is a different object
/// per request. The authoritative key is the <c>Mcp-Session-Id</c> header.
/// </summary>
public class McpToolHandlersSessionTests
{
    private const string SessionIdHeader = "Mcp-Session-Id";

    [Fact]
    public async Task Authenticate_SetsMcpSessionIdHeader()
    {
        var (manager, session, services, accessor) = CreateServices();
        session.BrowserWebSocket = new RespondingWebSocket(session);
        accessor.HttpContext = new DefaultHttpContext { RequestServices = services };

        var request = new RequestContext<CallToolRequestParams>(new FakeMcpServer(), new JsonRpcRequest { Method = "tools/call" }, new CallToolRequestParams
        {
            Name = "authenticate",
            Arguments = new Dictionary<string, JsonElement> { ["key"] = JsonSerializer.SerializeToElement(session.TabKey) },
        })
        {
            Services = services,
        };

        await McpToolHandlers.CallToolAsync(request, default);

        Assert.True(accessor.HttpContext.Response.Headers.ContainsKey(SessionIdHeader));
        var sessionId = accessor.HttpContext.Response.Headers[SessionIdHeader].ToString();
        Assert.False(string.IsNullOrWhiteSpace(sessionId));
    }

    [Fact]
    public async Task CallTool_WithSessionIdHeader_FindsBoundSessionEvenWithDifferentServer()
    {
        var (manager, session, services, accessor) = CreateServices();
        session.BrowserWebSocket = new RespondingWebSocket(session);
        accessor.HttpContext = new DefaultHttpContext { RequestServices = services };

        var authRequest = new RequestContext<CallToolRequestParams>(new FakeMcpServer(), new JsonRpcRequest { Method = "tools/call" }, new CallToolRequestParams
        {
            Name = "authenticate",
            Arguments = new Dictionary<string, JsonElement> { ["key"] = JsonSerializer.SerializeToElement(session.TabKey) },
        })
        {
            Services = services,
        };

        await McpToolHandlers.CallToolAsync(authRequest, default);
        var sessionId = accessor.HttpContext.Response.Headers[SessionIdHeader].ToString();

        // Simulate a separate HTTP request: different McpServer instance, but same session id header.
        session.BrowserWebSocket = null;
        accessor.HttpContext = new DefaultHttpContext { RequestServices = services };
        accessor.HttpContext.Request.Headers[SessionIdHeader] = sessionId;
        var toolRequest = new RequestContext<CallToolRequestParams>(new FakeMcpServer(), new JsonRpcRequest { Method = "tools/call" }, new CallToolRequestParams
        {
            Name = "get_all_elements",
        })
        {
            Services = services,
        };

        var result = await McpToolHandlers.CallToolAsync(toolRequest, default);

        var text = Assert.Single(result.Content) is TextContentBlock tb ? tb.Text : "";
        Assert.DoesNotContain(McpToolHandlers.NotAuthenticatedMessage, text);
        Assert.Contains("Kitchen client is not connected", text);
    }

    [Fact]
    public async Task CallTool_WithoutSessionIdHeaderAndDifferentServer_RequiresAuthentication()
    {
        var (manager, session, services, accessor) = CreateServices();
        session.BrowserWebSocket = new RespondingWebSocket(session);
        accessor.HttpContext = new DefaultHttpContext { RequestServices = services };

        var authServer = new FakeMcpServer();
        var authRequest = new RequestContext<CallToolRequestParams>(authServer, new JsonRpcRequest { Method = "tools/call" }, new CallToolRequestParams
        {
            Name = "authenticate",
            Arguments = new Dictionary<string, JsonElement> { ["key"] = JsonSerializer.SerializeToElement(session.TabKey) },
        })
        {
            Services = services,
        };

        await McpToolHandlers.CallToolAsync(authRequest, default);

        // New HTTP request: different server, no session id header -> binding is lost.
        accessor.HttpContext = new DefaultHttpContext { RequestServices = services };
        var toolRequest = new RequestContext<CallToolRequestParams>(new FakeMcpServer(), new JsonRpcRequest { Method = "tools/call" }, new CallToolRequestParams
        {
            Name = "get_all_elements",
        })
        {
            Services = services,
        };

        var result = await McpToolHandlers.CallToolAsync(toolRequest, default);

        var text = Assert.Single(result.Content) is TextContentBlock tb ? tb.Text : "";
        Assert.Contains(McpToolHandlers.NotAuthenticatedMessage, text);
    }
    [Fact]
    public async Task CallTool_WithRemoteEndpointFallback_FindsBoundSessionWithoutSessionIdHeader()
    {
        var (manager, session, services, accessor) = CreateServices();
        session.BrowserWebSocket = new RespondingWebSocket(session);
        accessor.HttpContext = CreateHttpContext(services, "192.168.1.100", 12345);

        var authRequest = new RequestContext<CallToolRequestParams>(new FakeMcpServer(), new JsonRpcRequest { Method = "tools/call" }, new CallToolRequestParams
        {
            Name = "authenticate",
            Arguments = new Dictionary<string, JsonElement> { ["key"] = JsonSerializer.SerializeToElement(session.TabKey) },
        })
        {
            Services = services,
        };

        await McpToolHandlers.CallToolAsync(authRequest, default);

        // Same endpoint, different server, no session id header -> binding survives.
        accessor.HttpContext = CreateHttpContext(services, "192.168.1.100", 12345);
        var toolRequest = new RequestContext<CallToolRequestParams>(new FakeMcpServer(), new JsonRpcRequest { Method = "tools/call" }, new CallToolRequestParams
        {
            Name = "get_all_elements",
        })
        {
            Services = services,
        };

        var result = await McpToolHandlers.CallToolAsync(toolRequest, default);
        var text = Assert.Single(result.Content) is TextContentBlock tb ? tb.Text : "";
        Assert.DoesNotContain(McpToolHandlers.NotAuthenticatedMessage, text);
    }

    [Fact]
    public async Task CallTool_AfterPortChange_FallsBackToIpBinding()
    {
        var (manager, session, services, accessor) = CreateServices();
        session.BrowserWebSocket = new RespondingWebSocket(session);
        accessor.HttpContext = CreateHttpContext(services, "192.168.1.100", 12345);

        var authRequest = new RequestContext<CallToolRequestParams>(new FakeMcpServer(), new JsonRpcRequest { Method = "tools/call" }, new CallToolRequestParams
        {
            Name = "authenticate",
            Arguments = new Dictionary<string, JsonElement> { ["key"] = JsonSerializer.SerializeToElement(session.TabKey) },
        })
        {
            Services = services,
        };

        await McpToolHandlers.CallToolAsync(authRequest, default);

        // Simulate connection reset: same IP, different port -> falls back to IP binding.
        accessor.HttpContext = CreateHttpContext(services, "192.168.1.100", 12346);
        var toolRequest = new RequestContext<CallToolRequestParams>(new FakeMcpServer(), new JsonRpcRequest { Method = "tools/call" }, new CallToolRequestParams
        {
            Name = "get_all_elements",
        })
        {
            Services = services,
        };

        var result = await McpToolHandlers.CallToolAsync(toolRequest, default);
        var text = Assert.Single(result.Content) is TextContentBlock tb ? tb.Text : "";
        Assert.DoesNotContain(McpToolHandlers.NotAuthenticatedMessage, text);
    }

    [Fact]
    public async Task Authenticate_WithSameKeyAfterDisconnect_RebindsNewEndpoint()
    {
        var (manager, session, services, accessor) = CreateServices();
        session.BrowserWebSocket = new RespondingWebSocket(session);
        accessor.HttpContext = CreateHttpContext(services, "192.168.1.100", 12345);

        var authRequest1 = new RequestContext<CallToolRequestParams>(new FakeMcpServer(), new JsonRpcRequest { Method = "tools/call" }, new CallToolRequestParams
        {
            Name = "authenticate",
            Arguments = new Dictionary<string, JsonElement> { ["key"] = JsonSerializer.SerializeToElement(session.TabKey) },
        })
        {
            Services = services,
        };
        await McpToolHandlers.CallToolAsync(authRequest1, default);

        // Connection reset: re-authenticate with the same key from a new port.
        accessor.HttpContext = CreateHttpContext(services, "192.168.1.100", 12346);
        var authRequest2 = new RequestContext<CallToolRequestParams>(new FakeMcpServer(), new JsonRpcRequest { Method = "tools/call" }, new CallToolRequestParams
        {
            Name = "authenticate",
            Arguments = new Dictionary<string, JsonElement> { ["key"] = JsonSerializer.SerializeToElement(session.TabKey) },
        })
        {
            Services = services,
        };
        var authResult = await McpToolHandlers.CallToolAsync(authRequest2, default);
        var authText = Assert.Single(authResult.Content) is TextContentBlock authTb ? authTb.Text : "";
        Assert.DoesNotContain(McpToolHandlers.NotAuthenticatedMessage, authText);

        // Subsequent calls from the new endpoint work.
        accessor.HttpContext = CreateHttpContext(services, "192.168.1.100", 12346);
        var toolRequest = new RequestContext<CallToolRequestParams>(new FakeMcpServer(), new JsonRpcRequest { Method = "tools/call" }, new CallToolRequestParams
        {
            Name = "get_all_elements",
        })
        {
            Services = services,
        };

        var result = await McpToolHandlers.CallToolAsync(toolRequest, default);
        var text = Assert.Single(result.Content) is TextContentBlock toolTb ? toolTb.Text : "";
        Assert.DoesNotContain(McpToolHandlers.NotAuthenticatedMessage, text);
    }

    private static DefaultHttpContext CreateHttpContext(IServiceProvider services, string ip, int port)
    {
        var ctx = new DefaultHttpContext { RequestServices = services };
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
        ctx.Connection.RemotePort = port;
        return ctx;
    }

    private static (McpSessionManager Manager, KitchenServer.Web.Services.McpSession Session, IServiceProvider Services, IHttpContextAccessor Accessor) CreateServices()
    {
        var manager = new McpSessionManager();
        var session = manager.CreateSession("user1", "proj-1");
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(manager);
        serviceCollection.AddHttpContextAccessor();
        var services = serviceCollection.BuildServiceProvider();
        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        return (manager, session, services, accessor);
    }

    /// <summary>WebSocket fake that immediately resolves every sent request with an empty success reply.</summary>
    private sealed class RespondingWebSocket : WebSocket
    {
        private readonly KitchenServer.Web.Services.McpSession _session;

        public RespondingWebSocket(KitchenServer.Web.Services.McpSession session) => _session = session;

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string CloseStatusDescription => string.Empty;
        public override WebSocketState State => WebSocketState.Open;
        public override string SubProtocol => string.Empty;

        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
            Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            var json = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("id", out var idProp))
            {
                var id = idProp.GetString() ?? idProp.GetRawText();
                var response = JsonDocument.Parse("{\"type\":\"result\",\"data\":{}}").RootElement.Clone();
                _session.ResolveResponse(id, response);
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>Minimal concrete <see cref="McpServer"/> for tests — only identity matters.</summary>
#pragma warning disable MCPEXP002
    private sealed class FakeMcpServer : McpServer
#pragma warning restore MCPEXP002
    {
        public override ClientCapabilities ClientCapabilities => new();
        public override Implementation ClientInfo => new() { Name = "test", Version = "1.0" };
        public override McpServerOptions ServerOptions => new();
        public override IServiceProvider Services => EmptyServiceProvider.Instance;
        [Obsolete]
        public override LoggingLevel? LoggingLevel => null;
        public override string SessionId => Guid.NewGuid().ToString("N");
        public override string NegotiatedProtocolVersion => "2024-11-05";

        public override Task RunAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public override Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override IAsyncDisposable RegisterNotificationHandler(string method, Func<JsonRpcNotification, CancellationToken, ValueTask> handler) =>
            NullAsyncDisposable.Instance;
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public static readonly EmptyServiceProvider Instance = new();
            public object? GetService(Type serviceType) => null;
        }

        private sealed class NullAsyncDisposable : IAsyncDisposable
        {
            public static readonly NullAsyncDisposable Instance = new();
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
