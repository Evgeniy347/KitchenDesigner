using System;

namespace KitchenDesigner.Core.MCP.Contract
{
    [Serializable]
    public class ParamsDescribeScene
    {
        [McpParam("COUNT of characters the digest may take at most (default 1500). It is never exceeded: lines that do not fit are replaced by one last line '+N more, use scope'.",
            Min = SceneDigestText.MinMaxChars, Max = SceneDigestText.MaxMaxChars)]
        public int max_chars = SceneDigestText.DefaultMaxChars;

        [McpParam("Narrow the digest to one place and list its PARTS one per line instead of the modules: a module name (module:B4 also works), room:ID or a room id, "
            + "or a selector (name mask, type:board, module:B4*, thickness==18 - see guide topic bulk). Omit for the whole scene.")]
        public string? scope;

        [McpParam(McpRefText.Output)] public string? @ref;
    }
}
