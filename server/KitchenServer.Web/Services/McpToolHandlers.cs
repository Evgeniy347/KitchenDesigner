using System.Text.Json;
using System.Text.Json.Nodes;
using KitchenDesigner.Core.MCP.Contract;
using KitchenServer.McpContract;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace KitchenServer.Web.Services;

/// <summary>
/// Real MCP (Streamable HTTP) surface. tools/list is built from the shared C#
/// contract (McpToolRegistry); tools/call authenticates with a tab key on the
/// FIRST call, then forwards every command to the bound browser tab over WebSocket.
/// </summary>
public static class McpToolHandlers
{
    public const string AuthToolName = "authenticate";

    /// <summary>HTTP header that carries the MCP session id across Streamable-HTTP requests.</summary>
    public const string SessionIdHeader = "Mcp-Session-Id";

    /// <summary>Returned for any tool call before the agent has authenticated.</summary>
    public const string NotAuthenticatedMessage =
        "требуется подключение к проекту. Если ты уже был подключён, соединение могло оборваться — " +
        "попробуй вызвать authenticate с тем же ключом ещё раз. Иначе попроси пользователя передать ключ проекта.";

    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Server instructions — the auth-first workflow is stated up front.</summary>
    public const string Instructions =
        "This server controls a 3D kitchen / furniture BOARD designer running in Unity, in the user's browser tab.\n\n" +
        "AUTHENTICATION IS REQUIRED FIRST. Before any other tool works you MUST call `authenticate` with the\n" +
        "project key. The user opens their project in a browser tab; a project key is shown at the top of that tab\n" +
        "(next to a red/green light). Ask the user to copy that key and pass it to you, then call authenticate{key}.\n" +
        "Every other tool returns an error until you do. After authenticating, the light turns green and all tools\n" +
        "operate on that tab.\n\n" +
        "UNITS: position x/y/z are in METERS; size width/height/depth are in MILLIMETERS (1 m = 1000 mm).\n" +
        "Call get_all_elements first before editing; after each change call get_violations (expect count 0).";

    // ── tools/list ────────────────────────────────────────────────────────────

    public static ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> _, CancellationToken __)
    {
        var tools = new List<Tool> { AuthTool() };
        foreach (var def in McpToolRegistry.Tools)
        {
            if (def.StaticText) continue; // guide et al. live on the local bridge only
            tools.Add(new Tool
            {
                Name = def.Name,
                Title = def.Title,
                Description = def.Description,
                InputSchema = McpJsonSchema.BuildInputSchema(def.ParamsType),
                Annotations = Annotations(def),
            });
        }
        return ValueTask.FromResult(new ListToolsResult { Tools = tools });
    }

    private static Tool AuthTool() => new()
    {
        Name = AuthToolName,
        Title = "Connect to project (authenticate)",
        Description =
            "Call this FIRST. Pass the project key shown at the top of the user's open project tab. On success " +
            "the tab's light turns green and every other tool starts operating on that tab.",
        InputSchema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["key"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "The project key copied from the top of the open project tab.",
                },
            },
            ["required"] = new JsonArray { "key" },
        }),
        Annotations = new ToolAnnotations { ReadOnlyHint = false, OpenWorldHint = false, Title = "Connect to project" },
    };

    private static ToolAnnotations Annotations(McpToolDef def) => new()
    {
        Title = def.Title,
        ReadOnlyHint = def.Kind == McpToolKind.Read,
        DestructiveHint = def.Kind == McpToolKind.Destructive,
        OpenWorldHint = def.OpenWorld,
    };

    // ── tools/call ────────────────────────────────────────────────────────────

    public static async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> ctx, CancellationToken ct)
    {
        var services = ctx.Services ?? throw new InvalidOperationException("Service provider is not available.");
        var manager = services.GetRequiredService<McpSessionManager>();
        var httpAccessor = services.GetService<IHttpContextAccessor>();
        var sessionId = httpAccessor?.HttpContext?.Request.Headers[SessionIdHeader].FirstOrDefault();
        var remoteIp = httpAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var remotePort = httpAccessor?.HttpContext?.Connection.RemotePort ?? 0;
        var remoteEndpoint = !string.IsNullOrEmpty(remoteIp) && remotePort > 0 ? $"{remoteIp}:{remotePort}" : null;
        var agent = (object)ctx.Server;
        var name = ctx.Params?.Name ?? "";
        var args = ctx.Params?.Arguments;

        if (name == AuthToolName)
            return await AuthenticateAsync(manager, agent, sessionId, remoteEndpoint, remoteIp, args, httpAccessor, ct);

        var def = McpToolRegistry.Tools.FirstOrDefault(t => t.Name == name && !t.StaticText);
        if (def == null)
            return Error($"Unknown tool: {name}");

        var session = manager.GetBoundSession(agent, sessionId, remoteEndpoint, remoteIp);
        if (session == null)
            return Error(NotAuthenticatedMessage);

        var wireParams = BuildWireParams(def, args);
        try
        {
            var response = await session.SendCommandAsync(def.Name, wireParams, CallTimeout, ct);
            return FromUnityResponse(response);
        }
        catch (McpBridgeException ex)
        {
            return Error(ex.Message);
        }
    }

    private static async ValueTask<CallToolResult> AuthenticateAsync(
        McpSessionManager manager, object agent, string? requestSessionId, string? remoteEndpoint, string? remoteIp,
        IDictionary<string, JsonElement>? args,
        IHttpContextAccessor? httpAccessor, CancellationToken ct)
    {
        var key = args != null && args.TryGetValue("key", out var k) && k.ValueKind == JsonValueKind.String
            ? k.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(key))
            return Error("Provide the project key: authenticate{ key: \"...\" }. It is shown at the top of the open project tab.");

        var session = manager.GetByKey(key.Trim());
        if (session == null)
            return Error("Unknown or expired project key. Ask the user to reopen the project tab and copy the current key.");
        if (!session.IsBrowserConnected)
            return Error("That project tab is not connected. Ask the user to open (or refresh) the project in the browser, then retry.");

        // Round-trip to the tab to confirm it is alive before binding.
        try
        {
            await session.SendCommandAsync("ping", null, CallTimeout, ct);
        }
        catch (McpBridgeException ex)
        {
            return Error($"Could not reach the project tab: {ex.Message}");
        }

        // Prefer the session id created during the MCP initialize handshake; only fall
        // back to minting a new one for clients that do not support Streamable-HTTP sessions.
        var sessionId = string.IsNullOrEmpty(requestSessionId)
            ? manager.BindAgent(agent, session)
            : requestSessionId;

        if (!string.IsNullOrEmpty(requestSessionId))
            manager.BindAgentToSessionId(requestSessionId, session);

        // Bind by remote endpoint (IP:port) for stateless clients (e.g. opencode) that never
        // send a session id header. Falls back to IP-only if the port is not available.
        manager.BindEndpoint(remoteEndpoint, remoteIp, session);

        WriteSessionIdHeader(httpAccessor, sessionId);
        var project = string.IsNullOrEmpty(session.ProjectId) ? "" : $" (project {session.ProjectId})";
        return Text($"Connected to the project tab{project}. Authentication OK — all tools now operate on that tab.");
    }

    private static void WriteSessionIdHeader(IHttpContextAccessor? httpAccessor, string sessionId)
    {
        var response = httpAccessor?.HttpContext?.Response;
        if (response == null || response.HasStarted || response.Headers.IsReadOnly)
            return;

        response.Headers[SessionIdHeader] = sessionId;
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>Build the Unity wire params object from the agent args, applying the agent->wire rename map.</summary>
    private static JsonObject BuildWireParams(McpToolDef def, IDictionary<string, JsonElement>? args)
    {
        var obj = new JsonObject();
        if (args == null || def.ParamsType == null) return obj;

        var rename = McpJsonSchema.BuildRenameMap(def.ParamsType);
        foreach (var (k, v) in args)
        {
            var wireKey = rename != null && rename.TryGetValue(k, out var w) ? w : k;
            obj[wireKey] = JsonNode.Parse(v.GetRawText());
        }
        return obj;
    }

    /// <summary>Turn Unity's {id,type,data,etag} envelope into an MCP result.</summary>
    private static CallToolResult FromUnityResponse(JsonElement response)
    {
        var type = response.TryGetProperty("type", out var t) ? t.GetString() : "result";
        response.TryGetProperty("data", out var data);

        if (type == "error")
        {
            var msg = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("message", out var m)
                ? m.GetString() ?? "Unity returned an error"
                : "Unity returned an error";
            return Error(msg);
        }

        var text = data.ValueKind == JsonValueKind.Undefined
            ? "{}"
            : JsonSerializer.Serialize(data, PrettyJson);
        return Text(text);
    }

    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

    private static CallToolResult Text(string text) => new()
    {
        Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
    };

    private static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = new List<ContentBlock> { new TextContentBlock { Text = $"ERROR: {message}" } },
    };
}
