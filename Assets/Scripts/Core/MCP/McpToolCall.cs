using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public sealed class McpToolCall
    {
        public const int TimeoutSeconds = 30;

        private readonly Func<McpRequest, McpResponse> _dispatch;

        public McpToolCall(Func<McpRequest, McpResponse> dispatch)
        {
            _dispatch = dispatch;
        }

        public JObject Invoke(McpToolDef tool, JObject arguments, string requestId)
        {
            if (tool.StaticText)
                return TextContent(GuideText(arguments));

            var wire = new JObject();
            var rename = McpJsonSchema.RenameTable(tool);
            foreach (var property in arguments.Properties())
            {
                var field = rename.TryGetValue(property.Name, out var wireName) ? wireName : property.Name;
                wire[field] = property.Value.DeepClone();
            }

            McpResponse response;
            try
            {
                response = _dispatch(new McpRequest
                {
                    id = requestId,
                    method = tool.Name,
                    Params = wire
                });
            }
            catch (TimeoutException)
            {
                return ErrorContent("Timeout after " + TimeoutSeconds + "s waiting for \"" + tool.Name
                    + "\". Is the Kitchen Designer app running and not frozen?");
            }
            catch (Exception ex)
            {
                return ErrorContent(ex.Message);
            }

            if (response.type == "error")
                return ErrorContent(FailureMessage(response.data));

            return TextContent(JsonConvert.SerializeObject(
                response.data ?? new JObject(), Formatting.Indented, McpJson.Settings));
        }

        private static string GuideText(JObject arguments)
        {
            var requested = arguments["topic"];
            if (requested != null && requested.Type == JTokenType.String)
            {
                var topic = requested.Value<string>();
                if (topic != null && McpGuideTexts.Topics.TryGetValue(topic, out var text))
                    return text;
            }
            return McpGuideTexts.Topics[McpGuideTexts.DefaultTopic];
        }

        private static string FailureMessage(object? data)
        {
            if (data == null)
                return "Unity returned an error without a message";
            var asToken = JToken.FromObject(data);
            var message = asToken["message"];
            return message != null ? message.ToString() : asToken.ToString();
        }

        private static JObject TextContent(string text) =>
            new JObject { ["content"] = ContentArray(text) };

        private static JObject ErrorContent(string message) =>
            new JObject { ["content"] = ContentArray("ERROR: " + message), ["isError"] = true };

        private static JArray ContentArray(string text) =>
            new JArray { new JObject { ["type"] = "text", ["text"] = text } };
    }
}
