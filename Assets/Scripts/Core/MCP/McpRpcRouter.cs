using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public sealed class McpRpcRouter
    {
        public const string ServerName = "unity-kitchen";

        public const string DefaultProtocolVersion = "2025-06-18";

        public static readonly IReadOnlyList<string> SupportedProtocolVersions = new List<string>
        {
            "2024-11-05", "2025-03-26", "2025-06-18", "2025-11-25"
        };

        private const int ParseError = -32700;
        private const int InvalidRequest = -32600;
        private const int MethodNotFound = -32601;
        private const int InvalidParams = -32602;
        private const int InternalError = -32603;

        private readonly McpToolCall _toolCall;
        private readonly string _serverVersion;
        private readonly JsonSerializer _serializer;

        public McpRpcRouter(Func<McpRequest, McpResponse> dispatch, string serverVersion)
        {
            _toolCall = new McpToolCall(dispatch);
            _serverVersion = serverVersion;
            _serializer = JsonSerializer.Create(McpJson.Settings);
        }

        public (int status, string? body) Handle(string bodyJson)
        {
            JToken parsed;
            try
            {
                parsed = JToken.Parse(bodyJson);
            }
            catch (JsonException ex)
            {
                return (200, ErrorEnvelope(JValue.CreateNull(), ParseError, "Parse error: " + ex.Message)
                    .ToString(Formatting.None));
            }

            if (parsed is JArray batch)
            {
                if (batch.Count == 0)
                    return (400, ErrorEnvelope(JValue.CreateNull(), InvalidRequest, "Invalid request")
                        .ToString(Formatting.None));

                var replies = new JArray();
                foreach (var item in batch)
                {
                    var reply = Answer(item);
                    if (reply != null) replies.Add(reply);
                }
                return replies.Count == 0 ? (202, null) : (200, replies.ToString(Formatting.None));
            }

            var single = Answer(parsed);
            return single == null ? (202, (string?)null) : (200, single.ToString(Formatting.None));
        }

        private JObject? Answer(JToken message)
        {
            if (!(message is JObject request))
                return ErrorEnvelope(JValue.CreateNull(), InvalidRequest, "Invalid request");

            if (!request.TryGetValue("id", out var id))
                return null;

            var version = request["jsonrpc"];
            if (version == null || version.Type != JTokenType.String || version.Value<string>() != "2.0")
                return ErrorEnvelope(id, InvalidRequest, "Invalid request");

            var method = request["method"];
            if (method == null || method.Type != JTokenType.String)
                return ErrorEnvelope(id, InvalidRequest, "Invalid request");

            var name = method.Value<string>() ?? string.Empty;
            var parameters = request["params"] as JObject ?? new JObject();

            try
            {
                switch (name)
                {
                    case "initialize":
                        return ResultEnvelope(id, Initialize(parameters));
                    case "ping":
                        return ResultEnvelope(id, new JObject());
                    case "tools/list":
                        return ResultEnvelope(id, ToolsList());
                    case "tools/call":
                        return ToolsCall(id, parameters);
                    default:
                        return ErrorEnvelope(id, MethodNotFound, "Method not found: " + name);
                }
            }
            catch (Exception ex)
            {
                return ErrorEnvelope(id, InternalError, "Internal error: " + ex.Message);
            }
        }

        private JObject Initialize(JObject parameters)
        {
            return new JObject
            {
                ["protocolVersion"] = NegotiatedVersion(parameters["protocolVersion"]),
                ["capabilities"] = new JObject { ["tools"] = new JObject() },
                ["serverInfo"] = new JObject
                {
                    ["name"] = ServerName,
                    ["version"] = _serverVersion
                },
                ["instructions"] = McpGuideTexts.Instructions
            };
        }

        private static string NegotiatedVersion(JToken? requested)
        {
            if (requested == null || requested.Type != JTokenType.String)
                return DefaultProtocolVersion;
            var asked = requested.Value<string>();
            foreach (var supported in SupportedProtocolVersions)
                if (supported == asked) return supported;
            return DefaultProtocolVersion;
        }

        private JObject ToolsList()
        {
            var tools = new JArray();
            foreach (var tool in McpToolRegistry.Tools)
            {
                tools.Add(new JObject
                {
                    ["name"] = tool.Name,
                    ["title"] = tool.Title,
                    ["description"] = tool.Description,
                    ["inputSchema"] = JToken.FromObject(McpJsonSchema.ForTool(tool), _serializer),
                    ["annotations"] = JToken.FromObject(McpJsonSchema.Annotations(tool), _serializer)
                });
            }
            return new JObject { ["tools"] = tools };
        }

        private JObject ToolsCall(JToken id, JObject parameters)
        {
            var requested = parameters["name"];
            if (requested == null || requested.Type != JTokenType.String)
                return ErrorEnvelope(id, InvalidParams, "Invalid params: name is required");

            var toolName = requested.Value<string>();
            McpToolDef? tool = null;
            foreach (var candidate in McpToolRegistry.Tools)
                if (candidate.Name == toolName) { tool = candidate; break; }

            if (tool == null)
                return ErrorEnvelope(id, InvalidParams, "Invalid params: unknown tool " + toolName);

            var arguments = parameters["arguments"];
            if (arguments != null && arguments.Type != JTokenType.Null && !(arguments is JObject))
                return ErrorEnvelope(id, InvalidParams, "Invalid params: arguments must be an object");

            return ResultEnvelope(id, _toolCall.Invoke(
                tool, arguments as JObject ?? new JObject(), id.ToString()));
        }

        private static JObject ResultEnvelope(JToken id, JObject result) =>
            new JObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone(), ["result"] = result };

        private static JObject ErrorEnvelope(JToken id, int code, string message) =>
            new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id.DeepClone(),
                ["error"] = new JObject { ["code"] = code, ["message"] = message }
            };
    }
}
