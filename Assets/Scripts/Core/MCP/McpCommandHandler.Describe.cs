using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public partial class McpCommandHandler
    {
        private McpResponse HandleDescribeScene(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsDescribeScene>() ?? new ParamsDescribeScene();
            if (p.max_chars < SceneDigestText.MinMaxChars || p.max_chars > SceneDigestText.MaxMaxChars)
                return McpResponse.Error(req.id, -32602,
                    $"max_chars must be {SceneDigestText.MinMaxChars}..{SceneDigestText.MaxMaxChars} characters, got {p.max_chars}");
            if (!McpReference.TryParse(p.@ref, out var reference, out var refError))
                return McpResponse.Error(req.id, -32602, refError);
            if (!McpSceneDigest.TryCollect(p.scope, reference, out var input, out var scopeError))
                return McpResponse.Error(req.id, -1, scopeError);
            return McpResponse.Result(req.id, SceneDigestText.Build(input, p.max_chars));
        }
    }
}
